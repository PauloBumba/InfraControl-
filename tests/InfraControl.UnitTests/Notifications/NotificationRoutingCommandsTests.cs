using FluentAssertions;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.Routing;
using InfraControl.Application.Notifications.Routing.Ports;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Options;
using Moq;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// F39-T003: CQRS Administer da política — defaults legados no GET sem
/// linha, upsert com bump de versão e validação pt-BR (evento do catálogo,
/// canal do enum, severidade 0–5, IDs numéricos do Zabbix).
/// </summary>
public sealed class NotificationRoutingCommandsTests
{
    private static readonly SaveNotificationRoutingCommand ValidCommand = new(
        Enabled: true,
        Defaults: ["Email", "SignalR"],
        Rules:
        [
            new SaveNotificationRoutingRuleRequest(
                Id: null,
                EventType: "IncidentCreatedDomainEvent",
                Enabled: true,
                MinSeverity: 4,
                MaxSeverity: null,
                HostIds: ["10"],
                HostGroupIds: null,
                TriggerId: null,
                Tags: [new SaveNotificationRoutingTagRequest("service", "web")],
                Channels: ["WhatsApp"]),
        ]);

    private static Mock<INotificationRoutingRepository> Repository(
        NotificationRoutingState? state = null)
    {
        var repository = new Mock<INotificationRoutingRepository>();
        var current = state ?? new NotificationRoutingState(null, []);
        repository.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => current);
        repository.Setup(r => r.SaveStateAsync(
                It.IsAny<NotificationRoutingPolicy>(),
                It.IsAny<IReadOnlyList<NotificationRoutingRule>>(),
                It.IsAny<CancellationToken>()))
            .Callback<NotificationRoutingPolicy, IReadOnlyList<NotificationRoutingRule>, CancellationToken>(
                (policy, rules, _) => current = new NotificationRoutingState(policy, rules))
            .Returns(Task.CompletedTask);
        return repository;
    }

    private static GetNotificationRoutingQueryHandler GetHandler(
        Mock<INotificationRoutingRepository> repository) =>
        new(repository.Object,
            Options.Create(new NotificationChannelsOptions
            {
                Default = ["Email", "SignalR"],
            }));

    // ── GET ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_WithoutPersistedPolicy_ShouldExposeLegacyDefaultsAndVersionZero()
    {
        var result = await GetHandler(Repository()).Handle(
            new GetNotificationRoutingQuery(), CancellationToken.None);

        result.Enabled.Should().BeFalse();
        result.Version.Should().Be(0);
        result.Defaults.Should().Equal("Email", "SignalR");
        result.Rules.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_WithPersistedPolicy_ShouldExposeState()
    {
        var policy = NotificationRoutingPolicy.Create(enabled: true, "Email,WhatsApp");
        var rule = NotificationRoutingRule.Create(
            "AlertCreatedDomainEvent", enabled: true, 4, 5,
            "10", string.Empty, null, "[]", "SignalR");
        var state = new NotificationRoutingState(policy, [rule]);

        var result = await GetHandler(Repository(state)).Handle(
            new GetNotificationRoutingQuery(), CancellationToken.None);

        result.Enabled.Should().BeTrue();
        result.Version.Should().Be(1);
        result.Defaults.Should().Equal("Email", "WhatsApp");
        var dto = result.Rules.Should().ContainSingle().Subject;
        dto.MinSeverity.Should().Be(4);
        dto.MaxSeverity.Should().Be(5);
        dto.HostIds.Should().Equal("10");
        dto.Channels.Should().Equal("SignalR");
    }

    // ── SAVE ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Save_FirstTime_ShouldCreatePolicyWithVersionOne()
    {
        var repository = Repository();

        var result = await new SaveNotificationRoutingCommandHandler(repository.Object)
            .Handle(ValidCommand, CancellationToken.None);

        result.Version.Should().Be(1);
        result.Enabled.Should().BeTrue();
        result.Defaults.Should().Equal("Email", "SignalR");

        var rule = result.Rules.Should().ContainSingle().Subject;
        rule.EventType.Should().Be("IncidentCreatedDomainEvent");
        rule.MinSeverity.Should().Be(4);
        rule.HostIds.Should().Equal("10");
        rule.Tags.Should().ContainSingle()
            .Which.Should().Be(new NotificationRoutingTagDto("service", "web"));
        rule.Channels.Should().Equal("WhatsApp");

        repository.Verify(r => r.SaveStateAsync(
            It.Is<NotificationRoutingPolicy>(p => p.Version == 1),
            It.Is<IReadOnlyList<NotificationRoutingRule>>(list => list.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Save_Again_ShouldBumpVersion()
    {
        var repository = Repository();
        var handler = new SaveNotificationRoutingCommandHandler(repository.Object);

        await handler.Handle(ValidCommand, CancellationToken.None);
        var second = await handler.Handle(ValidCommand, CancellationToken.None);

        second.Version.Should().Be(2);
    }

    [Fact]
    public async Task Save_UpdatingRule_ShouldPreserveIdAndCreateNewForNullId()
    {
        var repository = Repository();
        var handler = new SaveNotificationRoutingCommandHandler(repository.Object);
        var first = await handler.Handle(ValidCommand, CancellationToken.None);
        var existingId = first.Rules.Single().Id;

        var withUpdate = ValidCommand with
        {
            Rules =
            [
                ValidCommand.Rules[0] with { Id = existingId, Channels = ["Email"] },
                ValidCommand.Rules[0] with { Id = null, EventType = "AlertCreatedDomainEvent" },
            ],
        };
        var result = await handler.Handle(withUpdate, CancellationToken.None);

        result.Rules.Should().HaveCount(2);
        result.Rules.Should().Contain(r => r.Id == existingId && r.Channels.SequenceEqual(new[] { "Email" }));
        result.Rules.Should().Contain(r =>
            r.EventType == "AlertCreatedDomainEvent" && r.Id != existingId);
    }

    [Fact]
    public async Task Save_RulesAbsentFromPayload_ShouldBeRemoved()
    {
        var repository = Repository();
        var handler = new SaveNotificationRoutingCommandHandler(repository.Object);
        await handler.Handle(ValidCommand, CancellationToken.None);

        var result = await handler.Handle(
            ValidCommand with { Rules = [] }, CancellationToken.None);

        result.Rules.Should().BeEmpty();
    }

    // ── validação pt-BR ───────────────────────────────────────────────────────

    [Fact]
    public async Task Save_UnknownEventType_ShouldThrow()
    {
        var handler = new SaveNotificationRoutingCommandHandler(Repository().Object);
        var command = ValidCommand with
        {
            Rules = [ValidCommand.Rules[0] with { EventType = "NotInCatalog" }],
        };

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Tipo de evento desconhecido*");
    }

    [Fact]
    public async Task Save_InvalidChannel_ShouldThrow()
    {
        var handler = new SaveNotificationRoutingCommandHandler(Repository().Object);
        var command = ValidCommand with
        {
            Defaults = ["Email", "Pigeon"],
        };

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Canal de notificação inválido*");
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(6, null)]
    [InlineData(null, 6)]
    [InlineData(4, 2)]
    public async Task Save_InvalidSeverityRange_ShouldThrow(int? min, int? max)
    {
        var handler = new SaveNotificationRoutingCommandHandler(Repository().Object);
        var command = ValidCommand with
        {
            Rules = [ValidCommand.Rules[0] with { MinSeverity = min, MaxSeverity = max }],
        };

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Save_NonNumericZabbixId_ShouldThrow()
    {
        var handler = new SaveNotificationRoutingCommandHandler(Repository().Object);
        var command = ValidCommand with
        {
            Rules = [ValidCommand.Rules[0] with { HostIds = ["abc"] }],
        };

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ID do Zabbix inválido*");
    }
}

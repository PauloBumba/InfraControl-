using FluentAssertions;
using InfraControl.Application.Notifications.Rules;
using InfraControl.Application.Notifications.Rules.Ports;
using InfraControl.Domain.Entities;
using Moq;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// F42-T001: regras por tipo de evento — catálogo completo com default
/// ligado, upsert por EventType, validação de canal e evento desconhecido.
/// </summary>
public sealed class NotificationEventRulesCommandsTests
{
    [Fact]
    public async Task GetRules_WithoutPersistedRows_ShouldReturnCatalogWithDefaults()
    {
        var repository = new Mock<INotificationEventRuleRepository>();
        repository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NotificationEventRule>());

        var handler = new GetNotificationEventRulesQueryHandler(repository.Object);
        var result = await handler.Handle(
            new GetNotificationEventRulesQuery(), CancellationToken.None);

        result.Should().HaveCountGreaterThanOrEqualTo(13);
        result.Should().OnlyContain(r => r.Enabled);
        result.Should().OnlyContain(r => r.Channels.Count == 0);
        result.Should().Contain(r =>
            r.EventType == "IncidentCreatedDomainEvent" &&
            r.Category == "Operational");
    }

    [Fact]
    public async Task GetRules_WithPersistedRule_ShouldOverrideDefault()
    {
        var rule = NotificationEventRule.Create(
            "IncidentCreatedDomainEvent", enabled: false, channels: "Email");
        var repository = new Mock<INotificationEventRuleRepository>();
        repository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([rule]);

        var handler = new GetNotificationEventRulesQueryHandler(repository.Object);
        var result = await handler.Handle(
            new GetNotificationEventRulesQuery(), CancellationToken.None);

        var incident = result.Single(r => r.EventType == "IncidentCreatedDomainEvent");
        incident.Enabled.Should().BeFalse();
        incident.Channels.Should().ContainSingle().Which.Should().Be("Email");

        // os demais seguem o default (ligado, sem canais = fanout global)
        var alert = result.Single(r => r.EventType == "AlertCreatedDomainEvent");
        alert.Enabled.Should().BeTrue();
        alert.Channels.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveRules_WithNewRule_ShouldPersist()
    {
        var repository = new Mock<INotificationEventRuleRepository>();
        repository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NotificationEventRule>());
        repository.Setup(r => r.GetByEventTypeAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationEventRule?)null);
        NotificationEventRule? saved = null;
        repository.Setup(r => r.SaveAsync(
                It.IsAny<NotificationEventRule>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationEventRule, CancellationToken>((rule, _) => saved = rule)
            .Returns(Task.CompletedTask);

        var handler = new SaveNotificationEventRulesCommandHandler(repository.Object);
        await handler.Handle(
            new SaveNotificationEventRulesCommand(
            [
                new SaveNotificationEventRuleRequest(
                    "IncidentCreatedDomainEvent", Enabled: false,
                    Channels: ["Email", "SignalR"])
            ]),
            CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.EventType.Should().Be("IncidentCreatedDomainEvent");
        saved.Enabled.Should().BeFalse();
        saved.ChannelNames.Should().Equal("Email", "SignalR");
    }

    [Fact]
    public async Task SaveRules_WithUnknownEventType_ShouldThrow()
    {
        var repository = new Mock<INotificationEventRuleRepository>();
        var handler = new SaveNotificationEventRulesCommandHandler(repository.Object);

        var act = async () => await handler.Handle(
            new SaveNotificationEventRulesCommand(
            [
                new SaveNotificationEventRuleRequest(
                    "NaoExisteDomainEvent", true, null)
            ]),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*desconhecido*");
    }

    [Fact]
    public async Task SaveRules_WithInvalidChannel_ShouldThrow()
    {
        var repository = new Mock<INotificationEventRuleRepository>();
        var handler = new SaveNotificationEventRulesCommandHandler(repository.Object);

        var act = async () => await handler.Handle(
            new SaveNotificationEventRulesCommand(
            [
                new SaveNotificationEventRuleRequest(
                    "IncidentCreatedDomainEvent", true, ["CarrierPigeon"])
            ]),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Canal*inválido*");
    }

    [Fact]
    public void Rule_ChannelNames_ShouldParseCommaSeparated()
    {
        var rule = NotificationEventRule.Create(
            "X", enabled: true, channels: "Email, SignalR ,Telegram");

        rule.ChannelNames.Should().Equal("Email", "SignalR", "Telegram");
    }

    [Fact]
    public void Rule_EmptyChannels_ShouldReturnEmptyList()
    {
        var rule = NotificationEventRule.Create("X", enabled: true, channels: "");

        rule.ChannelNames.Should().BeEmpty();
    }
}

using FluentAssertions;
using InfraControl.Application.Alerts;
using InfraControl.Application.Alerts.Queries;
using InfraControl.Application.Monitoring.Checks;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Alerts;

/// <summary>
/// F6-T001: fluxo ponta-a-ponta dos alertas — avaliação pós-check no
/// pipeline real (AuthorizationBehavior por role), PostgreSQL do
/// Testcontainers e leitura pela query. ICMP falso sempre sobe.
/// </summary>
public sealed class AlertIntegrationTests : BaseIntegrationTest
{
    private static Alert SeedAlert(
        AppDbContext dbContext, Guid deviceId, AlertStatus status)
    {
        var alert = Alert.Create(
            deviceId, CheckKind.Ping, AlertSeverity.Critical,
            $"Device 'sw-it' está fora do ar (ping falhou).");
        if (status == AlertStatus.Resolved)
            alert.Resolve();

        dbContext.Alerts.Add(alert);
        dbContext.SaveChanges();
        return alert;
    }

    [Fact]
    public async Task OpenAlert_ShouldResolveAfterSuccessfulPingAndReadBackViaQuery()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.50");
        var alert = SeedAlert(dbContext, device.Id, AlertStatus.Open);
        // ADR-030: RunCheck é Operate (Viewer não executa check).
        UseRole(UserRole.Operator);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var run = await mediator.Send(new RunCheckCommand(device.Id, "Ping"));
        run.Succeeded.Should().BeTrue();

        var page = await mediator.Send(new SearchAlertsQuery());
        var item = page.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(alert.Id);
        item.Status.Should().Be(nameof(AlertStatus.Resolved));
        item.DeviceName.Should().Be(device.Name);
        item.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchAlerts_WithStatusFilter_ShouldReturnOnlyMatching()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.51");
        SeedAlert(dbContext, device.Id, AlertStatus.Open);
        SeedAlert(dbContext, device.Id, AlertStatus.Resolved);
        UseRole(UserRole.Admin);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var open = await mediator.Send(new SearchAlertsQuery(Status: "open"));
        open.Items.Should().ContainSingle(
            item => item.Status == nameof(AlertStatus.Open));

        var resolved = await mediator.Send(new SearchAlertsQuery(Status: "resolved"));
        resolved.Items.Should().ContainSingle(
            item => item.Status == nameof(AlertStatus.Resolved));
    }

    [Fact]
    public async Task SearchAlerts_WithoutRole_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        UseNoRoles();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(new SearchAlertsQuery());

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator, Viewer");
    }

    [Fact]
    public async Task SearchAlerts_WithInvalidStatus_ShouldThrowArgumentPtBr()
    {
        using var scope = CreateScope();
        UseRole(UserRole.Admin);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(new SearchAlertsQuery(Status: "banana"));

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*banana*open*resolved*");
    }
}

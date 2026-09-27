using FluentAssertions;
using InfraControl.Application.Alerts;
using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Alerts.Queries;
using InfraControl.Application.Incidents.Commands;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Alerts;

/// <summary>
/// F6-T004: supressão topológica ponta-a-ponta — DI real (repositório de
/// correlação por convenção), avaliação com upstream em Incident Open e
/// leitura do alerta Suppressed pela query no pipeline completo.
/// ICMP falso sobe → ping ok; o histórico seedado força o streak.
/// </summary>
public sealed class CorrelationIntegrationTests : BaseIntegrationTest
{
    private static async Task SeedFailuresAsync(
        AppDbContext context, Guid deviceId, int count)
    {
        for (var i = count; i >= 1; i--)
        {
            var row = MonitoringResult.Create(
                deviceId, CheckKind.Ping, succeeded: false, 5, "erro");
            context.MonitoringResults.Add(row);
            await context.SaveChangesAsync();
            // auditoria sobrescreve CreateDateTime — streak precisa de ordem
            var at = DateTime.UtcNow.AddHours(-i);
            await context.Database.ExecuteSqlAsync(
                $"UPDATE monitoring_results SET \"CreateDateTime\" = {at} WHERE id = {row.Id}");
        }
    }

    [Fact]
    public async Task DownstreamPingFailure_WithUpstreamIncident_ShouldSuppressLinkAndReadBack()
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var location = Domain.Entities.Location.Create("Sala Correlation");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var up = Domain.Entities.Device.Create(
            location.Id, "sw-core-it", DeviceType.Switch, ip: "192.0.2.80");
        var down = Domain.Entities.Device.Create(
            location.Id, "sw-edge-it", DeviceType.Switch, ip: "192.0.2.81");
        context.Devices.AddRange(up, down);
        await context.SaveChangesAsync();

        var nicUp = NetworkInterface.Create(up.Id, "Gi0/1");
        var nicDown = NetworkInterface.Create(down.Id, "Gi0/1");
        context.NetworkInterfaces.AddRange(nicUp, nicDown);
        await context.SaveChangesAsync();
        context.Connections.Add(Connection.Create(nicUp.Id, nicDown.Id));
        await context.SaveChangesAsync();

        var rootIncident = Incident.Create(
            "core fora do ar", IncidentSeverity.Critical, deviceId: up.Id);
        context.Incidents.Add(rootIncident);
        await context.SaveChangesAsync();
        await SeedFailuresAsync(context, down.Id, count: 2);

        var evaluator = scope.ServiceProvider.GetRequiredService<IAlertEvaluator>();
        await evaluator.EvaluateAsync(
            down, CheckKind.Ping,
            MonitoringResult.Create(
                down.Id, CheckKind.Ping, succeeded: false, 5, "erro"),
            CancellationToken.None);
        await context.SaveChangesAsync();

        UseRole(UserRole.Admin);
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var page = await mediator.Send(new SearchAlertsQuery(Status: "suppressed"));

        var item = page.Items.Should().ContainSingle().Subject;
        item.DeviceName.Should().Be(down.Name);
        item.Status.Should().Be(nameof(AlertStatus.Suppressed));
        item.Id.Should().NotBeEmpty();

        var links = await context.IncidentAlerts.AsNoTracking()
            .Where(link => link.IncidentId == rootIncident.Id)
            .ToListAsync();
        links.Should().ContainSingle(link => link.AlertId == item.Id,
            "alerta suprimido fica vinculado ao incidente raiz");

        var downIncidents = await context.Incidents.AsNoTracking()
            .Where(incident => incident.DeviceId == down.Id)
            .ToListAsync();
        downIncidents.Should().BeEmpty(
            "downstream não abre incidente próprio sob supressão");
    }

    [Fact]
    public async Task SearchAlerts_WithInvalidStatus_ShouldThrow400PtBrViaPipeline()
    {
        using var scope = CreateScope();
        UseRole(UserRole.Admin);
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var action = () => mediator.Send(new SearchAlertsQuery(Status: "nao-existe"));

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Status inválido*");
    }

    [Fact]
    public async Task UpstreamCleared_PromotesSuppressedAlertToOpenViaQuery()
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(context, ip: "192.0.2.82");
        var suppressed = Alert.Create(
            device.Id, CheckKind.Ping, AlertSeverity.Critical, "edge continua down");
        suppressed.Suppress();
        context.Alerts.Add(suppressed);
        await context.SaveChangesAsync();

        UseRole(UserRole.Viewer);
        var page = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new SearchAlertsQuery(Status: "suppressed"));

        page.Items.Should().ContainSingle(
            "o filtro enxerga o estado Suppressed persistido");
    }
}

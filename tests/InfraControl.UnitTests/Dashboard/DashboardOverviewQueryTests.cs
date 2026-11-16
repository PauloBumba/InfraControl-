using FluentAssertions;
using InfraControl.Application.Dashboard.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Dashboard;

/// <summary>
/// F54-T002 (docs/69): visão geral do dashboard — contagens globais em
/// uma única carga (sem N+1), precedência Offline > Degraded > Online >
/// Unknown com sinais locais, e separação de alertas/incidentes ativos.
/// </summary>
public sealed class DashboardOverviewQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public DashboardOverviewQueryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<(AppDbContext Context, GetDashboardOverviewQueryHandler Handler)> CreateAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return (context, new GetDashboardOverviewQueryHandler(
            new DeviceRepository(context),
            new AlertRepository(context),
            new IncidentRepository(context),
            new MonitoringResultRepository(context)));
    }

    [Fact]
    public async Task Overview_ShouldAggregateCountsWithoutCountingResolved()
    {
        var (context, handler) = await CreateAsync();
        var location = Location.Create("Sala Dashboard");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var sw = Device.Create(location.Id, "sw-1", DeviceType.Switch, ip: "192.0.2.10");
        var prn = Device.Create(location.Id, "prn-1", DeviceType.Printer, ip: "192.0.2.20");
        var srv = Device.Create(location.Id, "srv-1", DeviceType.Server, ip: "192.0.2.30");
        var cam = Device.Create(location.Id, "cam-1", DeviceType.Camera, ip: "192.0.2.40");
        context.Devices.AddRange(sw, prn, srv, cam);
        await context.SaveChangesAsync();

        // sw: ping ok, sem alerta → Online. Um alerta RESOLVIDO não conta.
        context.MonitoringResults.Add(
            MonitoringResult.Create(sw.Id, CheckKind.Ping, succeeded: true, 5));
        // srv: último ping falhou, sem alerta → Offline.
        context.MonitoringResults.Add(
            MonitoringResult.Create(srv.Id, CheckKind.Ping, succeeded: false, 5));
        // prn: alerta de aviso de toner, sem ping → Degraded + suprimento.
        // cam: sem ping e sem alerta → Unknown.
        var supplyAlert = Alert.Create(
            prn.Id, CheckKind.SnmpToner, AlertSeverity.Warning, "toner 12%");
        var resolvedAlert = Alert.Create(
            sw.Id, CheckKind.Ping, AlertSeverity.Critical, "já resolvido");
        resolvedAlert.Resolve();
        context.Alerts.AddRange(supplyAlert, resolvedAlert);

        var activeIncident = Incident.Create("Incidente ativo", IncidentSeverity.Medium);
        var resolvedIncident = Incident.Create("Incidente encerrado", IncidentSeverity.Low);
        resolvedIncident.ChangeStatus(IncidentStatus.Resolved);
        context.Incidents.AddRange(activeIncident, resolvedIncident);
        await context.SaveChangesAsync();

        var overview = await handler.Handle(
            new GetDashboardOverviewQuery(), CancellationToken.None);

        overview.Devices.Total.Should().Be(4);
        overview.Devices.ByType.Should().Equal(
            new Dictionary<string, int>
            {
                ["Switch"] = 1, ["Printer"] = 1, ["Server"] = 1, ["Camera"] = 1
            });
        overview.Devices.Online.Should().Be(1);
        overview.Devices.Degraded.Should().Be(1);
        overview.Devices.Offline.Should().Be(1);
        overview.Devices.Unknown.Should().Be(1);
        overview.Devices.WithAlerts.Should().Be(1);

        overview.Alerts.Open.Should().Be(1);
        overview.Alerts.Warning.Should().Be(1);
        overview.Alerts.Critical.Should().Be(0);

        overview.Incidents.Active.Should().Be(1);

        overview.Printers.Total.Should().Be(1);
        overview.Printers.WithSupplyAlert.Should().Be(1);
    }

    [Fact]
    public async Task Overview_CriticalAlertShouldWinOverSuccessfulPing()
    {
        var (context, handler) = await CreateAsync();
        var location = Location.Create("Sala Precedência");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var sw = Device.Create(location.Id, "sw-2", DeviceType.Switch, ip: "192.0.2.11");
        context.Devices.Add(sw);
        await context.SaveChangesAsync();

        context.MonitoringResults.Add(
            MonitoringResult.Create(sw.Id, CheckKind.Ping, succeeded: true, 5));
        context.Alerts.Add(Alert.Create(
            sw.Id, CheckKind.Ping, AlertSeverity.Critical, "ping falhou 5x"));
        await context.SaveChangesAsync();

        var overview = await handler.Handle(
            new GetDashboardOverviewQuery(), CancellationToken.None);

        overview.Devices.Offline.Should().Be(1);
        overview.Devices.Online.Should().Be(0);
        overview.Alerts.Critical.Should().Be(1);
    }

    [Fact]
    public async Task Overview_EmptyDatabase_ShouldReturnZeros()
    {
        var (context, handler) = await CreateAsync();

        var overview = await handler.Handle(
            new GetDashboardOverviewQuery(), CancellationToken.None);

        overview.Devices.Total.Should().Be(0);
        overview.Devices.ByType.Should().BeEmpty();
        overview.Devices.Online.Should().Be(0);
        overview.Devices.Offline.Should().Be(0);
        overview.Alerts.Open.Should().Be(0);
        overview.Incidents.Active.Should().Be(0);
        overview.Printers.Total.Should().Be(0);
        overview.GeneratedAtUtc.Should().NotBe(default);
    }
}

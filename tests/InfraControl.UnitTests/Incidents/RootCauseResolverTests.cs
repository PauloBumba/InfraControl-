using FluentAssertions;
using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Network.Connections.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using InfraControl.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Incidents;

/// <summary>
/// F6-T005 — Root Cause: causa provável pt-BR por heurística (upstream
/// com incidente → causa nele; vizinho caído ANTES → enlace do vizinho;
/// senão → falha local). Regra de tempo evita culpar vítimas.
/// </summary>
public sealed class RootCauseResolverTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public RootCauseResolverTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<AppDbContext> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static RootCauseResolver Resolver(AppDbContext context)
        => new(
            new ConnectionRepository(context),
            new AlertRepository(context),
            new DeviceRepository(context),
            new UpstreamCorrelationRepository(context),
            new IncidentRepository(context));

    private static async Task<Device> SeedDeviceAsync(
        AppDbContext context, string name)
    {
        var location = Location.Create("Sala RootCause");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(
            location.Id, name, DeviceType.Switch, ip: null);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    private static async Task ConnectAsync(
        AppDbContext context, Device from, Device to)
    {
        var nicFrom = NetworkInterface.Create(from.Id, $"eth-{from.Name}");
        var nicTo = NetworkInterface.Create(to.Id, $"eth-{to.Name}");
        context.NetworkInterfaces.AddRange(nicFrom, nicTo);
        await context.SaveChangesAsync();
        context.Connections.Add(Connection.Create(nicFrom.Id, nicTo.Id));
        await context.SaveChangesAsync();
    }

    private static Incident OpenIncident(AppDbContext context, Device device)
    {
        var incident = Incident.Create(
            "fora do ar", IncidentSeverity.Critical, deviceId: device.Id);
        context.Incidents.Add(incident);
        return incident;
    }

    // Auditoria sobrescreve CreateDateTime no SaveChanges — a regra de
    // tempo (F6-T005) precisa de datas fixadas via SQL (padrão do repo).
    // Tabela interpolada em string comum; valores como parâmetros ({0}).
    private static Task FixCreatedAtAsync(
        AppDbContext context, string table, Guid id, DateTime at)
        => context.Database.ExecuteSqlRawAsync(
            $"UPDATE {table} SET \"CreateDateTime\" = {{0}} WHERE id = {{1}}",
            at, id);

    [Fact]
    public async Task NoNeighbors_ShouldReturnLocalFailure()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context, "sw-alone");
        var incident = OpenIncident(context, device);
        await context.SaveChangesAsync();

        var cause = await Resolver(context).ResolveAsync(incident);

        cause.Should().Be("Falha local no device (energia, processo ou enlace direto).");
    }

    [Fact]
    public async Task UpstreamWithOpenIncident_ShouldPointToUpstream()
    {
        await using var context = await CreateContextAsync();
        var up = await SeedDeviceAsync(context, "sw-core");
        var down = await SeedDeviceAsync(context, "sw-edge");
        await ConnectAsync(context, up, down);
        context.Incidents.Add(
            Incident.Create("core down", IncidentSeverity.Critical, deviceId: up.Id));
        var incident = OpenIncident(context, down);
        await context.SaveChangesAsync();

        var cause = await Resolver(context).ResolveAsync(incident);

        cause.Should().Contain("upstream 'sw-core'",
            "BFS acha o incidente do upstream antes da regra de vizinho");
    }

    [Fact]
    public async Task NeighborOlderThanIncident_ShouldPointToNeighbor()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context, "sw-a");
        var neighbor = await SeedDeviceAsync(context, "sw-b");
        await ConnectAsync(context, device, neighbor);

        var alert = Alert.Create(
            neighbor.Id, CheckKind.Ping, AlertSeverity.Critical, "b down");
        context.Alerts.Add(alert);
        var incident = OpenIncident(context, device);
        await context.SaveChangesAsync();

        var alertAt = DateTime.UtcNow.AddMinutes(-30); // vizinho caiu antes
        var incidentAt = DateTime.UtcNow.AddMinutes(-10);
        await FixCreatedAtAsync(context, "alerts", alert.Id, alertAt);
        await FixCreatedAtAsync(context, "incidents", incident.Id, incidentAt);
        alert.CreateDateTime = alertAt;
        incident.CreateDateTime = incidentAt;

        var cause = await Resolver(context).ResolveAsync(incident);

        cause.Should().Contain("Vizinho 'sw-b' sem resposta");
        cause.Should().Contain("enlace ou switch");
    }

    [Fact]
    public async Task NeighborFellAfterIncident_ShouldStayLocalFailure()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context, "sw-core");
        var neighbor = await SeedDeviceAsync(context, "sw-edge");
        await ConnectAsync(context, device, neighbor);

        var incident = OpenIncident(context, device);
        var alert = Alert.Create(
            neighbor.Id, CheckKind.Ping, AlertSeverity.Critical, "edge down");
        context.Alerts.Add(alert);
        await context.SaveChangesAsync();

        var incidentAt = DateTime.UtcNow.AddMinutes(-30); // raiz caiu 1º
        var alertAt = DateTime.UtcNow.AddMinutes(-10); // vítima depois
        await FixCreatedAtAsync(context, "incidents", incident.Id, incidentAt);
        await FixCreatedAtAsync(context, "alerts", alert.Id, alertAt);
        incident.CreateDateTime = incidentAt;
        alert.CreateDateTime = alertAt;

        var cause = await Resolver(context).ResolveAsync(incident);

        cause.Should().Be("Falha local no device (energia, processo ou enlace direto).",
            "vítima caiu depois — não é a causa");
    }

    [Fact]
    public async Task SuppressedNeighborAlert_ShouldNotCount()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context, "sw-a");
        var neighbor = await SeedDeviceAsync(context, "sw-b");
        await ConnectAsync(context, device, neighbor);

        var alert = Alert.Create(
            neighbor.Id, CheckKind.Ping, AlertSeverity.Critical, "b down");
        alert.Suppress();
        context.Alerts.Add(alert);
        var incident = OpenIncident(context, device);
        await context.SaveChangesAsync();

        var alertAt = DateTime.UtcNow.AddMinutes(-30);
        var incidentAt = DateTime.UtcNow.AddMinutes(-10);
        await FixCreatedAtAsync(context, "alerts", alert.Id, alertAt);
        await FixCreatedAtAsync(context, "incidents", incident.Id, incidentAt);
        alert.CreateDateTime = alertAt;
        incident.CreateDateTime = incidentAt;

        var cause = await Resolver(context).ResolveAsync(incident);

        cause.Should().Be("Falha local no device (energia, processo ou enlace direto).",
            "Suppressed é vítima do mesmo root — não vira causa");
    }

    [Fact]
    public async Task IncidentWithoutDevice_ShouldReturnNull()
    {
        await using var context = await CreateContextAsync();
        var incident = Incident.Create("manual", IncidentSeverity.Low);
        await context.SaveChangesAsync();

        var cause = await Resolver(context).ResolveAsync(incident);

        cause.Should().BeNull("incidente manual sem device não tem causa derivada");
    }

    [Fact]
    public async Task RefreshAsync_ShouldUpdateOpenIncidentRootCause()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context, "sw-a");
        var incident = OpenIncident(context, device);
        await context.SaveChangesAsync();
        incident.RootCause.Should().BeNull();

        await Resolver(context).RefreshAsync(device.Id);

        var tracked = await context.Incidents
            .FirstAsync(i => i.Id == incident.Id);
        tracked.RootCause.Should().Contain("Falha local");
    }

    [Fact]
    public async Task RefreshAsync_WithoutIncident_ShouldNoOp()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context, "sw-clean");

        Func<Task> action = () => Resolver(context).RefreshAsync(device.Id);

        await action.Should().NotThrowAsync();
    }
}

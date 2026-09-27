using FluentAssertions;
using InfraControl.Application.Alerts.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Alerts;

/// <summary>
/// F6-T004 — Correlation: BFS não-dirigido pelas Connections até o
/// primeiro (mais próximo) device com Incident Open; si-mesmo excluído;
/// sem caminho ou sem incidente Open → null.
/// </summary>
public sealed class UpstreamCorrelationRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public UpstreamCorrelationRepositoryTests()
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

    private static UpstreamCorrelationRepository Repo(AppDbContext context)
        => new(context);

    private static async Task<Device> SeedDeviceAsync(
        AppDbContext context, string name)
    {
        var location = Location.Create("Sala Correlation");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(
            location.Id, name, DeviceType.Switch, ip: null);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    /// <summary>Aresta dirigida a→b (nic de cada lado) — grafo é não-dirigido.</summary>
    private static async Task ConnectAsync(
        AppDbContext context, Device from, Device to, bool reverse = false)
    {
        var nicFrom = NetworkInterface.Create(from.Id, $"eth-{from.Name}");
        var nicTo = NetworkInterface.Create(to.Id, $"eth-{to.Name}");
        context.NetworkInterfaces.AddRange(nicFrom, nicTo);
        await context.SaveChangesAsync();

        context.Connections.Add(reverse
            ? Connection.Create(nicTo.Id, nicFrom.Id)
            : Connection.Create(nicFrom.Id, nicTo.Id));
        await context.SaveChangesAsync();
    }

    private static async Task<Incident> OpenIncidentAsync(
        AppDbContext context, Device device)
    {
        var incident = Incident.Create(
            "raiz", IncidentSeverity.Critical, deviceId: device.Id);
        context.Incidents.Add(incident);
        await context.SaveChangesAsync();
        return incident;
    }

    [Fact]
    public async Task NeighborWithOpenIncident_ShouldReturnIt()
    {
        await using var context = await CreateContextAsync();
        var up = await SeedDeviceAsync(context, "sw-up");
        var down = await SeedDeviceAsync(context, "sw-down");
        await ConnectAsync(context, up, down);
        var incident = await OpenIncidentAsync(context, up);

        var result = await Repo(context)
            .FindOpenIncidentUpstreamAsync(down.Id);

        result.Should().NotBeNull();
        result!.DeviceId.Should().Be(up.Id);
        result.IncidentId.Should().Be(incident.Id);
    }

    [Fact]
    public async Task TransitiveTwoHops_ShouldReachRootIncident()
    {
        await using var context = await CreateContextAsync();
        var root = await SeedDeviceAsync(context, "sw-root");
        var middle = await SeedDeviceAsync(context, "sw-mid");
        var leaf = await SeedDeviceAsync(context, "sw-leaf");
        await ConnectAsync(context, root, middle);
        await ConnectAsync(context, middle, leaf);
        var incident = await OpenIncidentAsync(context, root);

        var result = await Repo(context)
            .FindOpenIncidentUpstreamAsync(leaf.Id);

        result.Should().NotBeNull("BFS é transitive pelas conexões");
        result!.DeviceId.Should().Be(root.Id);
        result.IncidentId.Should().Be(incident.Id);
    }

    [Fact]
    public async Task SelfIncidentOnly_ShouldReturnNull()
    {
        await using var context = await CreateContextAsync();
        var up = await SeedDeviceAsync(context, "sw-a");
        var down = await SeedDeviceAsync(context, "sw-b");
        await ConnectAsync(context, up, down);
        // incidente só no próprio device consultado → não é upstream
        await OpenIncidentAsync(context, down);

        var result = await Repo(context)
            .FindOpenIncidentUpstreamAsync(down.Id);

        result.Should().BeNull("si-mesmo excluído do BFS");
    }

    [Fact]
    public async Task ResolvedIncident_ShouldBeIgnored()
    {
        await using var context = await CreateContextAsync();
        var up = await SeedDeviceAsync(context, "sw-up");
        var down = await SeedDeviceAsync(context, "sw-down");
        await ConnectAsync(context, up, down);
        var incident = await OpenIncidentAsync(context, up);
        incident.ChangeStatus(IncidentStatus.Resolved);
        await context.SaveChangesAsync();

        var result = await Repo(context)
            .FindOpenIncidentUpstreamAsync(down.Id);

        result.Should().BeNull("só Incident Open suprime");
    }

    [Fact]
    public async Task NoPath_ShouldReturnNull()
    {
        await using var context = await CreateContextAsync();
        var up = await SeedDeviceAsync(context, "sw-up");
        var down = await SeedDeviceAsync(context, "sw-down");
        await OpenIncidentAsync(context, up); // sem conexão entre eles

        var result = await Repo(context)
            .FindOpenIncidentUpstreamAsync(down.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ReversedEdgeDirection_ShouldStillTraverse()
    {
        await using var context = await CreateContextAsync();
        var up = await SeedDeviceAsync(context, "sw-up");
        var down = await SeedDeviceAsync(context, "sw-down");
        // aresta "ao contrário": down → up
        await ConnectAsync(context, down, up, reverse: false);
        var incident = await OpenIncidentAsync(context, up);

        var result = await Repo(context)
            .FindOpenIncidentUpstreamAsync(down.Id);

        result.Should().NotBeNull("grafo é não-dirigido — direção da aresta não importa");
        result!.DeviceId.Should().Be(up.Id);
        result.IncidentId.Should().Be(incident.Id);
    }

    [Fact]
    public async Task NearestRoot_ShouldWinOverFartherOne()
    {
        await using var context = await CreateContextAsync();
        var far = await SeedDeviceAsync(context, "sw-far");
        var near = await SeedDeviceAsync(context, "sw-near");
        var mid = await SeedDeviceAsync(context, "sw-mid");
        var target = await SeedDeviceAsync(context, "sw-target");
        await ConnectAsync(context, far, mid);
        await ConnectAsync(context, mid, near);
        await ConnectAsync(context, near, target);
        var farIncident = await OpenIncidentAsync(context, far);
        var nearIncident = await OpenIncidentAsync(context, near);

        var result = await Repo(context)
            .FindOpenIncidentUpstreamAsync(target.Id);

        result.Should().NotBeNull();
        result!.IncidentId.Should().Be(nearIncident.Id,
            "BFS retorna o incidente mais próximo (1 hop, não 3)");
        result.IncidentId.Should().NotBe(farIncident.Id);
    }

    [Fact]
    public async Task NoConnections_ShouldReturnNull()
    {
        await using var context = await CreateContextAsync();
        var lonely = await SeedDeviceAsync(context, "sw-lonely");

        var result = await Repo(context)
            .FindOpenIncidentUpstreamAsync(lonely.Id);

        result.Should().BeNull();
    }
}


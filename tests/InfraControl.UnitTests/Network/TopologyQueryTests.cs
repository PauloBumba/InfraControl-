using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Network.Topology;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// F3-T006: query GetTopology (só backend — decisões do usuário: nodes =
/// INTERFACES incluindo isoladas, status = último Ping do histórico,
/// edges = Connections do F3-T005).
/// </summary>
public sealed class TopologyQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TopologyQueryTests()
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

    private static GetTopologyQueryHandler Handler(AppDbContext context)
        => new(new TopologyRepository(context));

    private static async Task<Guid> SeedDeviceAsync(
        AppDbContext context,
        string name,
        string? ip = null,
        DeviceType type = DeviceType.Switch)
    {
        var location = await context.Locations.FirstOrDefaultAsync();
        if (location is null)
        {
            location = Location.Create("Data Center A");
            context.Locations.Add(location);
            await context.SaveChangesAsync();
        }

        var device = Device.Create(location.Id, name, type, ip: ip);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device.Id;
    }

    private static async Task<Guid> SeedInterfaceAsync(
        AppDbContext context, Guid deviceId, string name)
    {
        var nic = NetworkInterface.Create(deviceId, name);
        context.NetworkInterfaces.Add(nic);
        await context.SaveChangesAsync();
        return nic.Id;
    }

    private static async Task SeedPingAsync(
        AppDbContext context, Guid deviceId, bool succeeded, DateTime at)
    {
        var row = MonitoringResult.Create(deviceId, CheckKind.Ping, succeeded, 5);
        context.MonitoringResults.Add(row);
        await context.SaveChangesAsync();

        // SaveChangesAsync sobrescreve CreateDateTime (auditoria) — o "mais
        // recente" precisa de data explícita, então ajusta via SQL.
        await context.Database.ExecuteSqlAsync(
            $"UPDATE monitoring_results SET \"CreateDateTime\" = {at} WHERE id = {row.Id}");
    }

    [Fact]
    public void Query_ShouldRequireAnyRole()
        => new GetTopologyQuery().RequiredRoles.Should()
            .BeEquivalentTo(RoleAccess.Read);

    [Fact]
    public async Task Graph_WithConnection_ShouldReturnNodesAndEdge()
    {
        await using var context = await CreateContextAsync();
        var sw = await SeedDeviceAsync(context, "sw-01", "192.0.2.10");
        var srv = await SeedDeviceAsync(context, "srv-01", "192.0.2.20", DeviceType.Server);
        var swNic = await SeedInterfaceAsync(context, sw, "Gi0/1");
        var srvNic = await SeedInterfaceAsync(context, srv, "eth0");
        context.Connections.Add(Connection.Create(swNic, srvNic));
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(
            new GetTopologyQuery(), CancellationToken.None);

        result.Nodes.Should().HaveCount(2);
        var node = result.Nodes.Single(n => n.Id == swNic);
        node.DeviceId.Should().Be(sw);
        node.Name.Should().Be("Gi0/1");
        node.DeviceName.Should().Be("sw-01");
        node.DeviceType.Should().Be(nameof(DeviceType.Switch));
        node.Ip.Should().Be("192.0.2.10");

        var edge = result.Edges.Should().ContainSingle().Subject;
        edge.SourceInterfaceId.Should().Be(swNic);
        edge.TargetInterfaceId.Should().Be(srvNic);
    }

    [Fact]
    public async Task Graph_InterfaceWithoutConnection_ShouldAppearIsolated()
    {
        await using var context = await CreateContextAsync();
        var sw = await SeedDeviceAsync(context, "sw-iso");
        var nic = await SeedInterfaceAsync(context, sw, "Gi0/9");

        var result = await Handler(context).Handle(
            new GetTopologyQuery(), CancellationToken.None);

        result.Nodes.Should().ContainSingle(n => n.Id == nic);
        result.Edges.Should().BeEmpty(
            "interface sem Connection é node isolado, não some do mapa");
    }

    [Fact]
    public async Task Graph_ShouldInheritLatestPingStatusFromDevice()
    {
        await using var context = await CreateContextAsync();
        var up = await SeedDeviceAsync(context, "sw-up");
        var down = await SeedDeviceAsync(context, "sw-down");
        var unknown = await SeedDeviceAsync(context, "sw-new");
        var upNic = await SeedInterfaceAsync(context, up, "eth0");
        var downNic = await SeedInterfaceAsync(context, down, "eth0");
        var unknownNic = await SeedInterfaceAsync(context, unknown, "eth0");

        var day = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
        // up: falha antiga + sucesso recente → "up" (mais recente vence)
        await SeedPingAsync(context, up, succeeded: false, at: day.AddHours(-2));
        await SeedPingAsync(context, up, succeeded: true, at: day.AddHours(-1));
        await SeedPingAsync(context, down, succeeded: false, at: day);

        var result = await Handler(context).Handle(
            new GetTopologyQuery(), CancellationToken.None);

        result.Nodes.Single(n => n.Id == upNic).Status.Should().Be("up");
        result.Nodes.Single(n => n.Id == downNic).Status.Should().Be("down");
        result.Nodes.Single(n => n.Id == unknownNic).Status.Should().BeNull(
            "device nunca verificado fica sem status");
    }

    [Fact]
    public async Task Graph_WithoutInterfaces_ShouldReturnEmpty()
    {
        await using var context = await CreateContextAsync();
        _ = await SeedDeviceAsync(context, "sw-vazio");

        var result = await Handler(context).Handle(
            new GetTopologyQuery(), CancellationToken.None);

        result.Nodes.Should().BeEmpty();
        result.Edges.Should().BeEmpty();
    }

    [Fact]
    public async Task Graph_ShouldOrderEdgesAndExcludeUnlinkedDevicesWithoutInterfaces()
    {
        await using var context = await CreateContextAsync();
        var sw = await SeedDeviceAsync(context, "sw-01");
        var a = await SeedInterfaceAsync(context, sw, "Gi0/2");
        var b = await SeedInterfaceAsync(context, sw, "Gi0/1");
        context.Connections.Add(Connection.Create(b, a));
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(
            new GetTopologyQuery(), CancellationToken.None);

        // Device sem interface não gera node; nodes ordenados por device+nome.
        result.Nodes.Select(n => n.Name).Should().Equal("Gi0/1", "Gi0/2");
        result.Edges.Should().ContainSingle();
    }

    private static async Task<Guid> SeedRunAsync(AppDbContext context, Guid deviceId)
    {
        var run = DiscoveryRun.Start(deviceId);
        context.DiscoveryRuns.Add(run);
        await context.SaveChangesAsync();
        return run.Id;
    }

    private static async Task<Guid> SeedNeighborAsync(
        AppDbContext context,
        Guid runId,
        Guid? linkedDeviceId,
        string? localIfName,
        string? sysName = null,
        string? portDesc = null)
    {
        var values = new Dictionary<string, string>();
        if (localIfName is not null)
            values["local_if_name"] = localIfName;
        if (sysName is not null)
            values["sys_name"] = sysName;
        if (portDesc is not null)
            values["port_desc"] = portDesc;

        var finding = DiscoveryResult.Create(
            runId,
            DiscoveryResultKind.Neighbor,
            sysName ?? "Vizinho",
            values,
            deviceId: linkedDeviceId);
        context.DiscoveryResults.Add(finding);
        await context.SaveChangesAsync();
        return finding.Id;
    }

    [Fact]
    public async Task Graph_GhostNeighbor_ShouldAppearAsGhostNodeWithPortEdge()
    {
        await using var context = await CreateContextAsync();
        var sw = await SeedDeviceAsync(context, "sw-01");
        var nic = await SeedInterfaceAsync(context, sw, "Gi0/1");
        var run = await SeedRunAsync(context, sw);
        var findingId = await SeedNeighborAsync(
            context, run, linkedDeviceId: null, localIfName: "Gi0/1",
            sysName: "sw-vizinho", portDesc: "Gi0/8");

        var result = await Handler(context).Handle(
            new GetTopologyQuery(), CancellationToken.None);

        var ghost = result.Nodes.Should().ContainSingle(n => n.IsGhost).Subject;
        ghost.Id.Should().Be(findingId);
        ghost.Name.Should().Be("sw-vizinho");
        ghost.DeviceName.Should().Be("sw-vizinho");
        ghost.DeviceType.Should().Be("Unknown");
        ghost.Port.Should().Be("Gi0/8");
        ghost.Ip.Should().BeNull();
        ghost.Status.Should().BeNull();

        var edge = result.Edges.Should().ContainSingle(e => e.IsGhost).Subject;
        edge.SourceInterfaceId.Should().Be(nic);
        edge.TargetInterfaceId.Should().Be(findingId);
    }

    [Fact]
    public async Task Graph_MatchedNeighbor_ShouldNotBecomeGhost()
    {
        await using var context = await CreateContextAsync();
        var sw = await SeedDeviceAsync(context, "sw-01");
        _ = await SeedInterfaceAsync(context, sw, "Gi0/1");
        var run = await SeedRunAsync(context, sw);

        // Vizinho já vinculado a um device gerenciado (DeviceId != null) →
        // NÃO é ghost (caminho normal de Connection, não do read-model ghost).
        var peer = await SeedDeviceAsync(context, "sw-peer");
        _ = await SeedNeighborAsync(
            context, run, linkedDeviceId: peer, localIfName: "Gi0/1",
            sysName: "sw-peer", portDesc: "Gi0/2");

        var result = await Handler(context).Handle(
            new GetTopologyQuery(), CancellationToken.None);

        result.Nodes.Should().NotContain(n => n.IsGhost);
        result.Edges.Should().NotContain(e => e.IsGhost);
    }

    [Fact]
    public async Task Graph_DuplicateGhostFindings_ShouldDedupeByKey()
    {
        await using var context = await CreateContextAsync();
        var sw = await SeedDeviceAsync(context, "sw-01");
        _ = await SeedInterfaceAsync(context, sw, "Gi0/1");
        var run = await SeedRunAsync(context, sw);

        // Mesmo vizinho descoberto em dois runs → um único nó ghost.
        _ = await SeedNeighborAsync(
            context, run, null, "Gi0/1", sysName: "sw-vizinho", portDesc: "Gi0/8");
        var run2 = await SeedRunAsync(context, sw);
        _ = await SeedNeighborAsync(
            context, run2, null, "Gi0/1", sysName: "sw-vizinho", portDesc: "Gi0/8");

        var result = await Handler(context).Handle(
            new GetTopologyQuery(), CancellationToken.None);

        result.Nodes.Count(n => n.IsGhost).Should().Be(1);
        result.Edges.Count(e => e.IsGhost).Should().Be(1);
    }

    [Fact]
    public async Task Graph_GhostWithUnknownLocalInterface_ShouldBeSkipped()
    {
        await using var context = await CreateContextAsync();
        var sw = await SeedDeviceAsync(context, "sw-01");
        _ = await SeedInterfaceAsync(context, sw, "Gi0/1");
        var run = await SeedRunAsync(context, sw);

        // local_if_name não casa nenhuma interface do device → sem ghost.
        _ = await SeedNeighborAsync(
            context, run, null, "Gi9/9", sysName: "sw-vizinho", portDesc: "Gi0/8");

        var result = await Handler(context).Handle(
            new GetTopologyQuery(), CancellationToken.None);

        result.Nodes.Should().NotContain(n => n.IsGhost);
        result.Edges.Should().NotContain(e => e.IsGhost);
    }
}

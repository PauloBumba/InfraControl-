using FluentAssertions;
using InfraControl.Application.Network.Explorer;
using InfraControl.Application.Network.Switches.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// Network Explorer F2 — `GET /network/explorer/switches/{id}`: portas com
/// VLANs associadas + MACs aprendidos via SNMP (MacTableEntry do próprio
/// switch) enriquecidos com ARP/DHCP. Guarda de tipo Switch herdada do
/// GetSwitchQuery.
/// </summary>
public sealed class SwitchExplorerQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SwitchExplorerQueryTests()
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

    private static async Task<(Guid LocationId, Guid SwitchId)> SeedSwitchAsync(
        AppDbContext context, string name = "sw-core")
    {
        var location = Location.Create("Sala");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, name, DeviceType.Switch, ip: "192.168.1.2");
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return (location.Id, device.Id);
    }

    private static async Task<Guid> SeedRunAsync(AppDbContext context, Guid deviceId)
    {
        var run = DiscoveryRun.Start(deviceId);
        run.Complete();
        context.DiscoveryRuns.Add(run);
        await context.SaveChangesAsync();
        return run.Id;
    }

    private static async Task SeedResultAsync(
        AppDbContext context, Guid runId, DiscoveryResultKind kind,
        IReadOnlyDictionary<string, string>? values, Guid? deviceId = null,
        DateTime? createdAt = null)
    {
        context.DiscoveryResults.Add(
            DiscoveryResult.Create(runId, kind, "achado", values, deviceId, createdAt));
        await context.SaveChangesAsync();
    }

    private static GetSwitchExplorerQueryHandler Handler(AppDbContext context)
        => new(new GetSwitchQueryHandler(new DeviceRepository(context)),
            new SwitchPortRepository(context),
            new SwitchPortVlanRepository(context),
            new VlanRepository(context),
            new DiscoveryResultRepository(context));

    [Fact]
    public async Task Explorer_ShouldReturnPortsWithAssociatedVlans()
    {
        await using var context = await CreateContextAsync();
        var (_, switchId) = await SeedSwitchAsync(context);

        var vlan10 = Vlan.Create("Usuarios", 10);
        var vlan20 = Vlan.Create("Servidores", 20);
        context.Vlans.AddRange(vlan10, vlan20);
        var port1 = SwitchPort.Create(switchId, "Gi1/0/1");
        var port2 = SwitchPort.Create(switchId, "Gi1/0/2");
        context.SwitchPorts.AddRange(port1, port2);
        context.SwitchPortVlans.Add(SwitchPortVlan.Create(port1.Id, vlan20.Id));
        context.SwitchPortVlans.Add(SwitchPortVlan.Create(port1.Id, vlan10.Id));
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(
            new GetSwitchExplorerQuery(switchId), CancellationToken.None);

        result.Name.Should().Be("sw-core");
        result.Ip.Should().Be("192.168.1.2");
        result.Ports.Should().HaveCount(2);
        var p1 = result.Ports.Single(p => p.Name == "Gi1/0/1");
        p1.Vlans.Select(v => v.Number).Should().Equal([10, 20], "ordenadas por número");
        result.Ports.Single(p => p.Name == "Gi1/0/2").Vlans.Should().BeEmpty();
    }

    [Fact]
    public async Task Explorer_ShouldReturnLearnedMacsEnrichedByDhcp()
    {
        await using var context = await CreateContextAsync();
        var (_, switchId) = await SeedSwitchAsync(context);
        var (_, otherSwitchId) = await SeedSwitchAsync(context, "sw-edge");
        var runId = await SeedRunAsync(context, switchId);
        var otherRunId = await SeedRunAsync(context, otherSwitchId);

        await SeedResultAsync(context, runId, DiscoveryResultKind.MacTableEntry,
            new Dictionary<string, string>
            {
                ["mac"] = "AA:BB:CC:DD:EE:01",
                ["bridge_port"] = "Gi1/0/5",
                ["fdb_status"] = "3",
            }, deviceId: switchId);
        await SeedResultAsync(context, runId, DiscoveryResultKind.DhcpLease,
            new Dictionary<string, string>
            {
                ["mac"] = "aa:bb:cc:dd:ee:01",
                ["ip"] = "192.0.2.10",
                ["hostname"] = "estacao",
                ["vlan"] = "10",
            });
        // MAC aprendido por OUTRO switch não pode aparecer aqui.
        await SeedResultAsync(context, otherRunId, DiscoveryResultKind.MacTableEntry,
            new Dictionary<string, string>
            {
                ["mac"] = "11:22:33:44:55:66",
                ["bridge_port"] = "Gi0/1",
                ["fdb_status"] = "3",
            }, deviceId: otherSwitchId);

        var result = await Handler(context).Handle(
            new GetSwitchExplorerQuery(switchId), CancellationToken.None);

        result.LearnedMacs.Should().ContainSingle();
        var mac = result.LearnedMacs[0];
        mac.Mac.Should().Be("AA:BB:CC:DD:EE:01");
        mac.Ip.Should().Be("192.0.2.10");
        mac.Hostname.Should().Be("estacao");
        mac.Vlan.Should().Be("10");
        mac.BridgePort.Should().Be("Gi1/0/5");
    }

    [Fact]
    public async Task Explorer_ShouldMatchBridgePortToRegisteredPortName()
    {
        await using var context = await CreateContextAsync();
        var (_, switchId) = await SeedSwitchAsync(context);
        context.SwitchPorts.Add(SwitchPort.Create(switchId, "Gi1/0/5"));
        await context.SaveChangesAsync();
        var runId = await SeedRunAsync(context, switchId);
        await SeedResultAsync(context, runId, DiscoveryResultKind.MacTableEntry,
            new Dictionary<string, string>
            {
                ["mac"] = "AA:BB:CC:DD:EE:02",
                ["bridge_port"] = "gi1/0/5",
                ["fdb_status"] = "3",
            }, deviceId: switchId);

        var result = await Handler(context).Handle(
            new GetSwitchExplorerQuery(switchId), CancellationToken.None);

        result.LearnedMacs.Should().ContainSingle();
        result.LearnedMacs[0].PortName.Should().Be("Gi1/0/5", "case-insensitive");
    }

    [Fact]
    public async Task Explorer_NonSwitchDevice_ShouldThrowNotFound()
    {
        await using var context = await CreateContextAsync();
        var location = Location.Create("Sala");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        var server = Device.Create(location.Id, "srv-01", DeviceType.Server);
        context.Devices.Add(server);
        await context.SaveChangesAsync();

        var act = async () => await Handler(context).Handle(
            new GetSwitchExplorerQuery(server.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("*não encontrado*");
    }
}

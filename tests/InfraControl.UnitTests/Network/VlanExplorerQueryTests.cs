using FluentAssertions;
using InfraControl.Application.Network.Explorer;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// Network Explorer F2 — `GET /network/explorer/vlans`: VLANs cadastradas
/// enriquecidas com switches/portas associados e dispositivos descobertos
/// (leases DHCP), mais VLANs vistas em DHCP mas NÃO cadastradas.
/// </summary>
public sealed class VlanExplorerQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public VlanExplorerQueryTests()
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

    private static async Task<Guid> SeedSwitchAsync(AppDbContext context, string name)
    {
        var location = Location.Create("Sala");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        var device = Device.Create(location.Id, name, DeviceType.Switch);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device.Id;
    }

    private static async Task<Guid> SeedRunAsync(AppDbContext context, Guid deviceId)
    {
        var run = DiscoveryRun.Start(deviceId);
        run.Complete();
        context.DiscoveryRuns.Add(run);
        await context.SaveChangesAsync();
        return run.Id;
    }

    private static async Task SeedLeaseAsync(
        AppDbContext context, Guid runId, string vlan, string mac, string ip,
        DateTime? createdAt = null)
    {
        context.DiscoveryResults.Add(DiscoveryResult.Create(
            runId, DiscoveryResultKind.DhcpLease, "lease",
            new Dictionary<string, string>
            {
                ["vlan"] = vlan, ["mac"] = mac, ["ip"] = ip,
            },
            createdAt: createdAt));
        await context.SaveChangesAsync();
    }

    private static SearchVlansExplorerQueryHandler Handler(AppDbContext context)
        => new(new VlanRepository(context),
            new SwitchPortVlanRepository(context),
            new SwitchPortRepository(context),
            new DeviceRepository(context),
            new DiscoveryResultRepository(context));

    [Fact]
    public async Task Explorer_ShouldEnrichRegisteredVlanWithSwitchesAndDiscoveredDevices()
    {
        await using var context = await CreateContextAsync();
        var sw1 = await SeedSwitchAsync(context, "sw-core");
        var sw2 = await SeedSwitchAsync(context, "sw-edge");
        var vlan = Vlan.Create("Usuarios", 10);
        context.Vlans.Add(vlan);
        var p1 = SwitchPort.Create(sw1, "Gi1/0/1");
        var p2 = SwitchPort.Create(sw2, "Gi0/2");
        var p3 = SwitchPort.Create(sw2, "Gi0/3");
        context.SwitchPorts.AddRange(p1, p2, p3);
        context.SwitchPortVlans.Add(SwitchPortVlan.Create(p1.Id, vlan.Id));
        context.SwitchPortVlans.Add(SwitchPortVlan.Create(p2.Id, vlan.Id));
        context.SwitchPortVlans.Add(SwitchPortVlan.Create(p3.Id, vlan.Id));
        var runId = await SeedRunAsync(context, sw1);
        await SeedLeaseAsync(context, runId, "10", "AA:BB:CC:00:00:01", "192.0.2.10");
        await SeedLeaseAsync(context, runId, "10", "AA:BB:CC:00:00:02", "192.0.2.11");
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(
            new SearchVlansExplorerQuery(), CancellationToken.None);

        result.Items.Should().ContainSingle();
        var item = result.Items[0];
        item.IsRegistered.Should().BeTrue();
        item.Name.Should().Be("Usuarios");
        item.PortCount.Should().Be(3);
        item.DiscoveredDeviceCount.Should().Be(2, "2 MACs distintos em leases DHCP");
        item.LastSeenAt.Should().NotBeNull();
        item.Switches.Should().HaveCount(2);
        item.Switches.Should().Contain(s => s.DeviceName == "sw-core" && s.PortCount == 1);
        item.Switches.Should().Contain(s => s.DeviceName == "sw-edge" && s.PortCount == 2);
    }

    [Fact]
    public async Task Explorer_ShouldListDhcpVlansNotRegistered()
    {
        await using var context = await CreateContextAsync();
        var sw = await SeedSwitchAsync(context, "sw-core");
        context.Vlans.Add(Vlan.Create("Usuarios", 10));
        await context.SaveChangesAsync();
        var runId = await SeedRunAsync(context, sw);
        await SeedLeaseAsync(context, runId, "30", "AA:BB:CC:00:00:09", "192.0.2.30");
        await SeedLeaseAsync(context, runId, "30", "AA:BB:CC:00:00:08", "192.0.2.31");

        var result = await Handler(context).Handle(
            new SearchVlansExplorerQuery(), CancellationToken.None);

        result.DiscoveredVlans.Should().ContainSingle();
        var discovered = result.DiscoveredVlans[0];
        discovered.IsRegistered.Should().BeFalse();
        discovered.VlanNumber.Should().Be(30);
        discovered.Name.Should().BeNull();
        discovered.DiscoveredDeviceCount.Should().Be(2);
        result.Items.Should().ContainSingle(i => i.VlanNumber == 10);
    }

    [Fact]
    public async Task Explorer_RegisteredVlanWithoutDiscovery_ShouldShowZeroCounts()
    {
        await using var context = await CreateContextAsync();
        context.Vlans.Add(Vlan.Create("Telefonia", 50));
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(
            new SearchVlansExplorerQuery(), CancellationToken.None);

        var item = result.Items.Should().ContainSingle().Subject;
        item.PortCount.Should().Be(0);
        item.DiscoveredDeviceCount.Should().Be(0);
        item.LastSeenAt.Should().BeNull();
        item.Switches.Should().BeEmpty();
    }
}

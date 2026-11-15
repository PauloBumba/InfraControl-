using FluentAssertions;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Network.Explorer;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Moq;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// Network Explorer F3 — `GET /network/explorer/aps`: APs do inventário +
/// APs/ACs vistos no UniFi sem cadastro, com telemetria ao vivo opcional,
/// clientes Wi-Fi por AP, uplink via LLDP e último discovery run.
/// </summary>
public sealed class ApExplorerQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ApExplorerQueryTests()
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

    private static async Task<(Guid LocationId, Guid ApId)> SeedApAsync(
        AppDbContext context, string name = "AP-Sala", string? mac = "aa:bb:cc:00:00:02",
        Guid? vlanId = null)
    {
        var location = Location.Create("Patio");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        var ap = Device.Create(location.Id, name, DeviceType.AccessPoint,
            ip: "192.168.1.10", mac: mac, vlanId: vlanId);
        context.Devices.Add(ap);
        await context.SaveChangesAsync();
        return (location.Id, ap.Id);
    }

    private static SearchApExplorerQueryHandler Handler(
        AppDbContext context, Mock<IUnifiApiClient>? unifi = null)
        => new(new DeviceRepository(context),
            new LocationRepository(context),
            new VlanRepository(context),
            new DiscoveryResultRepository(context),
            new DiscoveryRunRepository(context),
            (unifi ?? new Mock<IUnifiApiClient>()).Object);

    [Fact]
    public async Task Explorer_ShouldListInventoryApWithLocationAndVlan()
    {
        await using var context = await CreateContextAsync();
        var vlan = Vlan.Create("WiFi", 20);
        context.Vlans.Add(vlan);
        await context.SaveChangesAsync();
        await SeedApAsync(context, vlanId: vlan.Id);

        var result = await Handler(context).Handle(
            new SearchApExplorerQuery(), CancellationToken.None);

        var item = result.Items.Should().ContainSingle().Subject;
        item.Name.Should().Be("AP-Sala");
        item.Kind.Should().Be("AccessPoint");
        item.IsRegistered.Should().BeTrue();
        item.LocationName.Should().Be("Patio");
        item.ManagementVlan.Should().Be("WiFi");
    }

    [Fact]
    public async Task Explorer_ShouldEnrichApWithUnifiClientsAndTelemetry()
    {
        await using var context = await CreateContextAsync();
        await SeedApAsync(context, mac: "aa:bb:cc:00:00:02");

        var unifi = new Mock<IUnifiApiClient>();
        unifi.Setup(u => u.ListDevicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UnifiDeviceDto>());
        unifi.Setup(u => u.ListAccessPointsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UnifiAccessPointDto>
            {
                new("AA:BB:CC:00:00:02", "AP-Sala", "U6 Pro", "192.168.1.10",
                    true, 3600, 10, 20, 0, 0, 3, []),
            });
        unifi.Setup(u => u.ListConnectedClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UnifiConnectedClientDto>
            {
                new("ff:ee:dd:cc:bb:01", "192.168.1.50", "laptop", "AA:BB:CC:00:00:02",
                    null, "Escritorio", null, -50, false, null, "ng"),
                new("ff:ee:dd:cc:bb:02", "192.168.1.51", "phone", "AA:BB:CC:00:00:02",
                    null, "Escritorio", null, -60, false, null, "ng"),
            });

        var result = await Handler(context, unifi).Handle(
            new SearchApExplorerQuery(), CancellationToken.None);

        var item = result.Items.Should().ContainSingle().Subject;
        item.UniFiConnected.Should().BeTrue();
        item.ClientCount.Should().Be(3, "o contador vem do AP UniFi; só 2 entram na lista detalhada");
        item.Ssids.Should().Equal("Escritorio");
        item.Clients.Should().HaveCount(2);
    }

    [Fact]
    public async Task Explorer_ShouldListUnifiApWithoutInventoryAsUnregistered()
    {
        await using var context = await CreateContextAsync();
        var unifi = new Mock<IUnifiApiClient>();
        unifi.Setup(u => u.ListAccessPointsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UnifiAccessPointDto>
            {
                new("11:22:33:44:55:66", "AP-Patio-Novo", "U6", "192.168.1.99",
                    true, 100, 5, 10, 0, 0, 0, []),
            });

        var result = await Handler(context, unifi).Handle(
            new SearchApExplorerQuery(), CancellationToken.None);

        var item = result.Items.Should().ContainSingle().Subject;
        item.IsRegistered.Should().BeFalse();
        item.Name.Should().Be("AP-Patio-Novo");
        item.DeviceId.Should().BeNull();
        item.UniFiConnected.Should().BeTrue();
    }

    [Fact]
    public async Task Explorer_ShouldResolveUplinkFromLldpNeighbor()
    {
        await using var context = await CreateContextAsync();
        var (locationId, apId) = await SeedApAsync(context);
        var switchDevice = Device.Create(locationId, "sw-core", DeviceType.Switch);
        context.Devices.Add(switchDevice);
        var run = DiscoveryRun.Start(switchDevice.Id);
        run.Complete();
        context.DiscoveryRuns.Add(run);
        await context.SaveChangesAsync();

        context.DiscoveryResults.Add(DiscoveryResult.Create(
            run.Id, DiscoveryResultKind.Neighbor, "vizinho",
            new Dictionary<string, string>
            {
                ["chassis_id"] = "AA:BB:CC:00:00:02",
                ["sys_name"] = "AP-Sala",
                ["local_port"] = "Gi1/0/10",
                ["port_id"] = "2",
            },
            deviceId: switchDevice.Id));
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(
            new SearchApExplorerQuery(), CancellationToken.None);

        var item = result.Items.Should().ContainSingle(i => i.Name == "AP-Sala").Subject;
        item.UplinkSwitchName.Should().Be("sw-core");
        item.UplinkPort.Should().Be("Gi1/0/10");
    }

    [Fact]
    public async Task Explorer_WhenUnifiDown_ShouldStillListInventory()
    {
        await using var context = await CreateContextAsync();
        await SeedApAsync(context);

        var unifi = new Mock<IUnifiApiClient>();
        unifi.Setup(u => u.ListAccessPointsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"));
        unifi.Setup(u => u.ListDevicesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"));
        unifi.Setup(u => u.ListConnectedClientsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"));

        var result = await Handler(context, unifi).Handle(
            new SearchApExplorerQuery(), CancellationToken.None);

        var item = result.Items.Should().ContainSingle().Subject;
        item.Name.Should().Be("AP-Sala");
        item.UniFiConnected.Should().BeNull("UniFi fora não inventa estado");
        item.ClientCount.Should().Be(0);
    }
}

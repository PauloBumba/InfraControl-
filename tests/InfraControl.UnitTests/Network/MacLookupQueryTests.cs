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
/// Network Explorer F1 — `GET /api/v1/network/mac-lookup`: cruza evidências
/// reais de discovery (MacTableEntry→switch/porta, DhcpLease→ip/hostname/vlan,
/// ArpEntry, Neighbor/LLDP) e enriquece com AP Wi-Fi via UniFi ao vivo.
/// Regras: MAC completo casa só por MAC normalizado; termo livre casa
/// ip/hostname/vlan/mac parcial; IsStale quando a evidência mais nova tem
/// mais de 7 dias; UniFi indisponível nunca quebra a consulta.
/// </summary>
public sealed class MacLookupQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public MacLookupQueryTests()
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

    private static async Task<(Guid RunId, Guid DeviceId)> SeedRunAsync(
        AppDbContext context, string deviceName = "sw-core")
    {
        var location = Location.Create("Sala");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, deviceName, DeviceType.Switch);
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var run = DiscoveryRun.Start(device.Id);
        run.Complete();
        context.DiscoveryRuns.Add(run);
        await context.SaveChangesAsync();
        return (run.Id, device.Id);
    }

    private static async Task SeedResultAsync(
        AppDbContext context,
        Guid runId,
        DiscoveryResultKind kind,
        DateTime createdAt,
        IReadOnlyDictionary<string, string>? values,
        Guid? deviceId = null)
    {
        context.DiscoveryResults.Add(
            DiscoveryResult.Create(runId, kind, "achado", values, deviceId, createdAt));
        await context.SaveChangesAsync();
    }

    private static SearchMacLookupQueryHandler Handler(
        AppDbContext context, Mock<IUnifiApiClient>? unifi = null)
        => new(new DiscoveryResultRepository(context),
            new DeviceRepository(context),
            (unifi ?? new Mock<IUnifiApiClient>()).Object);

    private static DateTime Recent() => DateTime.UtcNow.AddHours(-1);
    private static DateTime Old() => DateTime.UtcNow.AddDays(-30);

    [Fact]
    public async Task Lookup_ByMac_ShouldCorrelateSwitchPortIpHostnameAndVlan()
    {
        await using var context = await CreateContextAsync();
        var (runId, switchId) = await SeedRunAsync(context);

        await SeedResultAsync(context, runId, DiscoveryResultKind.MacTableEntry, Recent(),
            new Dictionary<string, string>
            {
                ["mac"] = "AA:BB:CC:DD:EE:FF",
                ["bridge_port"] = "Gi1/0/24",
                ["fdb_status"] = "3",
            }, deviceId: switchId);
        await SeedResultAsync(context, runId, DiscoveryResultKind.DhcpLease, Recent(),
            new Dictionary<string, string>
            {
                ["mac"] = "aa-bb-cc-dd-ee-ff",
                ["ip"] = "192.0.2.50",
                ["hostname"] = "estacao-01",
                ["vlan"] = "10",
            });
        await SeedResultAsync(context, runId, DiscoveryResultKind.ArpEntry, Recent(),
            new Dictionary<string, string>
            {
                ["mac"] = "AABB.CCDD.EEFF",
                ["ip"] = "192.0.2.50",
                ["local_if"] = "Vlan10",
            });

        var result = await Handler(context).Handle(
            new SearchMacLookupQuery("aa:bb:cc:dd:ee:ff"), CancellationToken.None);

        result.Sightings.Should().HaveCount(3);
        result.Sightings[0].Kind.Should().Be(nameof(DiscoveryResultKind.MacTableEntry),
            "tabela MAC vem primeiro — é a resposta switch+porta");
        result.Sightings[0].DeviceName.Should().Be("sw-core");
        result.Sightings[0].Port.Should().Be("Gi1/0/24");

        result.MatchedMac.Should().NotBeNullOrEmpty();
        result.Ip.Should().Be("192.0.2.50");
        result.Hostname.Should().Be("estacao-01");
        result.Vlan.Should().Be("10");
        result.IsStale.Should().BeFalse();
        result.LastSeenAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Lookup_ByIpOrHostname_ShouldMatchFreeTerm()
    {
        await using var context = await CreateContextAsync();
        var (runId, _) = await SeedRunAsync(context);
        await SeedResultAsync(context, runId, DiscoveryResultKind.DhcpLease, Recent(),
            new Dictionary<string, string>
            {
                ["mac"] = "11:22:33:44:55:66",
                ["ip"] = "192.0.2.77",
                ["hostname"] = "Servidor-DB",
            });
        await SeedResultAsync(context, runId, DiscoveryResultKind.ArpEntry, Recent(),
            new Dictionary<string, string> { ["ip"] = "192.0.2.88", ["mac"] = "66:55:44:33:22:11" });

        var porIp = await Handler(context).Handle(
            new SearchMacLookupQuery("192.0.2.77"), CancellationToken.None);
        var porHost = await Handler(context).Handle(
            new SearchMacLookupQuery("servidor"), CancellationToken.None);

        porIp.Sightings.Should().ContainSingle();
        porIp.MatchedMac.Should().NotBeNull();
        porHost.Sightings.Should().ContainSingle();
        porHost.Hostname.Should().Be("Servidor-DB");
    }

    [Fact]
    public async Task Lookup_UnknownMac_ShouldReturnEmptyNotOffline()
    {
        await using var context = await CreateContextAsync();
        var (runId, _) = await SeedRunAsync(context);
        await SeedResultAsync(context, runId, DiscoveryResultKind.ArpEntry, Recent(),
            new Dictionary<string, string> { ["ip"] = "192.0.2.1", ["mac"] = "00:11:22:33:44:55" });

        var result = await Handler(context).Handle(
            new SearchMacLookupQuery("de:ad:be:ef:00:01"), CancellationToken.None);

        result.Sightings.Should().BeEmpty();
        result.MatchedMac.Should().BeNull();
        result.Ip.Should().BeNull();
        result.LastSeenAt.Should().BeNull();
        result.IsStale.Should().BeFalse();
    }

    [Fact]
    public async Task Lookup_OldEvidence_ShouldFlagStale()
    {
        await using var context = await CreateContextAsync();
        var (runId, _) = await SeedRunAsync(context);
        await SeedResultAsync(context, runId, DiscoveryResultKind.ArpEntry, Old(),
            new Dictionary<string, string> { ["ip"] = "192.0.2.9", ["mac"] = "00:11:22:33:44:66" });

        var result = await Handler(context).Handle(
            new SearchMacLookupQuery("00:11:22:33:44:66"), CancellationToken.None);

        result.Sightings.Should().ContainSingle();
        result.IsStale.Should().BeTrue("evidência com 30 dias é registro antigo");
    }

    [Fact]
    public async Task Lookup_WifiClient_ShouldResolveApFromUnifi()
    {
        await using var context = await CreateContextAsync();
        var (runId, _) = await SeedRunAsync(context);
        var apLocation = Location.Create("Patio");
        context.Locations.Add(apLocation);
        await context.SaveChangesAsync();
        var ap = Device.Create(apLocation.Id, "AP-Patio", DeviceType.AccessPoint,
            ip: "192.168.1.12", mac: "aa:bb:cc:00:00:01");
        context.Devices.Add(ap);
        await context.SaveChangesAsync();

        await SeedResultAsync(context, runId, DiscoveryResultKind.ArpEntry, Recent(),
            new Dictionary<string, string> { ["ip"] = "192.168.1.50", ["mac"] = "ff:ee:dd:cc:bb:aa" });

        var unifi = new Mock<IUnifiApiClient>();
        unifi.Setup(u => u.ListConnectedClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UnifiConnectedClientDto>
            {
                new("FF:EE:DD:CC:BB:AA", "192.168.1.50", "laptop", "AA:BB:CC:00:00:01",
                    null, "Escritorio-5G", null, -55, false, null, "ng"),
            });

        var result = await Handler(context, unifi).Handle(
            new SearchMacLookupQuery("ff:ee:dd:cc:bb:aa"), CancellationToken.None);

        result.WifiClient.Should().BeTrue();
        result.WifiApName.Should().Be("AP-Patio");
        result.WifiSsid.Should().Be("Escritorio-5G");
    }

    [Fact]
    public async Task Lookup_WhenUnifiFails_ShouldStillReturnDiscoveryEvidence()
    {
        await using var context = await CreateContextAsync();
        var (runId, _) = await SeedRunAsync(context);
        await SeedResultAsync(context, runId, DiscoveryResultKind.ArpEntry, Recent(),
            new Dictionary<string, string> { ["ip"] = "192.0.2.5", ["mac"] = "00:11:22:33:44:77" });

        var unifi = new Mock<IUnifiApiClient>();
        unifi.Setup(u => u.ListConnectedClientsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("controller offline"));

        var result = await Handler(context, unifi).Handle(
            new SearchMacLookupQuery("00:11:22:33:44:77"), CancellationToken.None);

        result.Sightings.Should().ContainSingle();
        result.WifiClient.Should().BeFalse();
        result.Ip.Should().Be("192.0.2.5");
    }

    [Fact]
    public async Task Lookup_ShortTerm_ShouldReturnEmptyWithoutQuerying()
    {
        await using var context = await CreateContextAsync();

        var result = await Handler(context).Handle(
            new SearchMacLookupQuery("a"), CancellationToken.None);

        result.Sightings.Should().BeEmpty();
        result.Term.Should().Be("a");
    }
}

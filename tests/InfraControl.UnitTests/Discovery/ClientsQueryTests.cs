using FluentAssertions;
using InfraControl.Application.Discovery.Clients.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;

namespace InfraControl.UnitTests.Discovery;

/// <summary>
/// F18-T008: `GET /api/v1/clients` — clientes da rede agregados de TODAS as
/// execuções de discovery (ARP/DHCP), sem depender do último run (decisão do
/// F17-T015). Dedupe por MAC normalizado (fallback = IP quando não há MAC);
/// `lastSeenAt` = CreatedAt mais recente do grupo; campos ausentes da linha
/// mais nova são completados por linhas antigas do mesmo grupo; achados sem
/// ip E sem MAC (ex.: "Nenhuma entrada ARP.") ficam de fora.
/// </summary>
public sealed class ClientsQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ClientsQueryTests()
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

    private static async Task<Guid> SeedRunAsync(AppDbContext context)
    {
        var location = Location.Create("Sala");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, "sw-01", DeviceType.Switch);
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var run = DiscoveryRun.Start(device.Id);
        run.Complete();
        context.DiscoveryRuns.Add(run);
        await context.SaveChangesAsync();
        return run.Id;
    }

    private static async Task SeedResultAsync(
        AppDbContext context,
        Guid runId,
        DiscoveryResultKind kind,
        DateTime createdAt,
        IReadOnlyDictionary<string, string>? values,
        string summary = "achado")
    {
        context.DiscoveryResults.Add(
            DiscoveryResult.Create(runId, kind, summary, values, createdAt: createdAt));
        await context.SaveChangesAsync();
    }

    private static SearchClientsQueryHandler SearchHandler(AppDbContext context)
        => new(new DiscoveryResultRepository(context));

    private static DateTime At(int hour, int minute)
        => new(2026, 9, 28, hour, minute, 0, DateTimeKind.Utc);

    private static readonly IReadOnlyDictionary<string, string> ArpValues =
        new Dictionary<string, string>
        {
            ["ip"] = "192.0.2.10",
            ["mac"] = "AA:BB:CC:DD:EE:FF",
            ["local_if"] = "Gi0/1",
        };

    [Fact]
    public async Task Search_ShouldAggregateAcrossRunsAndDedupeByMac()
    {
        await using var context = await CreateContextAsync();
        var run1 = await SeedRunAsync(context);
        var run2 = await SeedRunAsync(context);
        await SeedResultAsync(context, run1, DiscoveryResultKind.ArpEntry, At(12, 0), ArpValues);
        await SeedResultAsync(context, run2, DiscoveryResultKind.DhcpLease, At(13, 0),
            new Dictionary<string, string>
            {
                ["ip"] = "192.0.2.10",
                ["mac"] = "aa:bb:cc:dd:ee:ff",
                ["hostname"] = "pc-01",
                ["vlan"] = "10",
            });

        var page = await SearchHandler(context).Handle(
            new SearchClientsQuery(), CancellationToken.None);

        page.TotalCount.Should().Be(1);
        var client = page.Items.Single();
        client.Mac.Should().Be("aa:bb:cc:dd:ee:ff");
        client.Ip.Should().Be("192.0.2.10");
        client.Hostname.Should().Be("pc-01");
        client.Vlan.Should().Be("10");
        client.LocalIf.Should().Be("Gi0/1", "a linha ARP mais antiga completa a interface");
        client.Kind.Should().Be("DhcpLease", "o kind é o da linha mais recente");
        client.LastSeenAt.Should().Be(At(13, 0));
    }

    [Fact]
    public async Task Search_ShouldFillFieldsMissingInTheNewestRowFromOlderOnes()
    {
        await using var context = await CreateContextAsync();
        var run1 = await SeedRunAsync(context);
        var run2 = await SeedRunAsync(context);
        await SeedResultAsync(context, run1, DiscoveryResultKind.DhcpLease, At(12, 0),
            new Dictionary<string, string>
            {
                ["ip"] = "192.0.2.20",
                ["mac"] = "11:22:33:44:55:66",
                ["hostname"] = "notebook",
                ["vlan"] = "20",
            });
        // ARP mais recente não traz hostname/vlan — não podem sumir.
        await SeedResultAsync(context, run2, DiscoveryResultKind.ArpEntry, At(14, 0),
            new Dictionary<string, string>
            {
                ["ip"] = "192.0.2.20",
                ["mac"] = "11:22:33:44:55:66",
                ["local_if"] = "Gi0/2",
            });

        var page = await SearchHandler(context).Handle(
            new SearchClientsQuery(), CancellationToken.None);

        page.TotalCount.Should().Be(1);
        var client = page.Items.Single();
        client.Hostname.Should().Be("notebook");
        client.Vlan.Should().Be("20");
        client.LocalIf.Should().Be("Gi0/2");
        client.LastSeenAt.Should().Be(At(14, 0));
    }

    [Fact]
    public async Task Search_ShouldExcludeNonClientKindsAndFindingsWithoutIpAndMac()
    {
        await using var context = await CreateContextAsync();
        var run1 = await SeedRunAsync(context);
        var run2 = await SeedRunAsync(context);
        // Kinds que não são clientes (mesmo com ip/mac nos values).
        await SeedResultAsync(context, run1, DiscoveryResultKind.Neighbor, At(12, 0), ArpValues);
        await SeedResultAsync(context, run1, DiscoveryResultKind.Host, At(12, 1), ArpValues);
        await SeedResultAsync(context, run1, DiscoveryResultKind.MacTableEntry, At(12, 2), ArpValues);
        // Achado negativo de ARP: values sem ip e sem MAC.
        await SeedResultAsync(context, run2, DiscoveryResultKind.ArpEntry, At(13, 0),
            new Dictionary<string, string> { ["message"] = "Nenhuma entrada ARP." },
            summary: "Nenhuma entrada ARP.");
        // Cliente real.
        await SeedResultAsync(context, run2, DiscoveryResultKind.ArpEntry, At(13, 1), ArpValues);

        var page = await SearchHandler(context).Handle(
            new SearchClientsQuery(), CancellationToken.None);

        page.TotalCount.Should().Be(1);
        page.Items.Single().Ip.Should().Be("192.0.2.10");
    }

    [Fact]
    public async Task Search_TermShouldMatchIpMacOrHostnameCaseInsensitive()
    {
        await using var context = await CreateContextAsync();
        var run = await SeedRunAsync(context);
        await SeedResultAsync(context, run, DiscoveryResultKind.DhcpLease, At(12, 0),
            new Dictionary<string, string>
            {
                ["ip"] = "192.0.2.10",
                ["mac"] = "AA:BB:CC:DD:EE:FF",
                ["hostname"] = "Servidor-01",
            });
        await SeedResultAsync(context, run, DiscoveryResultKind.DhcpLease, At(12, 1),
            new Dictionary<string, string>
            {
                ["ip"] = "192.0.2.20",
                ["mac"] = "11:22:33:44:55:66",
                ["hostname"] = "Notebook",
            });

        var porIp = await SearchHandler(context).Handle(
            new SearchClientsQuery(Term: "192.0.2.10"), CancellationToken.None);
        var porHostname = await SearchHandler(context).Handle(
            new SearchClientsQuery(Term: "servidor"), CancellationToken.None);
        var porMac = await SearchHandler(context).Handle(
            new SearchClientsQuery(Term: "55:66"), CancellationToken.None);
        var nada = await SearchHandler(context).Handle(
            new SearchClientsQuery(Term: "inexistente"), CancellationToken.None);

        porIp.TotalCount.Should().Be(1);
        porIp.Items.Single().Ip.Should().Be("192.0.2.10");
        porHostname.TotalCount.Should().Be(1);
        porHostname.Items.Single().Hostname.Should().Be("Servidor-01");
        porMac.TotalCount.Should().Be(1);
        porMac.Items.Single().Mac.Should().Be("11:22:33:44:55:66");
        nada.TotalCount.Should().Be(0);
        nada.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_ShouldPaginateByDistinctClientsWithClampAndNewestFirst()
    {
        await using var context = await CreateContextAsync();
        var run1 = await SeedRunAsync(context);
        var run2 = await SeedRunAsync(context);
        // 3 clientes distintos × 2 linhas cada = 6 achados → TotalCount 3.
        foreach (var (mac, ip, seen) in new[]
        {
            ("AA:00:00:00:00:01", "192.0.2.1", At(12, 0)),
            ("AA:00:00:00:00:02", "192.0.2.2", At(11, 0)),
            ("AA:00:00:00:00:03", "192.0.2.3", At(13, 0)),
        })
        {
            var values = new Dictionary<string, string> { ["ip"] = ip, ["mac"] = mac };
            await SeedResultAsync(context, run1, DiscoveryResultKind.ArpEntry, seen, values);
            await SeedResultAsync(context, run2, DiscoveryResultKind.ArpEntry,
                seen.AddMinutes(30), values);
        }

        var clamped = await SearchHandler(context).Handle(
            new SearchClientsQuery(Page: 0, PageSize: 500), CancellationToken.None);
        var segundaPagina = await SearchHandler(context).Handle(
            new SearchClientsQuery(Page: 2, PageSize: 2), CancellationToken.None);

        clamped.Page.Should().Be(1);
        clamped.PageSize.Should().Be(100);
        clamped.TotalCount.Should().Be(3);
        // os vistos mais recentemente vêm primeiro
        clamped.Items.Select(client => client.Mac)
            .Should().Equal(
                "AA:00:00:00:00:03",
                "AA:00:00:00:00:01",
                "AA:00:00:00:00:02");
        segundaPagina.TotalCount.Should().Be(3);
        segundaPagina.Items.Should().ContainSingle()
            .Which.Mac.Should().Be("AA:00:00:00:00:02");
    }

    [Fact]
    public async Task Search_ShouldDedupeByIp_WhenMacIsMissing()
    {
        await using var context = await CreateContextAsync();
        var run = await SeedRunAsync(context);
        await SeedResultAsync(context, run, DiscoveryResultKind.ArpEntry, At(12, 0),
            new Dictionary<string, string> { ["ip"] = "192.0.2.99" });
        await SeedResultAsync(context, run, DiscoveryResultKind.ArpEntry, At(12, 30),
            new Dictionary<string, string> { ["ip"] = "192.0.2.99" });

        var page = await SearchHandler(context).Handle(
            new SearchClientsQuery(), CancellationToken.None);

        page.TotalCount.Should().Be(1);
        var client = page.Items.Single();
        client.Mac.Should().BeNull();
        client.LastSeenAt.Should().Be(At(12, 30));
    }

    [Fact]
    public async Task Search_EmptyBase_ShouldReturnEmptyPage()
    {
        await using var context = await CreateContextAsync();

        var page = await SearchHandler(context).Handle(
            new SearchClientsQuery(), CancellationToken.None);

        page.TotalCount.Should().Be(0);
        page.Items.Should().BeEmpty();
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(20);
    }
}

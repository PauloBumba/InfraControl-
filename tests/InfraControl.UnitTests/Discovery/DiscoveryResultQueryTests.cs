using FluentAssertions;
using InfraControl.Application.Discovery.Results.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Repositories;
using InfraControl.Infrastructure.Persistence;

namespace InfraControl.UnitTests.Discovery;

/// <summary>
/// F4-T002: queries somente-leitura do DiscoveryResult (escopo aprovado:
/// leitura apenas — escrita vem com os providers T003+). Ordenação por
/// CreatedAt DESC; lista de run inexistente → 404 (mesmo do GET run).
/// </summary>
public sealed class DiscoveryResultQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public DiscoveryResultQueryTests()
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

    private static async Task<Guid> SeedDeviceAsync(AppDbContext context, string deviceName)
    {
        var location = Location.Create("Data Center A");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, deviceName, DeviceType.Switch);
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

    private static async Task<Guid> SeedResultAsync(
        AppDbContext context,
        Guid runId,
        DiscoveryResultKind kind,
        string summary,
        DateTime createdAt,
        IReadOnlyDictionary<string, string>? values = null,
        Guid? deviceId = null)
    {
        var result = DiscoveryResult.Create(runId, kind, summary, values, deviceId, createdAt);
        context.DiscoveryResults.Add(result);
        await context.SaveChangesAsync();
        return result.Id;
    }

    private static SearchDiscoveryResultsQueryHandler SearchHandler(AppDbContext context)
        => new(new DiscoveryResultRepository(context), new DiscoveryRunRepository(context));

    private static GetDiscoveryResultQueryHandler GetHandler(AppDbContext context)
        => new(new DiscoveryResultRepository(context));

    private static DateTime At(int hour, int minute)
        => new(2026, 9, 25, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Search_ShouldReturnOnlyThatRunOrderedByCreatedAtDesc()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, "sw-01");
        var runId = await SeedRunAsync(context, deviceId);
        var otherRunId = await SeedRunAsync(context, deviceId);
        await SeedResultAsync(context, runId, DiscoveryResultKind.Neighbor, "r1", At(12, 0));
        await SeedResultAsync(context, runId, DiscoveryResultKind.Host, "r2", At(12, 10));
        await SeedResultAsync(context, runId, DiscoveryResultKind.ArpEntry, "r3", At(12, 5));
        await SeedResultAsync(context, otherRunId, DiscoveryResultKind.Host, "outra", At(13, 0));

        var result = await SearchHandler(context).Handle(
            new SearchDiscoveryResultsQuery(runId), CancellationToken.None);

        result.TotalCount.Should().Be(3);
        result.Items.Select(item => item.Summary)
            .Should().Equal("r2", "r3", "r1");
        result.Items.Should().OnlyContain(item => item.DiscoveryRunId == runId);
    }

    [Fact]
    public async Task Search_UnknownRun_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var act = () => SearchHandler(context).Handle(
            new SearchDiscoveryResultsQuery(unknownId), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Execução de discovery '{unknownId}' não encontrada.");
    }

    [Fact]
    public async Task Search_PageAndSize_ShouldBeClamped()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, "sw-01");
        var runId = await SeedRunAsync(context, deviceId);
        await SeedResultAsync(context, runId, DiscoveryResultKind.Host, "r1", At(12, 0));
        await SeedResultAsync(context, runId, DiscoveryResultKind.Host, "r2", At(12, 1));
        await SeedResultAsync(context, runId, DiscoveryResultKind.Host, "r3", At(12, 2));

        var result = await SearchHandler(context).Handle(
            new SearchDiscoveryResultsQuery(runId, Page: 0, PageSize: 0),
            CancellationToken.None);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(1);
        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Search_KindFilter_ShouldFilterAndPaginateWithFilteredTotalCount()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, "sw-01");
        var runId = await SeedRunAsync(context, deviceId);
        await SeedResultAsync(context, runId, DiscoveryResultKind.Host, "h1", At(12, 0));
        await SeedResultAsync(context, runId, DiscoveryResultKind.Host, "h2", At(12, 1));
        await SeedResultAsync(context, runId, DiscoveryResultKind.Neighbor, "n1", At(12, 2));

        var result = await SearchHandler(context).Handle(
            new SearchDiscoveryResultsQuery(runId, Kind: "Host", PageSize: 1),
            CancellationToken.None);

        result.TotalCount.Should().Be(2, "totalCount respeita o filtro kind");
        result.Items.Should().ContainSingle();
        result.Items.Should().OnlyContain(item => item.Kind == "Host");
    }

    [Fact]
    public async Task Search_KindFilter_ShouldBeCaseInsensitive()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, "sw-01");
        var runId = await SeedRunAsync(context, deviceId);
        await SeedResultAsync(context, runId, DiscoveryResultKind.Neighbor, "n1", At(12, 0));
        await SeedResultAsync(context, runId, DiscoveryResultKind.ArpEntry, "a1", At(12, 1));

        var minusculas = await SearchHandler(context).Handle(
            new SearchDiscoveryResultsQuery(runId, Kind: "neighbor"), CancellationToken.None);
        var maiusculas = await SearchHandler(context).Handle(
            new SearchDiscoveryResultsQuery(runId, Kind: "ARPENTRY"), CancellationToken.None);

        minusculas.TotalCount.Should().Be(1);
        minusculas.Items.Single().Kind.Should().Be(nameof(DiscoveryResultKind.Neighbor));
        maiusculas.TotalCount.Should().Be(1);
        maiusculas.Items.Single().Kind.Should().Be(nameof(DiscoveryResultKind.ArpEntry));
    }

    [Fact]
    public async Task Search_InvalidKind_ShouldThrowPtBrListingValidValues()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, "sw-01");
        var runId = await SeedRunAsync(context, deviceId);

        // Texto fora do enum E número fora do range (Enum.TryParse aceita
        // dígitos — o guard IsDefined precisa rejeitar).
        var actTexto = () => SearchHandler(context).Handle(
            new SearchDiscoveryResultsQuery(runId, Kind: "banana"), CancellationToken.None);
        var actNumero = () => SearchHandler(context).Handle(
            new SearchDiscoveryResultsQuery(runId, Kind: "9"), CancellationToken.None);

        (await actTexto.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*Valores válidos: Neighbor, ArpEntry, MacTableEntry, DhcpLease, Host*");
        (await actNumero.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*Valores válidos: Neighbor, ArpEntry, MacTableEntry, DhcpLease, Host*");
    }

    [Fact]
    public async Task GetById_Existing_ShouldReturnKindAsStringAndValues()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, "sw-01");
        var runId = await SeedRunAsync(context, deviceId);
        var values = new Dictionary<string, string> { ["ip"] = "192.0.2.5", ["mac"] = "aa:bb" };
        var id = await SeedResultAsync(
            context,
            runId,
            DiscoveryResultKind.ArpEntry,
            "arp 192.0.2.5",
            At(12, 0),
            values,
            deviceId);

        var result = await GetHandler(context).Handle(
            new GetDiscoveryResultQuery(id), CancellationToken.None);

        result.Id.Should().Be(id);
        result.DiscoveryRunId.Should().Be(runId);
        result.Kind.Should().Be(nameof(DiscoveryResultKind.ArpEntry));
        result.Summary.Should().Be("arp 192.0.2.5");
        result.Values.Should().BeEquivalentTo(values);
        result.DeviceId.Should().Be(deviceId);
    }

    [Fact]
    public async Task GetById_Unknown_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var act = () => GetHandler(context).Handle(
            new GetDiscoveryResultQuery(unknownId), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Resultado de discovery '{unknownId}' não encontrado.");
    }
}

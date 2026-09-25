using InfraControl.Application.Discovery.Runs.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Repositories;

namespace InfraControl.UnitTests.Discovery;

/// <summary>
/// F4-T001: queries somente-leitura do DiscoveryRun (sem POST nesta task —
/// gatilho vem com os providers F4-T003/T004). Ordenação por StartedAt DESC.
/// </summary>
public sealed class DiscoveryRunQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public DiscoveryRunQueryTests()
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

    private static async Task<Guid> SeedRunAsync(
        AppDbContext context,
        Guid deviceId,
        DateTime startedAt,
        DiscoveryRunStatus status = DiscoveryRunStatus.Running)
    {
        var run = DiscoveryRun.Start(deviceId, startedAt);
        if (status == DiscoveryRunStatus.Completed)
            run.Complete();
        context.DiscoveryRuns.Add(run);
        await context.SaveChangesAsync();
        return run.Id;
    }

    private static SearchDiscoveryRunsQueryHandler SearchHandler(AppDbContext context)
        => new(new DiscoveryRunRepository(context));

    private static GetDiscoveryRunQueryHandler GetHandler(AppDbContext context)
        => new(new DiscoveryRunRepository(context));

    private static DateTime At(int hour, int minute)
        => new(2026, 9, 25, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Search_NoFilter_ShouldReturnAllOrderedByStartedAtDesc()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, "sw-01");
        await SeedRunAsync(context, deviceId, At(12, 0));
        await SeedRunAsync(context, deviceId, At(12, 10));
        await SeedRunAsync(context, deviceId, At(12, 5));

        var result = await SearchHandler(context).Handle(
            new SearchDiscoveryRunsQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(3);
        result.Items.Select(run => run.StartedAt)
            .Should().Equal(At(12, 10), At(12, 5), At(12, 0));
    }

    [Fact]
    public async Task Search_DeviceIdFilter_ShouldReturnOnlyThatDevice()
    {
        await using var context = await CreateContextAsync();
        var deviceA = await SeedDeviceAsync(context, "sw-a");
        var deviceB = await SeedDeviceAsync(context, "sw-b");
        await SeedRunAsync(context, deviceA, At(12, 0));
        await SeedRunAsync(context, deviceA, At(13, 0));
        await SeedRunAsync(context, deviceB, At(14, 0));

        var result = await SearchHandler(context).Handle(
            new SearchDiscoveryRunsQuery(DeviceId: deviceA), CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(run => run.DeviceId == deviceA);
    }

    [Fact]
    public async Task Search_PageAndSize_ShouldBeClamped()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, "sw-01");
        await SeedRunAsync(context, deviceId, At(12, 0));
        await SeedRunAsync(context, deviceId, At(12, 1));
        await SeedRunAsync(context, deviceId, At(12, 2));

        var result = await SearchHandler(context).Handle(
            new SearchDiscoveryRunsQuery(Page: 0, PageSize: 0), CancellationToken.None);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(1);
        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetById_Existing_ShouldReturn()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, "sw-01");
        var startedAt = At(12, 0);
        var runId = await SeedRunAsync(context, deviceId, startedAt);

        var result = await GetHandler(context).Handle(
            new GetDiscoveryRunQuery(runId), CancellationToken.None);

        result.Id.Should().Be(runId);
        result.DeviceId.Should().Be(deviceId);
        result.Status.Should().Be(nameof(DiscoveryRunStatus.Running));
        result.StartedAt.Should().Be(startedAt);
        result.FinishedAt.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task GetById_Unknown_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var act = () => GetHandler(context).Handle(
            new GetDiscoveryRunQuery(unknownId), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Execução de discovery '{unknownId}' não encontrada.");
    }
}

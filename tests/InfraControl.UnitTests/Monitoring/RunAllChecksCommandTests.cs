using FluentAssertions;
using InfraControl.Application.Alerts;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.Application.Monitoring.Checks;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Services;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// MON-006/007 (docs/21): RunAllChecksCommand roda um check em todos os
/// devices — os com IP executam e persistem; os sem IP entram como
/// Executed=false (não houve tentativa).
/// </summary>
public sealed class RunAllChecksCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public RunAllChecksCommandTests()
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

    private static async Task<Location> SeedLocationAsync(AppDbContext context)
    {
        var location = Location.Create("Sala RunAll");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        return location;
    }

    private static async Task<Device> SeedDeviceAsync(
        AppDbContext context, Guid locationId, string name, string? ip)
    {
        var device = Device.Create(locationId, name, DeviceType.Switch, ip: ip);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    private sealed class FakePingProvider(Func<string, bool> upSelector) : IMonitoringProvider
    {
        public string Name => "Ping";

        public bool CanHandle(string providerKey) =>
            string.Equals(providerKey, "Ping", StringComparison.OrdinalIgnoreCase);

        public Task<MonitoringProviderResult> CheckAsync(
            string target, CancellationToken cancellationToken = default)
        {
            var up = upSelector(target);
            return Task.FromResult(new MonitoringProviderResult(
                up, TimeSpan.FromMilliseconds(7), up ? null : "Host inacessível."));
        }
    }

    private sealed class FakeRegistry(IMonitoringProvider provider)
        : IProviderRegistry<IMonitoringProvider>
    {
        public IMonitoringProvider Resolve(string providerKey) =>
            provider.CanHandle(providerKey)
                ? provider
                : throw new InvalidOperationException(
                    $"No provider registered for '{providerKey}'.");

        public IReadOnlyCollection<IMonitoringProvider> GetAll() => [provider];
    }

    [Fact]
    public async Task RunAllChecks_ShouldPersistAllResultsInSingleSaveChanges()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        await SeedDeviceAsync(context, location.Id, "dev-a", "192.0.2.90");
        await SeedDeviceAsync(context, location.Id, "dev-b", "192.0.2.91");
        await SeedDeviceAsync(context, location.Id, "dev-c", "192.0.2.92");

        var counting = new CountingResultRepository(new MonitoringResultRepository(context));
        var handler = new RunAllChecksCommandHandler(
            new DeviceRepository(context),
            new FakeRegistry(new FakePingProvider(_ => true)),
            counting,
            new AlertEvaluator(new AlertRepository(context),
                new MonitoringResultRepository(context),
                new IncidentRepository(context),
                new UpstreamCorrelationRepository(context),
                new RootCauseResolver(new ConnectionRepository(context),
                    new AlertRepository(context), new DeviceRepository(context),
                    new UpstreamCorrelationRepository(context),
                    new IncidentRepository(context))),
            NullLogger<RunAllChecksCommandHandler>.Instance);

        var results = await handler.Handle(new RunAllChecksCommand("Ping"), CancellationToken.None);

        results.Should().HaveCount(3);
        results.Should().OnlyContain(result => result.Executed && result.Succeeded);
        counting.SaveChangesCalls.Should().Be(1,
            "RunAll deve persistir em lote — N+1 (Dia 5, docs/20)");
        (await context.MonitoringResults.CountAsync()).Should().Be(3);
    }

    private sealed class CountingResultRepository(IMonitoringResultRepository inner)
        : IMonitoringResultRepository
    {
        public int SaveChangesCalls { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return inner.SaveChangesAsync(cancellationToken);
        }

        public Task<MonitoringResult?> GetByIdAsync(
            Guid id, CancellationToken cancellationToken = default)
            => inner.GetByIdAsync(id, cancellationToken);

        public Task AddAsync(MonitoringResult entity, CancellationToken cancellationToken = default)
            => inner.AddAsync(entity, cancellationToken);

        public void Remove(MonitoringResult entity) => inner.Remove(entity);

        public Task<IReadOnlyList<MonitoringResult>> GetRecentByDeviceAsync(
            Guid deviceId, int limit, CancellationToken cancellationToken = default)
            => inner.GetRecentByDeviceAsync(deviceId, limit, cancellationToken);

        public Task<IReadOnlyList<MonitoringResult>> GetRecentByDeviceAndKindAsync(
            Guid deviceId, Domain.Enums.CheckKind kind, int limit,
            CancellationToken cancellationToken = default)
            => inner.GetRecentByDeviceAndKindAsync(deviceId, kind, limit, cancellationToken);
    }

    private static RunAllChecksCommandHandler Handler(
        AppDbContext context, IProviderRegistry<IMonitoringProvider> registry)
        => new(
            new DeviceRepository(context),
            registry,
            new MonitoringResultRepository(context),
            new AlertEvaluator(new AlertRepository(context),
                new MonitoringResultRepository(context),
                new IncidentRepository(context),
                new UpstreamCorrelationRepository(context),
                new RootCauseResolver(new ConnectionRepository(context),
                    new AlertRepository(context), new DeviceRepository(context),
                    new UpstreamCorrelationRepository(context),
                    new IncidentRepository(context))),
            NullLogger<RunAllChecksCommandHandler>.Instance);

    [Fact]
    public async Task RunAll_ShouldExecuteDevicesWithIpAndSkipOnesWithout()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var up = await SeedDeviceAsync(context, location.Id, "sw-up", "192.0.2.20");
        var down = await SeedDeviceAsync(context, location.Id, "sw-down", "192.0.2.21");
        var noIp = await SeedDeviceAsync(context, location.Id, "sw-noip", ip: null);

        var provider = new FakePingProvider(ip => ip == "192.0.2.20");
        var results = await Handler(context, new FakeRegistry(provider))
            .Handle(new RunAllChecksCommand("Ping"), CancellationToken.None);

        results.Should().HaveCount(3);
        results.Single(r => r.DeviceId == up.Id).Succeeded.Should().BeTrue();
        results.Single(r => r.DeviceId == down.Id).Succeeded.Should().BeFalse();
        var skipped = results.Single(r => r.DeviceId == noIp.Id);
        skipped.Executed.Should().BeFalse();
        skipped.Error.Should().Be("Device sem IP cadastrado.");

        context.MonitoringResults.AsNoTracking().Count(r => r.DeviceId == up.Id)
            .Should().Be(1);
        context.MonitoringResults.AsNoTracking().Count(r => r.DeviceId == down.Id)
            .Should().Be(1);
        context.MonitoringResults.AsNoTracking().Count(r => r.DeviceId == noIp.Id)
            .Should().Be(0);
    }

    [Fact]
    public async Task RunAll_InvalidCheck_ShouldThrowArgumentPtBrMessage()
    {
        await using var context = await CreateContextAsync();

        var action = () => Handler(context, new FakeRegistry(new FakePingProvider(_ => true)))
            .Handle(new RunAllChecksCommand("banana"), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Check inválido: 'banana'.*");
    }
}

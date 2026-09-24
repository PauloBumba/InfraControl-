using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Monitoring.Checks;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// MON-006/007 (docs/21): RunCheckCommand executa um check num device,
/// persiste MonitoringResult e loga (DoD observabilidade). ADR-006: a
/// execução vive no command, não na query.
/// </summary>
public sealed class RunCheckCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public RunCheckCommandTests()
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
        var location = Location.Create("Sala RunCheck");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        return location;
    }

    private static async Task<Device> SeedDeviceAsync(
        AppDbContext context, Guid locationId, DeviceType type, string name, string? ip)
    {
        var device = Device.Create(locationId, name, type, ip: ip);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    private static async Task<List<MonitoringResult>> RowsAsync(
        AppDbContext context, Guid deviceId)
    {
        var rows = await context.MonitoringResults
            .AsNoTracking()
            .Where(result => result.DeviceId == deviceId)
            .ToListAsync();
        return rows.OrderBy(result => result.Kind).ToList();
    }

    // ── fakes ────────────────────────────────────────────────────────────

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

    private sealed class FakeSupplyProvider(string name, int levelPercent) : IMonitoringProvider
    {
        public string Name => name;

        public bool CanHandle(string providerKey) =>
            string.Equals(providerKey, name, StringComparison.OrdinalIgnoreCase);

        public Task<MonitoringProviderResult> CheckAsync(
            string target, CancellationToken cancellationToken = default)
            => Task.FromResult(new MonitoringProviderResult(
                true,
                TimeSpan.FromMilliseconds(12),
                null,
                new Dictionary<string, string>
                {
                    ["levelPercent"] = levelPercent.ToString(),
                    ["description"] = $"{name} cartridge"
                }));
    }

    private sealed class FakeRegistry(params IMonitoringProvider[] providers)
        : IProviderRegistry<IMonitoringProvider>
    {
        public IMonitoringProvider Resolve(string providerKey) =>
            providers.FirstOrDefault(p => p.CanHandle(providerKey))
            ?? throw new InvalidOperationException(
                $"No provider registered for '{providerKey}'.");

        public IReadOnlyCollection<IMonitoringProvider> GetAll() => providers;
    }

    private static RunCheckCommandHandler Handler(
        AppDbContext context, IProviderRegistry<IMonitoringProvider> registry)
        => new(
            new DeviceRepository(context),
            registry,
            new MonitoringResultRepository(context),
            NullLogger<RunCheckCommandHandler>.Instance);

    // ── testes ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RunCheck_PingUp_ShouldExecuteAndPersistResult()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(
            context, location.Id, DeviceType.Switch, "sw-01", "192.0.2.10");

        var result = await Handler(context, new FakeRegistry(new FakePingProvider(_ => true)))
            .Handle(new RunCheckCommand(device.Id, "Ping"), CancellationToken.None);

        result.Executed.Should().BeTrue();
        result.Succeeded.Should().BeTrue();
        result.DurationMs.Should().Be(7);
        result.Error.Should().BeNull();

        var rows = await RowsAsync(context, device.Id);
        rows.Should().ContainSingle();
        rows[0].Kind.Should().Be(CheckKind.Ping);
        rows[0].Succeeded.Should().BeTrue();
        rows[0].DurationMs.Should().Be(7);
    }

    [Fact]
    public async Task RunCheck_PingDown_ShouldPersistFailedResultWithError()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(
            context, location.Id, DeviceType.Switch, "sw-02", "192.0.2.11");

        var result = await Handler(context, new FakeRegistry(new FakePingProvider(_ => false)))
            .Handle(new RunCheckCommand(device.Id, "ping"), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Host inacessível.");

        var rows = await RowsAsync(context, device.Id);
        rows.Should().ContainSingle();
        rows[0].Succeeded.Should().BeFalse();
        rows[0].Error.Should().Be("Host inacessível.");
    }

    [Fact]
    public async Task RunCheck_WithoutIp_ShouldNotExecuteNorPersist()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(
            context, location.Id, DeviceType.Switch, "sw-03", ip: null);

        var result = await Handler(context, new FakeRegistry(new FakePingProvider(_ => true)))
            .Handle(new RunCheckCommand(device.Id, "Ping"), CancellationToken.None);

        result.Executed.Should().BeFalse();
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Device sem IP cadastrado.");
        (await RowsAsync(context, device.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task RunCheck_UnknownDevice_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => Handler(context, new FakeRegistry(new FakePingProvider(_ => true)))
            .Handle(new RunCheckCommand(unknownId, "Ping"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task RunCheck_InvalidCheck_ShouldThrowArgumentPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(
            context, location.Id, DeviceType.Switch, "sw-04", "192.0.2.12");

        var action = () => Handler(context, new FakeRegistry(new FakePingProvider(_ => true)))
            .Handle(new RunCheckCommand(device.Id, "banana"), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Check inválido: 'banana'.*");
    }

    [Fact]
    public async Task RunCheck_SnmpToner_ShouldReturnValuesAndPersist()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var printer = await SeedDeviceAsync(
            context, location.Id, DeviceType.Printer, "prn-01", "192.0.2.50");

        var registry = new FakeRegistry(new FakeSupplyProvider("SnmpToner", 42));
        var result = await Handler(context, registry)
            .Handle(new RunCheckCommand(printer.Id, "SnmpToner"), CancellationToken.None);

        result.Executed.Should().BeTrue();
        result.Succeeded.Should().BeTrue();
        result.Values!["levelPercent"].Should().Be("42");

        var rows = await RowsAsync(context, printer.Id);
        rows.Should().ContainSingle();
        rows[0].Kind.Should().Be(CheckKind.SnmpToner);
        rows[0].Values!["levelPercent"].Should().Be("42");
    }

    [Fact]
    public async Task RunCheck_SnmpCheck_OnNonPrinter_ShouldThrowArgumentPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var sw = await SeedDeviceAsync(
            context, location.Id, DeviceType.Switch, "sw-05", "192.0.2.13");

        var action = () => Handler(context, new FakeRegistry(new FakePingProvider(_ => true)))
            .Handle(new RunCheckCommand(sw.Id, "SnmpToner"), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*só se aplica a devices do tipo Printer*");
        (await RowsAsync(context, sw.Id)).Should().BeEmpty();
    }
}

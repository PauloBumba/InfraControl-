using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Monitoring.Ping;
using InfraControl.Application.Monitoring.PrinterSupplies;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F5-T002: os polls on-demand (ping e suprimentos) persistem uma linha de
/// MonitoringResult por tentativa — sem IP não grava (não houve tentativa).
/// </summary>
public sealed class MonitoringResultWriteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public MonitoringResultWriteTests()
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
        var location = Location.Create("Sala Histórico");
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

    private sealed class FakeRegistry(params IMonitoringProvider[] providers)
        : IProviderRegistry<IMonitoringProvider>
    {
        public IMonitoringProvider Resolve(string providerKey) =>
            providers.FirstOrDefault(p => p.CanHandle(providerKey))
            ?? throw new InvalidOperationException(
                $"No provider registered for '{providerKey}'.");

        public IReadOnlyCollection<IMonitoringProvider> GetAll() => providers;
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

    // ── handlers ─────────────────────────────────────────────────────────

    private static PingDeviceQueryHandler DeviceHandler(
        AppDbContext context, IMonitoringProvider provider)
        => new(
            new DeviceRepository(context),
            new FakeRegistry(provider),
            new MonitoringResultRepository(context));

    private static GetPingStatusQueryHandler StatusHandler(
        AppDbContext context, IMonitoringProvider provider)
        => new(
            new DeviceRepository(context),
            new FakeRegistry(provider),
            new MonitoringResultRepository(context));

    private static GetPrinterSuppliesQueryHandler SuppliesHandler(
        AppDbContext context, IProviderRegistry<IMonitoringProvider> registry)
        => new(
            new DeviceRepository(context),
            registry,
            new MonitoringResultRepository(context));

    // ── ping ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task PingDevice_Up_ShouldPersistSucceededRow()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(
            context, location.Id, DeviceType.Switch, "sw-01", "192.0.2.10");

        await DeviceHandler(context, new FakePingProvider(_ => true))
            .Handle(new PingDeviceQuery(device.Id), CancellationToken.None);

        var rows = await RowsAsync(context, device.Id);
        rows.Should().HaveCount(1);
        rows[0].Kind.Should().Be(CheckKind.Ping);
        rows[0].Succeeded.Should().BeTrue();
        rows[0].DurationMs.Should().Be(7);
        rows[0].Error.Should().BeNull();
    }

    [Fact]
    public async Task PingDevice_Down_ShouldPersistFailedRowWithError()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(
            context, location.Id, DeviceType.Switch, "sw-02", "192.0.2.11");

        await DeviceHandler(context, new FakePingProvider(_ => false))
            .Handle(new PingDeviceQuery(device.Id), CancellationToken.None);

        var rows = await RowsAsync(context, device.Id);
        rows.Should().HaveCount(1);
        rows[0].Succeeded.Should().BeFalse();
        rows[0].Error.Should().Be("Host inacessível.");
    }

    [Fact]
    public async Task PingDevice_WithoutIp_ShouldNotPersist()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(
            context, location.Id, DeviceType.Switch, "sw-03", ip: null);

        await DeviceHandler(context, new FakePingProvider(_ => true))
            .Handle(new PingDeviceQuery(device.Id), CancellationToken.None);

        (await RowsAsync(context, device.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task PingStatus_ShouldPersistOneRowPerDeviceWithIp()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var up = await SeedDeviceAsync(
            context, location.Id, DeviceType.Switch, "sw-up", "192.0.2.20");
        var down = await SeedDeviceAsync(
            context, location.Id, DeviceType.Switch, "sw-down", "192.0.2.21");
        var noIp = await SeedDeviceAsync(
            context, location.Id, DeviceType.Switch, "sw-noip", ip: null);

        var provider = new FakePingProvider(ip => ip == "192.0.2.20");
        var results = await StatusHandler(context, provider)
            .Handle(new GetPingStatusQuery(), CancellationToken.None);

        results.Should().HaveCount(3);
        (await RowsAsync(context, up.Id)).Should().HaveCount(1)
            .And.Subject.Should().ContainSingle(r => r.Succeeded);
        (await RowsAsync(context, down.Id)).Should().HaveCount(1)
            .And.Subject.Should().ContainSingle(r => !r.Succeeded);
        (await RowsAsync(context, noIp.Id)).Should().BeEmpty();
    }

    // ── suprimentos ──────────────────────────────────────────────────────

    [Fact]
    public async Task Supplies_ShouldPersistTonerAndDrumRowsWithValues()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var printer = await SeedDeviceAsync(
            context, location.Id, DeviceType.Printer, "prn-01", "192.0.2.50");

        var registry = new FakeRegistry(
            new FakeSupplyProvider("SnmpToner", 42),
            new FakeSupplyProvider("SnmpDrum", 70));

        await SuppliesHandler(context, registry)
            .Handle(new GetPrinterSuppliesQuery(printer.Id), CancellationToken.None);

        var rows = await RowsAsync(context, printer.Id);
        rows.Select(row => row.Kind)
            .Should().Equal(CheckKind.SnmpToner, CheckKind.SnmpDrum);
        rows.Should().OnlyContain(row => row.Succeeded);
        rows.Single(row => row.Kind == CheckKind.SnmpToner).Values!["levelPercent"]
            .Should().Be("42");
        rows.Single(row => row.Kind == CheckKind.SnmpDrum).Values!["levelPercent"]
            .Should().Be("70");
    }

    [Fact]
    public async Task Supplies_WithoutIp_ShouldNotPersist()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var printer = await SeedDeviceAsync(
            context, location.Id, DeviceType.Printer, "prn-02", ip: null);

        var registry = new FakeRegistry(
            new FakeSupplyProvider("SnmpToner", 42),
            new FakeSupplyProvider("SnmpDrum", 70));

        await SuppliesHandler(context, registry)
            .Handle(new GetPrinterSuppliesQuery(printer.Id), CancellationToken.None);

        (await RowsAsync(context, printer.Id)).Should().BeEmpty();
    }
}

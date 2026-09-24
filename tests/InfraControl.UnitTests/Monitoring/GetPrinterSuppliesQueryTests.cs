using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Monitoring.PrinterSupplies;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F5-T004: GetPrinterSuppliesQuery (toner + drum sob demanda) com
/// repositório real e providers SNMP falsos.
/// </summary>
public sealed class GetPrinterSuppliesQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public GetPrinterSuppliesQueryTests()
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
        var location = Location.Create("TI");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        return location;
    }

    private static async Task<Device> SeedDeviceAsync(
        AppDbContext context,
        Guid locationId,
        DeviceType type,
        string? ip)
    {
        var device = Device.Create(locationId, $"dev-{Guid.NewGuid():N}"[..12], type, ip: ip);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    private sealed class FakeSupplyProvider(
        string name,
        int? levelPercent,
        string? error) : IMonitoringProvider
    {
        public string Name => name;

        public bool CanHandle(string providerKey) =>
            string.Equals(providerKey, name, StringComparison.OrdinalIgnoreCase);

        public Task<MonitoringProviderResult> CheckAsync(
            string target,
            CancellationToken cancellationToken = default)
        {
            if (error is not null)
                return Task.FromResult(new MonitoringProviderResult(false, TimeSpan.Zero, error));

            return Task.FromResult(new MonitoringProviderResult(
                true,
                TimeSpan.FromMilliseconds(12),
                null,
                new Dictionary<string, string>
                {
                    ["levelPercent"] = levelPercent!.Value.ToString(),
                    ["description"] = $"{name} supply"
                }));
        }
    }

    private sealed class FakeRegistry : IProviderRegistry<IMonitoringProvider>
    {
        private readonly Dictionary<string, IMonitoringProvider> _byKey;

        public FakeRegistry(params IMonitoringProvider[] providers) =>
            _byKey = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        public IMonitoringProvider Resolve(string providerKey) =>
            _byKey.TryGetValue(providerKey, out var provider)
                ? provider
                : throw new InvalidOperationException(
                    $"No provider registered for '{providerKey}'.");

        public IReadOnlyCollection<IMonitoringProvider> GetAll() => _byKey.Values.ToArray();
    }

    private static GetPrinterSuppliesQueryHandler Handler(
        AppDbContext context,
        IProviderRegistry<IMonitoringProvider> registry)
        => new(new DeviceRepository(context), registry);

    private static FakeRegistry DefaultRegistry(int toner = 42, int drum = 70) =>
        new(
            new FakeSupplyProvider("SnmpToner", toner, null),
            new FakeSupplyProvider("SnmpDrum", drum, null));

    [Fact]
    public async Task Supplies_PrinterWithIp_ShouldReturnTonerAndDrumLevels()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var printer = await SeedDeviceAsync(context, location.Id, DeviceType.Printer, "192.0.2.50");

        var result = await Handler(context, DefaultRegistry(toner: 42, drum: 70))
            .Handle(new GetPrinterSuppliesQuery(printer.Id), CancellationToken.None);

        result.DeviceId.Should().Be(printer.Id);
        result.Ip.Should().Be("192.0.2.50");
        result.Supplies.Should().HaveCount(2);

        var toner = result.Supplies.Single(s => s.Check == "SnmpToner");
        toner.Succeeded.Should().BeTrue();
        toner.LevelPercent.Should().Be(42);

        var drum = result.Supplies.Single(s => s.Check == "SnmpDrum");
        drum.Succeeded.Should().BeTrue();
        drum.LevelPercent.Should().Be(70);
    }

    [Fact]
    public async Task Supplies_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => Handler(context, DefaultRegistry())
            .Handle(new GetPrinterSuppliesQuery(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Supplies_NonPrinterDevice_ShouldThrowArgumentPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var switchDevice = await SeedDeviceAsync(
            context, location.Id, DeviceType.Switch, "192.0.2.60");

        var action = () => Handler(context, DefaultRegistry())
            .Handle(new GetPrinterSuppliesQuery(switchDevice.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*só se aplica a devices do tipo Printer*");
    }

    [Fact]
    public async Task Supplies_PrinterWithoutIp_ShouldReturnSuppliesWithErrorPtBr()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var printer = await SeedDeviceAsync(context, location.Id, DeviceType.Printer, ip: null);

        var result = await Handler(context, DefaultRegistry())
            .Handle(new GetPrinterSuppliesQuery(printer.Id), CancellationToken.None);

        result.Supplies.Should().HaveCount(2);
        result.Supplies.Should().OnlyContain(s =>
            !s.Succeeded && s.LevelPercent == null && s.Error == "Device sem IP cadastrado.");
    }

    [Fact]
    public async Task Supplies_SnmpFailure_ShouldSurfaceProviderError()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var printer = await SeedDeviceAsync(context, location.Id, DeviceType.Printer, "192.0.2.50");

        var registry = new FakeRegistry(
            new FakeSupplyProvider("SnmpToner", null, "Nenhum toner encontrado na impressora."),
            new FakeSupplyProvider("SnmpDrum", 70, null));

        var result = await Handler(context, registry)
            .Handle(new GetPrinterSuppliesQuery(printer.Id), CancellationToken.None);

        var toner = result.Supplies.Single(s => s.Check == "SnmpToner");
        toner.Succeeded.Should().BeFalse();
        toner.LevelPercent.Should().BeNull();
        toner.Error.Should().Be("Nenhum toner encontrado na impressora.");
    }
}

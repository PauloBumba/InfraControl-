using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Monitoring.Ping;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F5-T003: PingDeviceQuery e GetPingStatusQuery (up/down estilo Zabbix)
/// com repositório real e provider de ping falso.
/// </summary>
public sealed class PingQueriesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public PingQueriesTests()
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
        var location = Location.Create("Sala A");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        return location;
    }

    private static async Task<Device> SeedDeviceAsync(
        AppDbContext context,
        Guid locationId,
        string name,
        string? ip)
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
            string target,
            CancellationToken cancellationToken = default)
        {
            var up = upSelector(target);
            return Task.FromResult(new MonitoringProviderResult(
                up,
                TimeSpan.FromMilliseconds(7),
                up ? null : "Host inacessível."));
        }
    }

    private sealed class FakeRegistry(IMonitoringProvider provider) : IProviderRegistry<IMonitoringProvider>
    {
        public IMonitoringProvider Resolve(string providerKey) =>
            provider.CanHandle(providerKey)
                ? provider
                : throw new InvalidOperationException(
                    $"No provider registered for '{providerKey}'.");

        public IReadOnlyCollection<IMonitoringProvider> GetAll() => [provider];
    }

    private static PingDeviceQueryHandler DeviceHandler(AppDbContext context, IMonitoringProvider provider)
        => new(new DeviceRepository(context), new FakeRegistry(provider));

    private static GetPingStatusQueryHandler StatusHandler(AppDbContext context, IMonitoringProvider provider)
        => new(new DeviceRepository(context), new FakeRegistry(provider));

    [Fact]
    public async Task PingDevice_WithUpDevice_ShouldReturnUpWithLatency()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(context, location.Id, "sw-01", "192.0.2.10");
        var provider = new FakePingProvider(_ => true);

        var result = await DeviceHandler(context, provider)
            .Handle(new PingDeviceQuery(device.Id), CancellationToken.None);

        result.DeviceId.Should().Be(device.Id);
        result.Ip.Should().Be("192.0.2.10");
        result.Up.Should().BeTrue();
        result.LatencyMs.Should().Be(7);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task PingDevice_WithDownDevice_ShouldReturnDownWithErrorPtBr()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(context, location.Id, "sw-02", "192.0.2.20");
        var provider = new FakePingProvider(_ => false);

        var result = await DeviceHandler(context, provider)
            .Handle(new PingDeviceQuery(device.Id), CancellationToken.None);

        result.Up.Should().BeFalse();
        result.Error.Should().Be("Host inacessível.");
    }

    [Fact]
    public async Task PingDevice_WithoutIp_ShouldReturnUpNullWithErrorPtBr()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(context, location.Id, "sw-03", ip: null);
        var provider = new FakePingProvider(_ => true);

        var result = await DeviceHandler(context, provider)
            .Handle(new PingDeviceQuery(device.Id), CancellationToken.None);

        result.Up.Should().BeNull();
        result.LatencyMs.Should().BeNull();
        result.Error.Should().Be("Device sem IP cadastrado.");
    }

    [Fact]
    public async Task PingDevice_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();
        var provider = new FakePingProvider(_ => true);

        var action = () => DeviceHandler(context, provider)
            .Handle(new PingDeviceQuery(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task GetPingStatus_ShouldReturnAllDevicesWithMixedStatus()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var up = await SeedDeviceAsync(context, location.Id, "sw-up", "192.0.2.10");
        var down = await SeedDeviceAsync(context, location.Id, "sw-down", "192.0.2.20");
        var noIp = await SeedDeviceAsync(context, location.Id, "sw-noip", ip: null);
        var provider = new FakePingProvider(ip => ip == "192.0.2.10");

        var results = await StatusHandler(context, provider)
            .Handle(new GetPingStatusQuery(), CancellationToken.None);

        results.Should().HaveCount(3);
        var byId = results.ToDictionary(r => r.DeviceId);
        byId[up.Id].Up.Should().BeTrue();
        byId[up.Id].LatencyMs.Should().Be(7);
        byId[down.Id].Up.Should().BeFalse();
        byId[down.Id].Error.Should().Be("Host inacessível.");
        byId[noIp.Id].Up.Should().BeNull();
        byId[noIp.Id].Error.Should().Be("Device sem IP cadastrado.");
    }

    [Fact]
    public async Task GetPingStatus_ShouldResolveProviderWithPingKey()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        await SeedDeviceAsync(context, location.Id, "sw-01", "192.0.2.10");
        // Resolve lança InvalidOperationException para chave que não seja "Ping".
        var provider = new FakePingProvider(_ => true);

        var act = async () => await StatusHandler(context, provider)
            .Handle(new GetPingStatusQuery(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}

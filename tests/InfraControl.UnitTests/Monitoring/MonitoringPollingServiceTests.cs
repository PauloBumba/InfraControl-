using FluentAssertions;
using InfraControl.Application.Alerts;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Monitoring.Checks;
using InfraControl.Application.Monitoring.Polling;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using InfraControl.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F5-T007: polling periódico — o serviço executa, a cada ciclo, os
/// checks habilitados (MonitoringCheck.Enabled) de todos os devices com
/// IP, em 1 SaveChanges. Decisões aprovadas: serviço + worker fino e
/// honrar a tabela de checks habilitados (não RunAllChecks por kind).
/// </summary>
public sealed class MonitoringPollingServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public MonitoringPollingServiceTests()
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

    private static async Task<Device> SeedDeviceAsync(
        AppDbContext context, string name, string? ip)
    {
        var location = await context.Locations.FirstOrDefaultAsync();
        if (location is null)
        {
            location = Location.Create("Sala Polling");
            context.Locations.Add(location);
            await context.SaveChangesAsync();
        }

        var device = Device.Create(location.Id, name, DeviceType.Switch, ip: ip);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    private static async Task SeedCheckAsync(
        AppDbContext context, Device device, CheckKind kind, bool enabled = true)
    {
        context.MonitoringChecks.Add(MonitoringCheck.Create(device.Id, kind, enabled));
        await context.SaveChangesAsync();
    }

    private sealed class FakePingProvider(Func<string, MonitoringProviderResult> factory)
        : IMonitoringProvider
    {
        public string Name => "Ping";

        public bool CanHandle(string providerKey) =>
            string.Equals(providerKey, "Ping", StringComparison.OrdinalIgnoreCase);

        public Task<MonitoringProviderResult> CheckAsync(
            string target, CancellationToken cancellationToken = default)
            => Task.FromResult(factory(target));
    }

    private static MonitoringProviderResult Up() =>
        new(true, TimeSpan.FromMilliseconds(3));

    private static MonitoringPollingService Service(
        AppDbContext context, params IMonitoringProvider[] providers)
        => new(
            new DeviceRepository(context),
            new MonitoringCheckRepository(context),
            new ProviderRegistry<IMonitoringProvider>(providers),
            new MonitoringResultRepository(context),
            new AlertEvaluator(new AlertRepository(context),
                new MonitoringResultRepository(context),
                new IncidentRepository(context),
                new UpstreamCorrelationRepository(context),
                new RootCauseResolver(new ConnectionRepository(context),
                    new AlertRepository(context), new DeviceRepository(context),
                    new UpstreamCorrelationRepository(context),
                    new IncidentRepository(context))),
            NullLogger<MonitoringPollingService>.Instance);

    [Fact]
    public async Task PollOnce_EnabledChecksOnDevicesWithIp_ShouldExecuteAndPersist()
    {
        await using var context = await CreateContextAsync();
        var sw1 = await SeedDeviceAsync(context, "sw-01", "192.0.2.10");
        var sw2 = await SeedDeviceAsync(context, "sw-02", "192.0.2.11");
        await SeedCheckAsync(context, sw1, CheckKind.Ping);
        await SeedCheckAsync(context, sw2, CheckKind.Ping);

        var executed = await Service(
                context, new FakePingProvider(_ => Up()))
            .PollOnceAsync(CancellationToken.None);

        executed.Should().Be(2);
        var rows = await context.MonitoringResults.ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(row => row.Kind == CheckKind.Ping && row.Succeeded);
    }

    [Fact]
    public async Task PollOnce_DeviceWithoutIp_ShouldSkip()
    {
        await using var context = await CreateContextAsync();
        var noIp = await SeedDeviceAsync(context, "sw-noip", ip: null);
        await SeedCheckAsync(context, noIp, CheckKind.Ping);

        var executed = await Service(context, new FakePingProvider(_ => Up()))
            .PollOnceAsync(CancellationToken.None);

        executed.Should().Be(0);
        (await context.MonitoringResults.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task PollOnce_DisabledCheck_ShouldNotExecute()
    {
        await using var context = await CreateContextAsync();
        var sw = await SeedDeviceAsync(context, "sw-03", "192.0.2.12");
        await SeedCheckAsync(context, sw, CheckKind.Ping, enabled: false);

        var executed = await Service(context, new FakePingProvider(_ => Up()))
            .PollOnceAsync(CancellationToken.None);

        executed.Should().Be(0);
        (await context.MonitoringResults.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task PollOnce_SnmpTonerOnNonPrinter_ShouldSkipWithoutThrowing()
    {
        await using var context = await CreateContextAsync();
        var sw = await SeedDeviceAsync(context, "sw-04", "192.0.2.13");
        await SeedCheckAsync(context, sw, CheckKind.SnmpToner);

        var executed = await Service(context, new FakePingProvider(_ => Up()))
            .PollOnceAsync(CancellationToken.None);

        executed.Should().Be(0);
        (await context.MonitoringResults.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task PollOnce_ProviderThrows_ShouldContinueWithOtherDevices()
    {
        await using var context = await CreateContextAsync();
        var boom = await SeedDeviceAsync(context, "sw-boom", "192.0.2.14");
        var ok = await SeedDeviceAsync(context, "sw-ok", "192.0.2.15");
        await SeedCheckAsync(context, boom, CheckKind.Ping);
        await SeedCheckAsync(context, ok, CheckKind.Ping);

        var executed = await Service(
                context,
                new FakePingProvider(ip => ip == "192.0.2.14"
                    ? throw new TimeoutException("boom")
                    : Up()))
            .PollOnceAsync(CancellationToken.None);

        // O erro do primeiro device não aborta o ciclo nem perde o resto.
        executed.Should().Be(1);
        var rows = await context.MonitoringResults.ToListAsync();
        rows.Should().ContainSingle(row => row.DeviceId == ok.Id && row.Succeeded);
    }

    [Fact]
    public async Task PollOnce_NoEnabledChecks_ShouldReturnZero()
    {
        await using var context = await CreateContextAsync();
        _ = await SeedDeviceAsync(context, "sw-05", "192.0.2.16");

        var executed = await Service(context, new FakePingProvider(_ => Up()))
            .PollOnceAsync(CancellationToken.None);

        executed.Should().Be(0);
        (await context.MonitoringResults.ToListAsync()).Should().BeEmpty();
    }
}

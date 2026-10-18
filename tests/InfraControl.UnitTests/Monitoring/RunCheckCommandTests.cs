using FluentAssertions;
using InfraControl.Application.Alerts;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Monitoring.Checks;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Monitoring;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Services;
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
            new AlertEvaluator(new AlertRepository(context),
                new MonitoringResultRepository(context),
                new IncidentRepository(context),
                new UpstreamCorrelationRepository(context),
                new RootCauseResolver(new ConnectionRepository(context),
                    new AlertRepository(context), new DeviceRepository(context),
                    new UpstreamCorrelationRepository(context),
                    new IncidentRepository(context))),
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

    /// <summary>F30-T004 S2 (docs/52 §S2): SnmpRadio é AccessPoint-only.</summary>
    [Theory]
    [InlineData(DeviceType.Switch)]
    [InlineData(DeviceType.Printer)]
    public async Task RunCheck_SnmpRadio_OnNonAccessPoint_ShouldThrowArgumentPtBrMessage(
        DeviceType nonApType)
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var sw = await SeedDeviceAsync(
            context, location.Id, nonApType, "dev-07", "192.0.2.14");

        var action = () => Handler(context, new FakeRegistry(new FakePingProvider(_ => true)))
            .Handle(new RunCheckCommand(sw.Id, "SnmpRadio"), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*só se aplica a devices do tipo AccessPoint*");
        (await RowsAsync(context, sw.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task RunCheck_SnmpRadio_OnAccessPoint_ShouldExecuteAndPersist()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var ap = await SeedDeviceAsync(
            context, location.Id, DeviceType.AccessPoint, "ap-02", "192.0.2.21");

        var registry = new FakeRegistry(new FakeSupplyProvider("SnmpRadio", 7));
        var result = await Handler(context, registry)
            .Handle(new RunCheckCommand(ap.Id, "SnmpRadio"), CancellationToken.None);

        result.Executed.Should().BeTrue();
        result.Succeeded.Should().BeTrue();

        var rows = await RowsAsync(context, ap.Id);
        rows.Should().ContainSingle();
        rows[0].Kind.Should().Be(CheckKind.SnmpRadio);
    }

    [Fact]
    public async Task RunCheck_HttpWan_ShouldExecuteAndPersistResultWithValues()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var ap = await SeedDeviceAsync(
            context, location.Id, DeviceType.AccessPoint, "ap-01", "192.0.2.20");

        // https falha (ex.: cert self-signed rejeitado) → fallback http 200.
        var probe = new FakeHttpProbeClient(url => url.StartsWith("https")
            ? new HttpProbeReply(false, null, "certificate rejected")
            : new HttpProbeReply(true, 200));
        var registry = new FakeRegistry(new HttpWanMonitoringProvider(probe));

        var result = await Handler(context, registry)
            .Handle(new RunCheckCommand(ap.Id, "HttpWan"), CancellationToken.None);

        result.Executed.Should().BeTrue();
        result.Succeeded.Should().BeTrue();
        result.Values!["scheme"].Should().Be("http");
        result.Values!["status_code"].Should().Be("200");

        var rows = await RowsAsync(context, ap.Id);
        rows.Should().ContainSingle();
        rows[0].Kind.Should().Be(CheckKind.HttpWan);
        rows[0].Succeeded.Should().BeTrue();
        rows[0].Values!["url"].Should().Be("http://192.0.2.20/");
    }

    private sealed class FakeHttpProbeClient(Func<string, HttpProbeReply> factory)
        : IHttpProbeClient
    {
        public Task<HttpProbeReply> SendAsync(
            string url,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
            => Task.FromResult(factory(url));
    }

    [Fact]
    public async Task RunCheck_Tcp_ShouldExecuteAndPersistResultWithPort()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var camera = await SeedDeviceAsync(
            context, location.Id, DeviceType.Camera, "cam-01", "192.0.2.30");

        var probe = new FakeTcpProbeClient(() => new TcpProbeReply(true));
        var registry = new FakeRegistry(new TcpMonitoringProvider(probe));

        var result = await Handler(context, registry)
            .Handle(new RunCheckCommand(camera.Id, "Tcp"), CancellationToken.None);

        result.Executed.Should().BeTrue();
        result.Succeeded.Should().BeTrue();
        result.Values!["port"].Should().Be("37777");

        var rows = await RowsAsync(context, camera.Id);
        rows.Should().ContainSingle();
        rows[0].Kind.Should().Be(CheckKind.Tcp);
        rows[0].Succeeded.Should().BeTrue();
    }

    // ── F6-T001: avaliação pós-check (alerta no mesmo SaveChanges) ──────

    [Fact]
    public async Task RunCheck_PingDown_ShouldOpenCriticalAlertWithResult()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(
            context, location.Id, DeviceType.Switch, "sw-alert", "192.0.2.40");

        var handler = Handler(context, new FakeRegistry(new FakePingProvider(_ => false)));
        for (var i = 0; i < 3; i++)
            await handler.Handle(
                new RunCheckCommand(device.Id, "Ping"), CancellationToken.None);

        var alert = await context.Alerts.AsNoTracking().SingleAsync();
        alert.DeviceId.Should().Be(device.Id);
        alert.Kind.Should().Be(CheckKind.Ping);
        alert.Severity.Should().Be(AlertSeverity.Critical);
        alert.Status.Should().Be(AlertStatus.Open);
        (await RowsAsync(context, device.Id)).Should().HaveCount(3,
            "os resultados do check são persistidos junto com o alerta");
    }

    [Fact]
    public async Task RunCheck_PingDownThenUp_ShouldOpenThenResolveAlert()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(
            context, location.Id, DeviceType.Switch, "sw-rec", "192.0.2.41");

        var handler = Handler(context, new FakeRegistry(new FakePingProvider(_ => false)));
        for (var i = 0; i < 3; i++)
            await handler.Handle(
                new RunCheckCommand(device.Id, "Ping"), CancellationToken.None);
        await Handler(context, new FakeRegistry(new FakePingProvider(_ => true)))
            .Handle(new RunCheckCommand(device.Id, "Ping"), CancellationToken.None);

        var alerts = await context.Alerts.AsNoTracking().ToListAsync();
        alerts.Should().ContainSingle("auto-resolve não cria novo alerta — fecha o mesmo");
        alerts[0].Status.Should().Be(AlertStatus.Resolved);
        alerts[0].ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RunCheck_TonerBelowThreshold_ShouldOpenWarningAlert()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var printer = await SeedDeviceAsync(
            context, location.Id, DeviceType.Printer, "pr-alert", "192.0.2.42");

        var registry = new FakeRegistry(
            new FakeSupplyProvider("SnmpToner", levelPercent: 15));

        await Handler(context, registry)
            .Handle(new RunCheckCommand(printer.Id, "SnmpToner"), CancellationToken.None);

        var alert = await context.Alerts.AsNoTracking().SingleAsync();
        alert.Kind.Should().Be(CheckKind.SnmpToner);
        alert.Severity.Should().Be(AlertSeverity.Warning);
        alert.Message.Should().Contain("15%");
    }

    [Fact]
    public async Task RunCheck_PingDownThrice_ShouldOpenOnlyOnThirdConsecutiveFailure()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(
            context, location.Id, DeviceType.Switch, "sw-streak", "192.0.2.43");

        var handler = Handler(context, new FakeRegistry(new FakePingProvider(_ => false)));

        for (var i = 1; i <= 3; i++)
        {
            await handler.Handle(
                new RunCheckCommand(device.Id, "Ping"), CancellationToken.None);

            var open = await context.Alerts.AsNoTracking()
                .Where(alert => alert.DeviceId == device.Id
                    && alert.Status == AlertStatus.Open)
                .ToListAsync();

            if (i < 3)
                open.Should().BeEmpty($"falha {i} < N=3 não abre alerta");
            else
                open.Should().ContainSingle("3ª falha consecutiva abre o alerta");
        }
    }

    private sealed class FakeTcpProbeClient(Func<TcpProbeReply> factory) : ITcpProbeClient
    {
        public Task<TcpProbeReply> SendAsync(
            string target,
            int port,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
            => Task.FromResult(factory());
    }

    // ── F30-T005a: guard ONVIF (Camera/Nvr) ──────────────────────────────

    private sealed class FakeOnvifProvider(string name) : IMonitoringProvider
    {
        public string Name => name;

        public bool CanHandle(string providerKey) =>
            string.Equals(providerKey, name, StringComparison.OrdinalIgnoreCase);

        public Task<MonitoringProviderResult> CheckAsync(
            string target, CancellationToken cancellationToken = default)
            => Task.FromResult(new MonitoringProviderResult(
                true, TimeSpan.FromMilliseconds(9), null,
                new Dictionary<string, string> { ["usedPercent"] = "10" }));
    }

    /// <summary>OnvifStorage é Camera/Nvr-only — mesmo molde do guard Printer.</summary>
    [Theory]
    [InlineData(DeviceType.Switch)]
    [InlineData(DeviceType.Server)]
    [InlineData(DeviceType.AccessPoint)]
    public async Task RunCheck_OnvifStorage_OnNonCameraNvr_ShouldThrowArgumentPtBrMessage(
        DeviceType nonCameraType)
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(
            context, location.Id, nonCameraType, "dev-08", "192.0.2.15");

        var action = () => Handler(
                context, new FakeRegistry(new FakeOnvifProvider("OnvifStorage")))
            .Handle(new RunCheckCommand(device.Id, "OnvifStorage"), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*só se aplica a devices do tipo Camera ou Nvr*");
        (await RowsAsync(context, device.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task RunCheck_OnvifStorage_OnCamera_ShouldExecuteAndPersist()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var camera = await SeedDeviceAsync(
            context, location.Id, DeviceType.Camera, "cam-02", "192.0.2.31");

        var result = await Handler(
                context, new FakeRegistry(new FakeOnvifProvider("OnvifStorage")))
            .Handle(new RunCheckCommand(camera.Id, "OnvifStorage"), CancellationToken.None);

        result.Executed.Should().BeTrue();
        result.Succeeded.Should().BeTrue();

        var rows = await RowsAsync(context, camera.Id);
        rows.Should().ContainSingle();
        rows[0].Kind.Should().Be(CheckKind.OnvifStorage);
    }

    [Fact]
    public async Task RunCheck_OnvifRecording_OnNvr_ShouldPassGuardAndPersist()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var nvr = await SeedDeviceAsync(
            context, location.Id, DeviceType.Nvr, "nvr-01", "192.0.2.32");

        var result = await Handler(
                context, new FakeRegistry(new FakeOnvifProvider("OnvifRecording")))
            .Handle(new RunCheckCommand(nvr.Id, "OnvifRecording"), CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        var rows = await RowsAsync(context, nvr.Id);
        rows.Should().ContainSingle();
        rows[0].Kind.Should().Be(CheckKind.OnvifRecording);
    }

    // ── F30-T006a: guard Yealink (Phone) ─────────────────────────────────

    /// <summary>YealinkSip é Phone-only — mesmo molde do guard ONVIF.</summary>
    [Theory]
    [InlineData(DeviceType.Switch)]
    [InlineData(DeviceType.Server)]
    [InlineData(DeviceType.Camera)]
    public async Task RunCheck_YealinkSip_OnNonPhone_ShouldThrowArgumentPtBrMessage(
        DeviceType nonPhoneType)
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(
            context, location.Id, nonPhoneType, "dev-09", "192.0.2.20");

        var action = () => Handler(
                context, new FakeRegistry(new FakeOnvifProvider("YealinkSip")))
            .Handle(new RunCheckCommand(device.Id, "YealinkSip"), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*só se aplica a devices do tipo Phone*");
        (await RowsAsync(context, device.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task RunCheck_YealinkSip_OnPhone_ShouldExecuteAndPersist()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var phone = await SeedDeviceAsync(
            context, location.Id, DeviceType.Phone, "phone-01", "192.0.2.33");

        var result = await Handler(
                context, new FakeRegistry(new FakeOnvifProvider("YealinkSip")))
            .Handle(new RunCheckCommand(phone.Id, "YealinkSip"), CancellationToken.None);

        result.Executed.Should().BeTrue();
        result.Succeeded.Should().BeTrue();

        var rows = await RowsAsync(context, phone.Id);
        rows.Should().ContainSingle();
        rows[0].Kind.Should().Be(CheckKind.YealinkSip);
    }

    [Fact]
    public async Task RunCheck_YealinkCallQuality_OnPhone_ShouldPassGuardAndPersist()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var phone = await SeedDeviceAsync(
            context, location.Id, DeviceType.Phone, "phone-02", "192.0.2.34");

        var result = await Handler(
                context, new FakeRegistry(new FakeOnvifProvider("YealinkCallQuality")))
            .Handle(new RunCheckCommand(phone.Id, "YealinkCallQuality"), CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        var rows = await RowsAsync(context, phone.Id);
        rows.Should().ContainSingle();
        rows[0].Kind.Should().Be(CheckKind.YealinkCallQuality);
    }

    // ── F30-T007a: guard SnmpRouterRoutes (Router/Firewall) ──────────────

    /// <summary>SnmpRouterRoutes é Router/Firewall-only — mesmo molde do guard Yealink.</summary>
    [Theory]
    [InlineData(DeviceType.Switch)]
    [InlineData(DeviceType.Server)]
    [InlineData(DeviceType.Camera)]
    [InlineData(DeviceType.Phone)]
    public async Task RunCheck_SnmpRouterRoutes_OnNonRouter_ShouldThrowArgumentPtBrMessage(
        DeviceType nonRouterType)
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(
            context, location.Id, nonRouterType, "dev-10", "192.0.2.21");

        var action = () => Handler(
                context, new FakeRegistry(new FakeOnvifProvider("SnmpRouterRoutes")))
            .Handle(new RunCheckCommand(device.Id, "SnmpRouterRoutes"), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*só se aplica a devices do tipo Router ou Firewall*");
        (await RowsAsync(context, device.Id)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(DeviceType.Router)]
    [InlineData(DeviceType.Firewall)]
    public async Task RunCheck_SnmpRouterRoutes_OnRouterOrFirewall_ShouldPassGuardAndPersist(
        DeviceType routerType)
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(
            context, location.Id, routerType, "rtr-01", "192.0.2.35");

        var result = await Handler(
                context, new FakeRegistry(new FakeOnvifProvider("SnmpRouterRoutes")))
            .Handle(new RunCheckCommand(device.Id, "SnmpRouterRoutes"), CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        var rows = await RowsAsync(context, device.Id);
        rows.Should().ContainSingle();
        rows[0].Kind.Should().Be(CheckKind.SnmpRouterRoutes);
    }

    [Theory]
    [InlineData(DeviceType.Router)]
    [InlineData(DeviceType.Firewall)]
    public async Task RunCheck_SnmpRouterSessions_OnRouterOrFirewall_ShouldPassGuardAndPersist(
        DeviceType routerType)
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(
            context, location.Id, routerType, "rtr-02", "192.0.2.36");

        var result = await Handler(
                context, new FakeRegistry(new FakeOnvifProvider("SnmpRouterSessions")))
            .Handle(new RunCheckCommand(device.Id, "SnmpRouterSessions"), CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        var rows = await RowsAsync(context, device.Id);
        rows.Should().ContainSingle();
        rows[0].Kind.Should().Be(CheckKind.SnmpRouterSessions);
    }

    // ── F30-T007c: guard de saúde estendido (Server+Router+Firewall) ────

    /// <summary>
    /// SnmpServerCpu aceita Server, Router e Firewall (HOST-RESOURCES é
    /// universal — MikroTik expõe os mesmos OIDs); AccessPoint continua
    /// rejeitado.
    /// </summary>
    [Theory]
    [InlineData(DeviceType.Server)]
    [InlineData(DeviceType.Router)]
    [InlineData(DeviceType.Firewall)]
    public async Task RunCheck_SnmpServerCpu_OnServerRouterFirewall_ShouldPassGuardAndPersist(
        DeviceType healthType)
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(
            context, location.Id, healthType, "dev-11", "192.0.2.37");

        var result = await Handler(
                context, new FakeRegistry(new FakeOnvifProvider("SnmpServerCpu")))
            .Handle(new RunCheckCommand(device.Id, "SnmpServerCpu"), CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        var rows = await RowsAsync(context, device.Id);
        rows.Should().ContainSingle();
        rows[0].Kind.Should().Be(CheckKind.SnmpServerCpu);
    }

    [Fact]
    public async Task RunCheck_SnmpServerCpu_OnAccessPoint_ShouldThrowArgumentPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var location = await SeedLocationAsync(context);
        var device = await SeedDeviceAsync(
            context, location.Id, DeviceType.AccessPoint, "ap-01", "192.0.2.38");

        var action = () => Handler(
                context, new FakeRegistry(new FakeOnvifProvider("SnmpServerCpu")))
            .Handle(new RunCheckCommand(device.Id, "SnmpServerCpu"), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*só se aplica a devices do tipo Server, Router ou Firewall*");
        (await RowsAsync(context, device.Id)).Should().BeEmpty();
    }
}

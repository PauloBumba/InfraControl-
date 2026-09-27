using FluentAssertions;
using InfraControl.Application.Alerts;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Services;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Alerts;

/// <summary>
/// F6-T001: avaliação pós-check com thresholds FIXOS em código (decisão do
/// usuário): Ping down = critical; toner &lt; 20% e drum &lt; 10% = warning;
/// auto-resolve quando a condição normaliza; sem Values (erro SNMP) ou
/// kind sem regra = não avalia; não duplica alerta Open.
/// </summary>
public sealed class AlertEvaluatorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AlertEvaluatorTests()
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

    private static AlertEvaluator Evaluator(AppDbContext context)
        => new(new AlertRepository(context), new MonitoringResultRepository(context),
            new IncidentRepository(context), new UpstreamCorrelationRepository(context),
            new RootCauseResolver(new ConnectionRepository(context),
                new AlertRepository(context), new DeviceRepository(context),
                new UpstreamCorrelationRepository(context),
                new IncidentRepository(context)));

    private static async Task<Device> SeedDeviceAsync(
        AppDbContext context, DeviceType type = DeviceType.Switch, string name = "sw-01")
    {
        var location = Location.Create("Sala Alert");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, name, type, ip: "192.0.2.10");
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    private static async Task<List<Alert>> OpenAlertsAsync(
        AppDbContext context, Guid deviceId, CheckKind kind)
        => await context.Alerts.AsNoTracking()
            .Where(alert => alert.DeviceId == deviceId
                && alert.Kind == kind
                && alert.Status == AlertStatus.Open)
            .ToListAsync();

    private static MonitoringResult Result(
        CheckKind kind, bool succeeded,
        IReadOnlyDictionary<string, string>? values = null)
        => MonitoringResult.Create(
            Guid.NewGuid(), kind, succeeded, 5, succeeded ? null : "erro", values);

    private static async Task SeedHistoryAsync(
        AppDbContext context, Guid deviceId, bool succeeded, DateTime at)
    {
        var row = MonitoringResult.Create(
            deviceId, CheckKind.Ping, succeeded, 5, succeeded ? null : "erro");
        context.MonitoringResults.Add(row);
        await context.SaveChangesAsync();

        // SaveChangesAsync sobrescreve CreateDateTime (auditoria) — a
        // ordem do histórico precisa de datas explícitas.
        await context.Database.ExecuteSqlAsync(
            $"UPDATE monitoring_results SET \"CreateDateTime\" = {at} WHERE id = {row.Id}");
    }

    private static DateTime At(int hoursAgo) =>
        new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc).AddHours(-hoursAgo);

    // ── F6-T002: consecutive failures (Ping N=3 via histórico) ───────────

    [Fact]
    public async Task PingThreeConsecutiveFailures_ShouldOpenAlert()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(2));
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(1));

        await Evaluator(context).EvaluateAsync(
            device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, device.Id, CheckKind.Ping);
        open.Should().ContainSingle("3 falhas consecutivas = abre");
        open[0].Message.Should().Contain("consecutivas");
    }

    [Fact]
    public async Task PingTwoConsecutiveFailures_ShouldNotOpenYet()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(1));

        await Evaluator(context).EvaluateAsync(
            device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.Alerts.AsNoTracking().ToListAsync()).Should().BeEmpty(
            "2 falhas < N=3 — ainda não abre");
    }

    [Fact]
    public async Task PingFailureAfterSuccess_ShouldNotOpen()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(2));
        // sucesso mais recente quebra a sequência
        await SeedHistoryAsync(context, device.Id, succeeded: true, At(1));

        await Evaluator(context).EvaluateAsync(
            device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.Alerts.AsNoTracking().ToListAsync()).Should().BeEmpty(
            "sucesso no meio zera a sequência");
    }

    [Fact]
    public async Task PingUp_AfterOpenAlert_ShouldResolveEvenWithFailureHistory()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        context.Alerts.Add(Alert.Create(
            device.Id, CheckKind.Ping, AlertSeverity.Critical, "down"));
        await context.SaveChangesAsync();
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(2));
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(1));

        await Evaluator(context).EvaluateAsync(
            device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: true),
            CancellationToken.None);
        await context.SaveChangesAsync();

        // sucesso resolve independentemente do histórico de falhas — o
        // streak é contado do banco, então o sucesso salvo (pelo handler)
        // é quem zera a sequência no ciclo seguinte.
        (await OpenAlertsAsync(context, device.Id, CheckKind.Ping))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task PingStreak_ShouldCountOnlyThatDeviceHistory()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context, name: "sw-target");
        var other = await SeedDeviceAsync(context, name: "sw-other");
        //2 falhas do OUTRO device não podem contar para o alvo
        await SeedHistoryAsync(context, other.Id, succeeded: false, At(2));
        await SeedHistoryAsync(context, other.Id, succeeded: false, At(1));

        await Evaluator(context).EvaluateAsync(
            device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.Alerts.AsNoTracking().ToListAsync()).Should().BeEmpty(
            "o streak é por device — histórico alheio não conta");
    }

    // ── Ping ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task PingDown_ShouldOpenCriticalAlert()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        // F6-T002: 2 falhas já salvas + atual = streak N=3
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(2));
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(1));

        await Evaluator(context).EvaluateAsync(
            device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, device.Id, CheckKind.Ping);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Critical);
        open[0].Message.Should().Contain(device.Name);
    }

    [Fact]
    public async Task PingUp_WithoutOpenAlert_ShouldDoNothing()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        await Evaluator(context).EvaluateAsync(
            device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: true),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.Alerts.AsNoTracking().ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task PingUp_ShouldResolveOpenAlert()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        context.Alerts.Add(Alert.Create(
            device.Id, CheckKind.Ping, AlertSeverity.Critical, "down"));
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateAsync(
            device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: true),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, device.Id, CheckKind.Ping)).Should().BeEmpty();
        var resolved = await context.Alerts.AsNoTracking()
            .Where(alert => alert.DeviceId == device.Id
                && alert.Status == AlertStatus.Resolved)
            .ToListAsync();
        resolved.Should().ContainSingle();
        resolved[0].ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PingDown_Twice_ShouldNotDuplicateOpenAlert()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(2));
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(1));

        for (var i = 0; i < 2; i++)
        {
            await Evaluator(context).EvaluateAsync(
                device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: false),
                CancellationToken.None);
            await context.SaveChangesAsync();
        }

        (await OpenAlertsAsync(context, device.Id, CheckKind.Ping))
            .Should().ContainSingle();
    }

    // ── Toner / Drum (thresholds fixos) ──────────────────────────────────

    [Fact]
    public async Task TonerBelow20Percent_ShouldOpenWarningAlert()
    {
        await using var context = await CreateContextAsync();
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-01");

        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpToner,
            Result(CheckKind.SnmpToner, true, new Dictionary<string, string>
            {
                ["levelPercent"] = "18",
                ["description"] = "Black cartridge"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, printer.Id, CheckKind.SnmpToner);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Warning);
        open[0].Message.Should().Contain("18%");
    }

    [Fact]
    public async Task TonerAtThreshold_ShouldResolveOpenAlert()
    {
        await using var context = await CreateContextAsync();
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-02");
        context.Alerts.Add(Alert.Create(
            printer.Id, CheckKind.SnmpToner, AlertSeverity.Warning, "toner 18%"));
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpToner,
            Result(CheckKind.SnmpToner, true, new Dictionary<string, string>
            {
                ["levelPercent"] = "25"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, printer.Id, CheckKind.SnmpToner))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task DrumBelow10Percent_ShouldOpenWarningAlert()
    {
        await using var context = await CreateContextAsync();
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-03");

        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpDrum,
            Result(CheckKind.SnmpDrum, true, new Dictionary<string, string>
            {
                ["levelPercent"] = "9"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, printer.Id, CheckKind.SnmpDrum);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Warning);
        open[0].Message.Should().Contain("Drum");
    }

    [Fact]
    public async Task DrumAtThreshold_ShouldResolveOpenAlert()
    {
        await using var context = await CreateContextAsync();
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-04");
        context.Alerts.Add(Alert.Create(
            printer.Id, CheckKind.SnmpDrum, AlertSeverity.Warning, "drum 9%"));
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpDrum,
            Result(CheckKind.SnmpDrum, true, new Dictionary<string, string>
            {
                ["levelPercent"] = "15"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, printer.Id, CheckKind.SnmpDrum))
            .Should().BeEmpty();
    }

    // ── F6-T003: auto-abertura de Incident em Alert Critical ─────────────

    private static async Task<List<Incident>> OpenIncidentsAsync(
        AppDbContext context, Guid deviceId)
        => await context.Incidents.AsNoTracking()
            .Where(incident => incident.DeviceId == deviceId
                && incident.Status == IncidentStatus.Open)
            .ToListAsync();

    [Fact]
    public async Task CriticalPingAlert_ShouldAutoOpenIncidentAndLinkAlert()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(2));
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(1));

        await Evaluator(context).EvaluateAsync(
            device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var alert = await OpenAlertsAsync(context, device.Id, CheckKind.Ping);
        alert.Should().ContainSingle();

        var incident = (await OpenIncidentsAsync(context, device.Id))
            .Should().ContainSingle().Subject;
        incident.Severity.Should().Be(IncidentSeverity.Critical);
        incident.Title.Should().Be(alert[0].Message);

        var link = await context.IncidentAlerts.AsNoTracking()
            .Where(link => link.IncidentId == incident.Id && link.AlertId == alert[0].Id)
            .ToListAsync();
        link.Should().ContainSingle();
    }

    [Fact]
    public async Task WarningAlert_ShouldNotOpenIncident()
    {
        await using var context = await CreateContextAsync();
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-x");

        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpToner,
            Result(CheckKind.SnmpToner, true, new Dictionary<string, string>
            {
                ["levelPercent"] = "5"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.Incidents.AsNoTracking().ToListAsync()).Should().BeEmpty(
            "só Alert Critical abre Incident");
    }

    [Fact]
    public async Task RepeatedFailures_ShouldNotDuplicateIncident()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(3));
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(2));
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(1));

        for (var i = 0; i < 2; i++)
        {
            await Evaluator(context).EvaluateAsync(
                device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: false),
                CancellationToken.None);
            await context.SaveChangesAsync();
        }

        (await OpenIncidentsAsync(context, device.Id)).Should().ContainSingle(
            "1 Incident Open por device (anti-storm)");
    }

    [Fact]
    public async Task OrphanOpenAlert_ShouldGetLinkedToIncidentOnNextEvaluation()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        // alerta Open pré-existente (ex.: criado antes do T003), sem incidente
        var orphan = Alert.Create(
            device.Id, CheckKind.Ping, AlertSeverity.Critical, "down legado");
        context.Alerts.Add(orphan);
        await context.SaveChangesAsync();
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(2));
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(1));

        await Evaluator(context).EvaluateAsync(
            device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var incident = (await OpenIncidentsAsync(context, device.Id))
            .Should().ContainSingle().Subject;
        (await context.IncidentAlerts.AsNoTracking()
            .CountAsync(link => link.IncidentId == incident.Id
                && link.AlertId == orphan.Id)).Should().Be(1);
    }

    // ── F6-T004: supressão topológica (correlation) ──────────────────────

    private static async Task ConnectAsync(
        AppDbContext context, Device from, Device to)
    {
        var nicFrom = NetworkInterface.Create(from.Id, $"eth-{from.Name}");
        var nicTo = NetworkInterface.Create(to.Id, $"eth-{to.Name}");
        context.NetworkInterfaces.AddRange(nicFrom, nicTo);
        await context.SaveChangesAsync();
        context.Connections.Add(Connection.Create(nicFrom.Id, nicTo.Id));
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task DownstreamWithUpstreamIncident_ShouldSuppressAndLinkWithoutOwnIncident()
    {
        await using var context = await CreateContextAsync();
        var up = await SeedDeviceAsync(context, name: "sw-core");
        var down = await SeedDeviceAsync(context, name: "sw-edge");
        await ConnectAsync(context, up, down);
        var rootIncident = Incident.Create(
            "core fora do ar", IncidentSeverity.Critical, deviceId: up.Id);
        context.Incidents.Add(rootIncident);
        await context.SaveChangesAsync();
        await SeedHistoryAsync(context, down.Id, succeeded: false, At(2));
        await SeedHistoryAsync(context, down.Id, succeeded: false, At(1));

        await Evaluator(context).EvaluateAsync(
            down, CheckKind.Ping, Result(CheckKind.Ping, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var suppressed = await context.Alerts.AsNoTracking()
            .Where(alert => alert.DeviceId == down.Id
                && alert.Status == AlertStatus.Suppressed)
            .ToListAsync();
        suppressed.Should().ContainSingle("alerta do downstream é suprimido");
        suppressed[0].Message.Should().Contain(down.Name);

        (await context.IncidentAlerts.AsNoTracking()
            .CountAsync(link => link.IncidentId == rootIncident.Id
                && link.AlertId == suppressed[0].Id)).Should().Be(1,
            "alerta suprimido é vinculado ao incidente raiz");

        (await OpenIncidentsAsync(context, down.Id)).Should().BeEmpty(
            "downstream não cria incidente próprio enquanto suprimido");
    }

    [Fact]
    public async Task OpenAlert_ShouldFlipToSuppressedWhenUpstreamIncidentAppears()
    {
        await using var context = await CreateContextAsync();
        var up = await SeedDeviceAsync(context, name: "sw-core");
        var down = await SeedDeviceAsync(context, name: "sw-edge");
        await ConnectAsync(context, up, down);
        // alerta Open aberto antes (sem upstream) — corrida/F6-T003
        context.Alerts.Add(Alert.Create(
            down.Id, CheckKind.Ping, AlertSeverity.Critical, "down legado"));
        await context.SaveChangesAsync();
        // upstream passa a ter incidente Open
        var rootIncident = Incident.Create(
            "core fora do ar", IncidentSeverity.Critical, deviceId: up.Id);
        context.Incidents.Add(rootIncident);
        await context.SaveChangesAsync();
        await SeedHistoryAsync(context, down.Id, succeeded: false, At(2));
        await SeedHistoryAsync(context, down.Id, succeeded: false, At(1));

        await Evaluator(context).EvaluateAsync(
            down, CheckKind.Ping, Result(CheckKind.Ping, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var all = await context.Alerts.AsNoTracking()
            .Where(alert => alert.DeviceId == down.Id).ToListAsync();
        all.Should().ContainSingle("Open vira Suppressed — não duplica");
        all[0].Status.Should().Be(AlertStatus.Suppressed);
        var flippedId = all[0].Id;
        (await context.IncidentAlerts.AsNoTracking()
            .CountAsync(link => link.IncidentId == rootIncident.Id
                && link.AlertId == flippedId)).Should().Be(1,
            "alerta já aberto ganha vínculo com o incidente raiz");
    }

    [Fact]
    public async Task TonerOnDownstream_ShouldSuppressToo_AllKinds()
    {
        await using var context = await CreateContextAsync();
        var up = await SeedDeviceAsync(context, name: "sw-core");
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-behind");
        await ConnectAsync(context, up, printer);
        var rootIncident = Incident.Create(
            "core fora do ar", IncidentSeverity.Critical, deviceId: up.Id);
        context.Incidents.Add(rootIncident);
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpToner,
            Result(CheckKind.SnmpToner, true, new Dictionary<string, string>
            {
                ["levelPercent"] = "18"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var alerts = await context.Alerts.AsNoTracking()
            .Where(alert => alert.DeviceId == printer.Id).ToListAsync();
        alerts.Should().ContainSingle("decisão: TODOS os kinds do downstream são suprimidos");
        alerts[0].Status.Should().Be(AlertStatus.Suppressed);
        (await OpenIncidentsAsync(context, printer.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task PingUp_ShouldResolveSuppressedAlert()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        var suppressed = Alert.Create(
            device.Id, CheckKind.Ping, AlertSeverity.Critical, "down");
        suppressed.Suppress();
        context.Alerts.Add(suppressed);
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateAsync(
            device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: true),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var resolved = await context.Alerts.AsNoTracking()
            .Where(alert => alert.DeviceId == device.Id
                && alert.Status == AlertStatus.Resolved)
            .ToListAsync();
        resolved.Should().ContainSingle("condição normalizada resolve Suppressed também");
        resolved[0].ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpstreamCleared_ShouldPromoteSuppressedToOpenWithOwnIncident()
    {
        await using var context = await CreateContextAsync();
        var up = await SeedDeviceAsync(context, name: "sw-core");
        var down = await SeedDeviceAsync(context, name: "sw-edge");
        await ConnectAsync(context, up, down);
        // incidente raiz foi fechado (F6-T006/recovery manual)
        var rootIncident = Incident.Create(
            "core normalizou", IncidentSeverity.Critical, deviceId: up.Id);
        rootIncident.ChangeStatus(IncidentStatus.Resolved);
        context.Incidents.Add(rootIncident);
        // alerta do downstream ainda suprimido
        var suppressed = Alert.Create(
            down.Id, CheckKind.Ping, AlertSeverity.Critical, "edge continua down");
        suppressed.Suppress();
        context.Alerts.Add(suppressed);
        await context.SaveChangesAsync();
        await SeedHistoryAsync(context, down.Id, succeeded: false, At(2));
        await SeedHistoryAsync(context, down.Id, succeeded: false, At(1));

        await Evaluator(context).EvaluateAsync(
            down, CheckKind.Ping, Result(CheckKind.Ping, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, down.Id, CheckKind.Ping);
        open.Should().ContainSingle("sem supressor, Suppressed promove a Open");
        (await OpenIncidentsAsync(context, down.Id))
            .Should().ContainSingle("e o Critical volta a abrir incidente próprio");
    }

    [Fact]
    public async Task NeighborWithOnlyWarningAlert_NoIncident_ShouldNotSuppress()
    {
        await using var context = await CreateContextAsync();
        var up = await SeedDeviceAsync(context, name: "sw-up");
        var down = await SeedDeviceAsync(context, name: "sw-down");
        await ConnectAsync(context, up, down);
        // vizinho com alerta Open Warning (toner) — SEM incidente
        context.Alerts.Add(Alert.Create(
            up.Id, CheckKind.SnmpToner, AlertSeverity.Warning, "toner 15%"));
        await context.SaveChangesAsync();
        await SeedHistoryAsync(context, down.Id, succeeded: false, At(2));
        await SeedHistoryAsync(context, down.Id, succeeded: false, At(1));

        await Evaluator(context).EvaluateAsync(
            down, CheckKind.Ping, Result(CheckKind.Ping, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, down.Id, CheckKind.Ping);
        open.Should().ContainSingle(
            "gatilho de supressão é Incident Open no upstream, não qualquer alerta");
        (await OpenIncidentsAsync(context, down.Id)).Should().ContainSingle();
    }

    // ── F6-T005: causa provável do incidente ─────────────────────────────

    [Fact]
    public async Task AutoOpenedIncident_ShouldGetLocalRootCause()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context); // sem conexões
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(2));
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(1));

        await Evaluator(context).EvaluateAsync(
            device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var incident = (await OpenIncidentsAsync(context, device.Id))
            .Should().ContainSingle().Subject;
        incident.RootCause.Should().Be(
            "Falha local no device (energia, processo ou enlace direto).");
    }

    [Fact]
    public async Task RootCause_ShouldUpdateToUpstreamAfterRace()
    {
        await using var context = await CreateContextAsync();
        var up = await SeedDeviceAsync(context, name: "sw-core");
        var down = await SeedDeviceAsync(context, name: "sw-edge");
        await ConnectAsync(context, up, down);
        await SeedHistoryAsync(context, down.Id, succeeded: false, At(2));
        await SeedHistoryAsync(context, down.Id, succeeded: false, At(1));

        // ciclo1: downstream abre incidente antes do upstream (corrida)
        await Evaluator(context).EvaluateAsync(
            down, CheckKind.Ping, Result(CheckKind.Ping, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var before = (await OpenIncidentsAsync(context, down.Id))
            .Should().ContainSingle().Subject;
        before.RootCause.Should().Be(
            "Falha local no device (energia, processo ou enlace direto).",
            "upstream ainda sem incidente no ciclo1");

        // upstream ganha incidente (F6-T003) e é salvo
        context.Incidents.Add(Incident.Create(
            "core down", IncidentSeverity.Critical, deviceId: up.Id));
        await context.SaveChangesAsync();

        // ciclo2: avaliação do downstream recalcula a causa (RefreshAsync)
        await Evaluator(context).EvaluateAsync(
            down, CheckKind.Ping, Result(CheckKind.Ping, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var refreshed = await context.Incidents.AsNoTracking()
            .FirstAsync(incident => incident.Id == before.Id);
        refreshed.RootCause.Should().Contain("upstream 'sw-core'",
            "corrida coberta: causa passa a apontar o upstream no ciclo seguinte");
    }

    // ── F6-T006: recovery fecha Incident quando tudo normalizou ──────────

    [Fact]
    public async Task PingUp_ShouldCloseIncidentWhenAllLinkedAlertsResolved()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        var alert = Alert.Create(
            device.Id, CheckKind.Ping, AlertSeverity.Critical, "down");
        context.Alerts.Add(alert);
        var incident = Incident.Create(
            "fora do ar", IncidentSeverity.Critical, deviceId: device.Id);
        context.Incidents.Add(incident);
        await context.SaveChangesAsync();
        context.IncidentAlerts.Add(IncidentAlert.Create(incident.Id, alert.Id));
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateAsync(
            device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: true),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var closed = await context.Incidents.AsNoTracking()
            .FirstAsync(i => i.Id == incident.Id);
        closed.Status.Should().Be(IncidentStatus.Resolved);
        closed.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task IncidentStaysOpen_WhileDownstreamSuppressedAlertStillActive()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context, name: "sw-root");
        var down = await SeedDeviceAsync(context, name: "sw-edge");
        var rootAlert = Alert.Create(
            device.Id, CheckKind.Ping, AlertSeverity.Critical, "root down");
        var downAlert = Alert.Create(
            down.Id, CheckKind.Ping, AlertSeverity.Critical, "edge down");
        downAlert.Suppress(); // vítima ainda sem resposta
        context.Alerts.AddRange(rootAlert, downAlert);
        var incident = Incident.Create(
            "root down", IncidentSeverity.Critical, deviceId: device.Id);
        context.Incidents.Add(incident);
        await context.SaveChangesAsync();
        context.IncidentAlerts.AddRange(
            IncidentAlert.Create(incident.Id, rootAlert.Id),
            IncidentAlert.Create(incident.Id, downAlert.Id));
        await context.SaveChangesAsync();

        // raiz volta: só o alerta do root resolve
        await Evaluator(context).EvaluateAsync(
            device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: true),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var stillOpen = await context.Incidents.AsNoTracking()
            .FirstAsync(i => i.Id == incident.Id);
        stillOpen.Status.Should().Be(IncidentStatus.Open,
            "vínculo com alerta ainda ativo mantém o incidente aberto");

        // downstream normaliza: resolve o Suppressed → todos resolved
        await Evaluator(context).EvaluateAsync(
            down, CheckKind.Ping, Result(CheckKind.Ping, succeeded: true),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var closed = await context.Incidents.AsNoTracking()
            .FirstAsync(i => i.Id == incident.Id);
        closed.Status.Should().Be(IncidentStatus.Resolved,
            "último vínculo resolved → recovery fecha o incidente raiz");
        closed.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task IncidentWithoutLinkedAlerts_ShouldNotAutoClose()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        var alert = Alert.Create(
            device.Id, CheckKind.Ping, AlertSeverity.Critical, "down");
        context.Alerts.Add(alert);
        // incidente manual sem vínculo de alerts
        var manual = Incident.Create("investigação manual", IncidentSeverity.Low);
        context.Incidents.Add(manual);
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateAsync(
            device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: true),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var kept = await context.Incidents.AsNoTracking()
            .FirstAsync(i => i.Id == manual.Id);
        kept.Status.Should().Be(IncidentStatus.Open,
            "sem vínculos (manual) — fecha só por comando, não por recovery");
    }

    // ── casos que NÃO avaliam ────────────────────────────────────────────

    [Fact]
    public async Task TonerWithoutValues_ShouldNotEvaluate()
    {
        await using var context = await CreateContextAsync();
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-05");

        // erro de SNMP: Succeeded=false e Values=null → nível desconhecido
        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpToner,
            Result(CheckKind.SnmpToner, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.Alerts.AsNoTracking().ToListAsync()).Should().BeEmpty(
            "erro de SNMP não é nível baixo — não abre alerta de suprimento");
    }

    [Fact]
    public async Task KindWithoutRule_ShouldNotEvaluate()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        await Evaluator(context).EvaluateAsync(
            device, CheckKind.SnmpSystem,
            Result(CheckKind.SnmpSystem, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.Alerts.AsNoTracking().ToListAsync()).Should().BeEmpty(
            "SnmpSystem não tem regra em F6-T001 (só Ping/toner/drum)");
    }

    [Fact]
    public async Task Thresholds_AreFixedInCode_Toner20_Drum10()
    {
        AlertThresholds.TonerMinPercent.Should().Be(20);
        AlertThresholds.DrumMinPercent.Should().Be(10);
    }
}

using FluentAssertions;
using InfraControl.Application.Alerts;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
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

    // ── F30-T002a: color toner (C/M/Y) ──────────────────────────────────

    [Fact]
    public async Task ColorTonerWorstBelow20Percent_ShouldOpenWarningAlert()
    {
        await using var context = await CreateContextAsync();
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-cmy");

        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpColorToner,
            Result(CheckKind.SnmpColorToner, true, new Dictionary<string, string>
            {
                ["cyanPercent"] = "60",
                ["magentaPercent"] = "12",
                ["yellowPercent"] = "80",
                ["worstPercent"] = "12",
                ["worstColor"] = "magenta"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, printer.Id, CheckKind.SnmpColorToner);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Warning);
        open[0].Message.Should().Contain("12%");
        open[0].Message.Should().Contain("magenta");
    }

    [Fact]
    public async Task ColorTonerAboveThreshold_ShouldResolveOpenAlert()
    {
        await using var context = await CreateContextAsync();
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-cmy-2");
        context.Alerts.Add(Alert.Create(
            printer.Id, CheckKind.SnmpColorToner, AlertSeverity.Warning, "magenta 12%"));
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpColorToner,
            Result(CheckKind.SnmpColorToner, true, new Dictionary<string, string>
            {
                ["cyanPercent"] = "60",
                ["magentaPercent"] = "55",
                ["yellowPercent"] = "80",
                ["worstPercent"] = "55",
                ["worstColor"] = "magenta"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, printer.Id, CheckKind.SnmpColorToner))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ColorTonerWithoutWorstPercent_ShouldNotEvaluate()
    {
        await using var context = await CreateContextAsync();
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-cmy-3");

        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpColorToner,
            Result(CheckKind.SnmpColorToner, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, printer.Id, CheckKind.SnmpColorToner))
            .Should().BeEmpty("erro de SNMP não avalia nível");
    }

    // ── F30-T002b: printer status (bandejas/tampa/alertas) ──────────────

    [Fact]
    public async Task PrinterJammed_ShouldOpenCriticalAlert()
    {
        await using var context = await CreateContextAsync();
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-st-1");

        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpPrinterStatus,
            Result(CheckKind.SnmpPrinterStatus, true, new Dictionary<string, string>
            {
                ["inputCount"] = "2",
                ["inputEmptyCount"] = "0",
                ["inputJammedCount"] = "1",
                ["coverOpen"] = "false",
                ["activeAlertCount"] = "0"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, printer.Id, CheckKind.SnmpPrinterStatus);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Critical);
        open[0].Message.Should().Contain("Atolamento");
    }

    [Fact]
    public async Task AllTraysEmpty_ShouldOpenCriticalAlert()
    {
        await using var context = await CreateContextAsync();
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-st-2");

        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpPrinterStatus,
            Result(CheckKind.SnmpPrinterStatus, true, new Dictionary<string, string>
            {
                ["inputCount"] = "2",
                ["inputEmptyCount"] = "2",
                ["inputJammedCount"] = "0",
                ["coverOpen"] = "false",
                ["activeAlertCount"] = "0"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, printer.Id, CheckKind.SnmpPrinterStatus);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Critical);
        open[0].Message.Should().Contain("vazias");
    }

    [Fact]
    public async Task OneTrayEmpty_ShouldOpenWarningAlert()
    {
        await using var context = await CreateContextAsync();
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-st-3");

        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpPrinterStatus,
            Result(CheckKind.SnmpPrinterStatus, true, new Dictionary<string, string>
            {
                ["inputCount"] = "2",
                ["inputEmptyCount"] = "1",
                ["inputJammedCount"] = "0",
                ["coverOpen"] = "false",
                ["activeAlertCount"] = "0"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, printer.Id, CheckKind.SnmpPrinterStatus);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Warning);
        open[0].Message.Should().Contain("bandeja");
    }

    [Fact]
    public async Task CoverOpen_ShouldOpenWarningAlert()
    {
        await using var context = await CreateContextAsync();
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-st-4");

        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpPrinterStatus,
            Result(CheckKind.SnmpPrinterStatus, true, new Dictionary<string, string>
            {
                ["inputCount"] = "1",
                ["inputEmptyCount"] = "0",
                ["inputJammedCount"] = "0",
                ["coverOpen"] = "true",
                ["activeAlertCount"] = "0"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, printer.Id, CheckKind.SnmpPrinterStatus);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Warning);
        open[0].Message.Should().Contain("Tampa");
    }

    [Fact]
    public async Task ActiveAlertsWithoutSpecificCondition_ShouldOpenWarningAlert()
    {
        await using var context = await CreateContextAsync();
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-st-5");

        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpPrinterStatus,
            Result(CheckKind.SnmpPrinterStatus, true, new Dictionary<string, string>
            {
                ["inputCount"] = "1",
                ["inputEmptyCount"] = "0",
                ["inputJammedCount"] = "0",
                ["coverOpen"] = "false",
                ["activeAlertCount"] = "2"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, printer.Id, CheckKind.SnmpPrinterStatus);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Warning);
        open[0].Message.Should().Contain("2");
    }

    [Fact]
    public async Task HealthyStatus_ShouldResolveOpenAlert()
    {
        await using var context = await CreateContextAsync();
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-st-6");
        context.Alerts.Add(Alert.Create(
            printer.Id, CheckKind.SnmpPrinterStatus, AlertSeverity.Warning, "tampa aberta"));
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpPrinterStatus,
            Result(CheckKind.SnmpPrinterStatus, true, new Dictionary<string, string>
            {
                ["inputCount"] = "1",
                ["inputEmptyCount"] = "0",
                ["inputJammedCount"] = "0",
                ["coverOpen"] = "false",
                ["activeAlertCount"] = "0"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, printer.Id, CheckKind.SnmpPrinterStatus))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task PrinterStatusWithoutValues_ShouldNotEvaluate()
    {
        await using var context = await CreateContextAsync();
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-st-7");

        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpPrinterStatus,
            Result(CheckKind.SnmpPrinterStatus, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, printer.Id, CheckKind.SnmpPrinterStatus))
            .Should().BeEmpty("erro de SNMP não avalia estado");
    }

    [Fact]
    public async Task JamBeatsCoverOpen_ShouldOpenCriticalNotWarning()
    {
        await using var context = await CreateContextAsync();
        var printer = await SeedDeviceAsync(context, DeviceType.Printer, "pr-st-8");

        await Evaluator(context).EvaluateAsync(
            printer, CheckKind.SnmpPrinterStatus,
            Result(CheckKind.SnmpPrinterStatus, true, new Dictionary<string, string>
            {
                ["inputCount"] = "1",
                ["inputEmptyCount"] = "0",
                ["inputJammedCount"] = "1",
                ["coverOpen"] = "true",
                ["activeAlertCount"] = "1"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, printer.Id, CheckKind.SnmpPrinterStatus);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Critical,
            "atolamento é a condição mais grave e prevalece");
        open[0].Message.Should().Contain("Atolamento");
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

    // ── ADR-027: incidente não-Resolved (bugfix do ciclo) ────────────────

    [Fact]
    public async Task FailingCheck_WithIncidentInvestigating_ShouldReuseInsteadOfCreatingSecond()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        var incident = Incident.Create(
            "fora do ar", IncidentSeverity.Critical, deviceId: device.Id);
        context.Incidents.Add(incident);
        await context.SaveChangesAsync();
        incident.ChangeStatus(IncidentStatus.Investigating);
        await context.SaveChangesAsync();

        // check continua falhando: streak completo + falha atual
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(2));
        await SeedHistoryAsync(context, device.Id, succeeded: false, At(1));

        await Evaluator(context).EvaluateAsync(
            device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.Incidents.CountAsync()).Should().Be(1,
            "incidente em Investigating é não-Resolved — reutiliza, não abre o 2º");
        (await context.Alerts.AsNoTracking().CountAsync()).Should().Be(1);
        (await context.IncidentAlerts.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task PingUp_ShouldCloseIncidentEvenWhenInvestigating()
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
        incident.ChangeStatus(IncidentStatus.Investigating);
        await context.SaveChangesAsync();
        context.IncidentAlerts.Add(IncidentAlert.Create(incident.Id, alert.Id));
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateAsync(
            device, CheckKind.Ping, Result(CheckKind.Ping, succeeded: true),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var closed = await context.Incidents.AsNoTracking()
            .FirstAsync(i => i.Id == incident.Id);
        closed.Status.Should().Be(IncidentStatus.Resolved,
            "recovery fecha qualquer estado != Resolved, não só Open");
        closed.ResolvedAt.Should().NotBeNull();
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

    // ── F30-T003a: SnmpServerCpu (hrProcessorLoad, pior core) ──────────

    [Fact]
    public async Task ServerCpuAbove90Percent_ShouldOpenCriticalAlert()
    {
        await using var context = await CreateContextAsync();
        var server = await SeedDeviceAsync(context, DeviceType.Server, "srv-cpu-1");

        await Evaluator(context).EvaluateAsync(
            server, CheckKind.SnmpServerCpu,
            Result(CheckKind.SnmpServerCpu, true, new Dictionary<string, string>
            {
                ["cpuPercent"] = "95",
                ["averagePercent"] = "90",
                ["coreCount"] = "4"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, server.Id, CheckKind.SnmpServerCpu);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Critical);
        open[0].Message.Should().Contain("CPU").And.Contain("95");
    }

    [Fact]
    public async Task ServerCpuAt85Percent_ShouldOpenWarningAlert()
    {
        await using var context = await CreateContextAsync();
        var server = await SeedDeviceAsync(context, DeviceType.Server, "srv-cpu-2");

        await Evaluator(context).EvaluateAsync(
            server, CheckKind.SnmpServerCpu,
            Result(CheckKind.SnmpServerCpu, true, new Dictionary<string, string>
            {
                ["cpuPercent"] = "85"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, server.Id, CheckKind.SnmpServerCpu);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Warning);
    }

    [Fact]
    public async Task ServerCpuBelowThreshold_ShouldResolveOpenAlert()
    {
        await using var context = await CreateContextAsync();
        var server = await SeedDeviceAsync(context, DeviceType.Server, "srv-cpu-3");
        context.Alerts.Add(Alert.Create(
            server.Id, CheckKind.SnmpServerCpu, AlertSeverity.Critical, "CPU 95%"));
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateAsync(
            server, CheckKind.SnmpServerCpu,
            Result(CheckKind.SnmpServerCpu, true, new Dictionary<string, string>
            {
                ["cpuPercent"] = "42"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, server.Id, CheckKind.SnmpServerCpu))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ServerCpuWithoutCpuPercent_ShouldNotEvaluate()
    {
        await using var context = await CreateContextAsync();
        var server = await SeedDeviceAsync(context, DeviceType.Server, "srv-cpu-4");

        await Evaluator(context).EvaluateAsync(
            server, CheckKind.SnmpServerCpu,
            Result(CheckKind.SnmpServerCpu, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, server.Id, CheckKind.SnmpServerCpu))
            .Should().BeEmpty("erro de SNMP não avalia CPU");
    }

    // ── F30-T003b: SnmpServerMemory (hrStorageTable RAM) ────────────────

    [Fact]
    public async Task ServerMemoryAbove90Percent_ShouldOpenCriticalAlert()
    {
        await using var context = await CreateContextAsync();
        var server = await SeedDeviceAsync(context, DeviceType.Server, "srv-mem-1");

        await Evaluator(context).EvaluateAsync(
            server, CheckKind.SnmpServerMemory,
            Result(CheckKind.SnmpServerMemory, true, new Dictionary<string, string>
            {
                ["usedPercent"] = "95",
                ["totalMB"] = "8192"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, server.Id, CheckKind.SnmpServerMemory);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Critical);
        open[0].Message.Should().Contain("Memória").And.Contain("95");
    }

    [Fact]
    public async Task ServerMemoryAt85Percent_ShouldOpenWarningAlert()
    {
        await using var context = await CreateContextAsync();
        var server = await SeedDeviceAsync(context, DeviceType.Server, "srv-mem-2");

        await Evaluator(context).EvaluateAsync(
            server, CheckKind.SnmpServerMemory,
            Result(CheckKind.SnmpServerMemory, true, new Dictionary<string, string>
            {
                ["usedPercent"] = "85"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, server.Id, CheckKind.SnmpServerMemory);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Warning);
    }

    [Fact]
    public async Task ServerMemoryBelowThreshold_ShouldResolveOpenAlert()
    {
        await using var context = await CreateContextAsync();
        var server = await SeedDeviceAsync(context, DeviceType.Server, "srv-mem-3");
        context.Alerts.Add(Alert.Create(
            server.Id, CheckKind.SnmpServerMemory, AlertSeverity.Critical, "Memória 95%"));
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateAsync(
            server, CheckKind.SnmpServerMemory,
            Result(CheckKind.SnmpServerMemory, true, new Dictionary<string, string>
            {
                ["usedPercent"] = "40"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, server.Id, CheckKind.SnmpServerMemory))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ServerMemoryWithoutUsedPercent_ShouldNotEvaluate()
    {
        // Fallback hrMemorySize devolve total sem usado — informativo, sem regra.
        await using var context = await CreateContextAsync();
        var server = await SeedDeviceAsync(context, DeviceType.Server, "srv-mem-4");

        await Evaluator(context).EvaluateAsync(
            server, CheckKind.SnmpServerMemory,
            Result(CheckKind.SnmpServerMemory, true, new Dictionary<string, string>
            {
                ["totalMB"] = "2048"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, server.Id, CheckKind.SnmpServerMemory))
            .Should().BeEmpty("sem usedPercent não avalia");
    }

    // ── F30-T003c: SnmpServerDisk (pior partição) ───────────────────────

    [Fact]
    public async Task DiskWorstAbove90Percent_ShouldOpenCriticalAlert()
    {
        await using var context = await CreateContextAsync();
        var server = await SeedDeviceAsync(context, DeviceType.Server, "srv-disk-1");

        await Evaluator(context).EvaluateAsync(
            server, CheckKind.SnmpServerDisk,
            Result(CheckKind.SnmpServerDisk, true, new Dictionary<string, string>
            {
                ["worstPercent"] = "95",
                ["worstDisk"] = "/",
                ["diskCount"] = "2"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, server.Id, CheckKind.SnmpServerDisk);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Critical);
        open[0].Message.Should().Contain("Disco").And.Contain("95");
    }

    [Fact]
    public async Task DiskWorstAt85Percent_ShouldOpenWarningAlert()
    {
        await using var context = await CreateContextAsync();
        var server = await SeedDeviceAsync(context, DeviceType.Server, "srv-disk-2");

        await Evaluator(context).EvaluateAsync(
            server, CheckKind.SnmpServerDisk,
            Result(CheckKind.SnmpServerDisk, true, new Dictionary<string, string>
            {
                ["worstPercent"] = "85",
                ["worstDisk"] = "/var"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, server.Id, CheckKind.SnmpServerDisk);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Warning);
    }

    [Fact]
    public async Task DiskWorstBelowThreshold_ShouldResolveOpenAlert()
    {
        await using var context = await CreateContextAsync();
        var server = await SeedDeviceAsync(context, DeviceType.Server, "srv-disk-3");
        context.Alerts.Add(Alert.Create(
            server.Id, CheckKind.SnmpServerDisk, AlertSeverity.Critical, "Disco 95%"));
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateAsync(
            server, CheckKind.SnmpServerDisk,
            Result(CheckKind.SnmpServerDisk, true, new Dictionary<string, string>
            {
                ["worstPercent"] = "30",
                ["worstDisk"] = "/"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, server.Id, CheckKind.SnmpServerDisk))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task DiskWithoutWorstPercent_ShouldNotEvaluate()
    {
        await using var context = await CreateContextAsync();
        var server = await SeedDeviceAsync(context, DeviceType.Server, "srv-disk-4");

        await Evaluator(context).EvaluateAsync(
            server, CheckKind.SnmpServerDisk,
            Result(CheckKind.SnmpServerDisk, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, server.Id, CheckKind.SnmpServerDisk))
            .Should().BeEmpty("erro de SNMP não avalia disco");
    }

    // ── F30-T005a: OnvifStorage (armazenamento da câmera/NVR, docs/46 §C) ──

    [Fact]
    public async Task OnvifStorageAtOrAbove90_ShouldOpenCriticalAlert()
    {
        await using var context = await CreateContextAsync();
        var camera = await SeedDeviceAsync(context, DeviceType.Camera, "cam-storage-1");

        await Evaluator(context).EvaluateAsync(
            camera, CheckKind.OnvifStorage,
            Result(CheckKind.OnvifStorage, true, new Dictionary<string, string>
            {
                ["usedPercent"] = "95",
                ["totalMB"] = "102400"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, camera.Id, CheckKind.OnvifStorage);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Critical);
        open[0].Message.Should().Contain("95%")
            .And.Contain("crítico ≥ 90%");
    }

    [Fact]
    public async Task OnvifStorageBetween80And90_ShouldOpenWarningAlert()
    {
        await using var context = await CreateContextAsync();
        var camera = await SeedDeviceAsync(context, DeviceType.Camera, "cam-storage-2");

        await Evaluator(context).EvaluateAsync(
            camera, CheckKind.OnvifStorage,
            Result(CheckKind.OnvifStorage, true, new Dictionary<string, string>
            {
                ["usedPercent"] = "84"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, camera.Id, CheckKind.OnvifStorage);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Warning);
        open[0].Message.Should().Contain("limite 80%");
    }

    [Fact]
    public async Task OnvifStorageBelowWarning_ShouldResolveActiveAlert()
    {
        await using var context = await CreateContextAsync();
        var camera = await SeedDeviceAsync(context, DeviceType.Camera, "cam-storage-3");
        context.Alerts.Add(Alert.Create(
            camera.Id, CheckKind.OnvifStorage, AlertSeverity.Critical, "aberto antes"));
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateAsync(
            camera, CheckKind.OnvifStorage,
            Result(CheckKind.OnvifStorage, true, new Dictionary<string, string>
            {
                ["usedPercent"] = "42"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, camera.Id, CheckKind.OnvifStorage))
            .Should().BeEmpty("abaixo do limite resolve o alerta ativo");
    }

    [Fact]
    public async Task OnvifStorageWithoutUsedPercent_ShouldNotEvaluate()
    {
        await using var context = await CreateContextAsync();
        var camera = await SeedDeviceAsync(context, DeviceType.Camera, "cam-storage-4");

        await Evaluator(context).EvaluateAsync(
            camera, CheckKind.OnvifStorage,
            Result(CheckKind.OnvifStorage, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, camera.Id, CheckKind.OnvifStorage))
            .Should().BeEmpty("falha do ONVIF não avalia armazenamento");
    }

    // ── F30-T005b: OnvifRecording (gravação/video loss/tamper, docs/46 §B) ──

    private static Dictionary<string, string> Recording(
        string? recordingActive = null,
        string? videoLoss = null,
        string? tamper = null)
    {
        var values = new Dictionary<string, string>();
        if (recordingActive is not null) values["recordingActive"] = recordingActive;
        if (videoLoss is not null) values["videoLoss"] = videoLoss;
        if (tamper is not null) values["tamper"] = tamper;
        return values;
    }

    [Fact]
    public async Task OnvifRecordingStopped_ShouldOpenCriticalAlert()
    {
        await using var context = await CreateContextAsync();
        var camera = await SeedDeviceAsync(context, DeviceType.Camera, "cam-rec-1");

        await Evaluator(context).EvaluateAsync(
            camera, CheckKind.OnvifRecording,
            Result(CheckKind.OnvifRecording, true, Recording(recordingActive: "false")),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, camera.Id, CheckKind.OnvifRecording);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Critical);
        open[0].Message.Should().Contain("Gravação parada");
    }

    [Fact]
    public async Task OnvifVideoLoss_ShouldOpenCriticalAlert()
    {
        await using var context = await CreateContextAsync();
        var camera = await SeedDeviceAsync(context, DeviceType.Camera, "cam-rec-2");

        await Evaluator(context).EvaluateAsync(
            camera, CheckKind.OnvifRecording,
            Result(CheckKind.OnvifRecording, true,
                Recording(recordingActive: "true", videoLoss: "true")),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, camera.Id, CheckKind.OnvifRecording);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Critical);
        open[0].Message.Should().Contain("Perda de vídeo");
    }

    [Fact]
    public async Task OnvifTamper_ShouldOpenWarningAlert()
    {
        await using var context = await CreateContextAsync();
        var camera = await SeedDeviceAsync(context, DeviceType.Camera, "cam-rec-3");

        await Evaluator(context).EvaluateAsync(
            camera, CheckKind.OnvifRecording,
            Result(CheckKind.OnvifRecording, true,
                Recording(recordingActive: "true", tamper: "true")),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, camera.Id, CheckKind.OnvifRecording);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Warning);
        open[0].Message.Should().Contain("Tamper");
    }

    [Fact]
    public async Task OnvifRecordingHealthy_ShouldResolveActiveAlert()
    {
        await using var context = await CreateContextAsync();
        var camera = await SeedDeviceAsync(context, DeviceType.Camera, "cam-rec-4");
        context.Alerts.Add(Alert.Create(
            camera.Id, CheckKind.OnvifRecording, AlertSeverity.Critical, "aberto antes"));
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateAsync(
            camera, CheckKind.OnvifRecording,
            Result(CheckKind.OnvifRecording, true,
                Recording(recordingActive: "true", videoLoss: "false", tamper: "false")),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, camera.Id, CheckKind.OnvifRecording))
            .Should().BeEmpty("estado saudável resolve o alerta ativo");
    }

    [Fact]
    public async Task OnvifRecordingStopped_ShouldPrevailOverTamper()
    {
        await using var context = await CreateContextAsync();
        var camera = await SeedDeviceAsync(context, DeviceType.Camera, "cam-rec-5");

        await Evaluator(context).EvaluateAsync(
            camera, CheckKind.OnvifRecording,
            Result(CheckKind.OnvifRecording, true,
                Recording(recordingActive: "false", videoLoss: "true", tamper: "true")),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, camera.Id, CheckKind.OnvifRecording);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Critical);
        open[0].Message.Should().Contain("Gravação parada", "pior condição prevalece");
    }

    [Fact]
    public async Task OnvifRecordingWithoutValues_ShouldNotEvaluate()
    {
        await using var context = await CreateContextAsync();
        var camera = await SeedDeviceAsync(context, DeviceType.Camera, "cam-rec-6");

        await Evaluator(context).EvaluateAsync(
            camera, CheckKind.OnvifRecording,
            Result(CheckKind.OnvifRecording, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, camera.Id, CheckKind.OnvifRecording))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task OnvifRecordingPartialHealthy_ShouldResolve()
    {
        await using var context = await CreateContextAsync();
        var camera = await SeedDeviceAsync(context, DeviceType.Camera, "cam-rec-7");

        // Sem recordingActive (device não expôs) mas videoLoss/tamper OK.
        await Evaluator(context).EvaluateAsync(
            camera, CheckKind.OnvifRecording,
            Result(CheckKind.OnvifRecording, true,
                Recording(videoLoss: "false", tamper: "false")),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, camera.Id, CheckKind.OnvifRecording))
            .Should().BeEmpty();
    }

    // ── F30-T006a: YealinkSip (registro SIP, docs/47 §C) ────────────────

    [Fact]
    public async Task YealinkSipUnregistered_ShouldOpenCriticalAlert()
    {
        await using var context = await CreateContextAsync();
        var phone = await SeedDeviceAsync(context, DeviceType.Phone, "phone-sip-1");

        await Evaluator(context).EvaluateAsync(
            phone, CheckKind.YealinkSip,
            Result(CheckKind.YealinkSip, true, new Dictionary<string, string>
            {
                ["sipRegistered"] = "false",
                ["extension"] = "1001"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, phone.Id, CheckKind.YealinkSip);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Critical);
        open[0].Message.Should().Contain("Registro SIP").And.Contain("Unregistered");
    }

    [Fact]
    public async Task YealinkSipRegistered_ShouldResolveActiveAlert()
    {
        await using var context = await CreateContextAsync();
        var phone = await SeedDeviceAsync(context, DeviceType.Phone, "phone-sip-2");
        context.Alerts.Add(Alert.Create(
            phone.Id, CheckKind.YealinkSip, AlertSeverity.Critical, "aberto antes"));
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateAsync(
            phone, CheckKind.YealinkSip,
            Result(CheckKind.YealinkSip, true, new Dictionary<string, string>
            {
                ["sipRegistered"] = "true"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, phone.Id, CheckKind.YealinkSip))
            .Should().BeEmpty("registro OK resolve o alerta ativo");
    }

    [Fact]
    public async Task YealinkSipWithoutStatus_ShouldNotEvaluate()
    {
        await using var context = await CreateContextAsync();
        var phone = await SeedDeviceAsync(context, DeviceType.Phone, "phone-sip-3");

        // Só extension/sipServer — a API não expôs o estado do registro.
        await Evaluator(context).EvaluateAsync(
            phone, CheckKind.YealinkSip,
            Result(CheckKind.YealinkSip, true, new Dictionary<string, string>
            {
                ["extension"] = "1003"
            }),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, phone.Id, CheckKind.YealinkSip))
            .Should().BeEmpty("sem estado reconhecido não avalia");
    }

    [Fact]
    public async Task YealinkSipFailedCheck_ShouldNotEvaluate()
    {
        await using var context = await CreateContextAsync();
        var phone = await SeedDeviceAsync(context, DeviceType.Phone, "phone-sip-4");

        await Evaluator(context).EvaluateAsync(
            phone, CheckKind.YealinkSip,
            Result(CheckKind.YealinkSip, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, phone.Id, CheckKind.YealinkSip))
            .Should().BeEmpty("falha da API não avalia registro");
    }

    // ── F30-T006b: YealinkCallQuality (MOS/jitter/latência/perda/PoE) ────

    private static Dictionary<string, string> Quality(
        string? mos = null,
        string? jitter = null,
        string? latency = null,
        string? loss = null,
        string? poe = null)
    {
        var values = new Dictionary<string, string>();
        if (mos is not null) values["mos"] = mos;
        if (jitter is not null) values["jitterMs"] = jitter;
        if (latency is not null) values["latencyMs"] = latency;
        if (loss is not null) values["packetLossPercent"] = loss;
        if (poe is not null) values["poePowered"] = poe;
        return values;
    }

    [Fact]
    public async Task YealinkQualityWithoutPoE_ShouldOpenCriticalAlert()
    {
        await using var context = await CreateContextAsync();
        var phone = await SeedDeviceAsync(context, DeviceType.Phone, "phone-q-1");

        await Evaluator(context).EvaluateAsync(
            phone, CheckKind.YealinkCallQuality,
            Result(CheckKind.YealinkCallQuality, true,
                Quality(mos: "4.5", poe: "false")),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, phone.Id, CheckKind.YealinkCallQuality);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Critical);
        open[0].Message.Should().Contain("PoE");
    }

    [Fact]
    public async Task YealinkQualityLossAtOrAbove10_ShouldOpenCriticalAlert()
    {
        await using var context = await CreateContextAsync();
        var phone = await SeedDeviceAsync(context, DeviceType.Phone, "phone-q-2");

        await Evaluator(context).EvaluateAsync(
            phone, CheckKind.YealinkCallQuality,
            Result(CheckKind.YealinkCallQuality, true,
                Quality(loss: "12.5", mos: "4.0")),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, phone.Id, CheckKind.YealinkCallQuality);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Critical);
        open[0].Message.Should().Contain("12.5%").And.Contain("crítico ≥ 10%");
    }

    [Fact]
    public async Task YealinkQualityMosBelow3_ShouldOpenCriticalAlert()
    {
        await using var context = await CreateContextAsync();
        var phone = await SeedDeviceAsync(context, DeviceType.Phone, "phone-q-3");

        await Evaluator(context).EvaluateAsync(
            phone, CheckKind.YealinkCallQuality,
            Result(CheckKind.YealinkCallQuality, true, Quality(mos: "2.8")),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, phone.Id, CheckKind.YealinkCallQuality);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Critical);
        open[0].Message.Should().Contain("2.8").And.Contain("crítico < 3.0");
    }

    [Fact]
    public async Task YealinkQualityLossBetween5And10_ShouldOpenWarningAlert()
    {
        await using var context = await CreateContextAsync();
        var phone = await SeedDeviceAsync(context, DeviceType.Phone, "phone-q-4");

        await Evaluator(context).EvaluateAsync(
            phone, CheckKind.YealinkCallQuality,
            Result(CheckKind.YealinkCallQuality, true, Quality(loss: "7.5")),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, phone.Id, CheckKind.YealinkCallQuality);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Warning);
        open[0].Message.Should().Contain("limite 5%");
    }

    [Fact]
    public async Task YealinkQualityMosBetween3And35_ShouldOpenWarningAlert()
    {
        await using var context = await CreateContextAsync();
        var phone = await SeedDeviceAsync(context, DeviceType.Phone, "phone-q-5");

        await Evaluator(context).EvaluateAsync(
            phone, CheckKind.YealinkCallQuality,
            Result(CheckKind.YealinkCallQuality, true, Quality(mos: "3.2")),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, phone.Id, CheckKind.YealinkCallQuality);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Warning);
        open[0].Message.Should().Contain("limite 3.5");
    }

    [Fact]
    public async Task YealinkQualityHighJitter_ShouldOpenWarningAlert()
    {
        await using var context = await CreateContextAsync();
        var phone = await SeedDeviceAsync(context, DeviceType.Phone, "phone-q-6");

        await Evaluator(context).EvaluateAsync(
            phone, CheckKind.YealinkCallQuality,
            Result(CheckKind.YealinkCallQuality, true,
                Quality(jitter: "65.0", mos: "4.1")),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, phone.Id, CheckKind.YealinkCallQuality);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Warning);
        open[0].Message.Should().Contain("Jitter").And.Contain("65.0ms");
    }

    [Fact]
    public async Task YealinkQualityHighLatency_ShouldOpenWarningAlert()
    {
        await using var context = await CreateContextAsync();
        var phone = await SeedDeviceAsync(context, DeviceType.Phone, "phone-q-7");

        await Evaluator(context).EvaluateAsync(
            phone, CheckKind.YealinkCallQuality,
            Result(CheckKind.YealinkCallQuality, true,
                Quality(latency: "210.0", mos: "4.1", jitter: "10.0")),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, phone.Id, CheckKind.YealinkCallQuality);
        open.Should().ContainSingle();
        open[0].Severity.Should().Be(AlertSeverity.Warning);
        open[0].Message.Should().Contain("Latência").And.Contain("210.0ms");
    }

    [Fact]
    public async Task YealinkQualityHealthy_ShouldResolveActiveAlert()
    {
        await using var context = await CreateContextAsync();
        var phone = await SeedDeviceAsync(context, DeviceType.Phone, "phone-q-8");
        context.Alerts.Add(Alert.Create(
            phone.Id, CheckKind.YealinkCallQuality, AlertSeverity.Warning, "aberto antes"));
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateAsync(
            phone, CheckKind.YealinkCallQuality,
            Result(CheckKind.YealinkCallQuality, true,
                Quality(mos: "4.4", jitter: "12.0", latency: "45.0",
                    loss: "0.1", poe: "true")),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, phone.Id, CheckKind.YealinkCallQuality))
            .Should().BeEmpty("métricas saudáveis resolvem o alerta ativo");
    }

    [Fact]
    public async Task YealinkQualityPoEPowerPrevailsOverMos()
    {
        await using var context = await CreateContextAsync();
        var phone = await SeedDeviceAsync(context, DeviceType.Phone, "phone-q-9");

        await Evaluator(context).EvaluateAsync(
            phone, CheckKind.YealinkCallQuality,
            Result(CheckKind.YealinkCallQuality, true,
                Quality(mos: "2.0", poe: "false")),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var open = await OpenAlertsAsync(context, phone.Id, CheckKind.YealinkCallQuality);
        open.Should().ContainSingle();
        open[0].Message.Should().Contain("PoE", "pior condição prevalece");
    }

    [Fact]
    public async Task YealinkQualityWithoutValues_ShouldNotEvaluate()
    {
        await using var context = await CreateContextAsync();
        var phone = await SeedDeviceAsync(context, DeviceType.Phone, "phone-q-10");

        await Evaluator(context).EvaluateAsync(
            phone, CheckKind.YealinkCallQuality,
            Result(CheckKind.YealinkCallQuality, succeeded: false),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await OpenAlertsAsync(context, phone.Id, CheckKind.YealinkCallQuality))
            .Should().BeEmpty("falha da API não avalia qualidade");
    }

    // ── F33-T004/ADR-059: AlertCreatedDomainEvent pelos 3 pontos ──────

    /// <summary>
    /// Prevenção (EvaluateVerdictAsync → OpenOrSuppressAsync) com
    /// Critical: evento levantado com HasIncident=true (auto-abertura do
    /// incidente F6-T003 acontece no mesmo ciclo).
    /// </summary>
    [Fact]
    public async Task EvaluateVerdict_Critical_ShouldRaiseAlertCreatedWithIncident()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        await Evaluator(context).EvaluateVerdictAsync(
            device, CheckKind.Ping, AlertSeverity.Critical,
            "Device 'sw-01' está fora do ar (ping falhou).",
            CancellationToken.None);
        await context.SaveChangesAsync();

        // Query SEM AsNoTracking → instância trackeada com os eventos
        // pendentes (o contexto de teste não tem IMediator — não despacha).
        var alert = await context.Alerts.SingleAsync(a => a.DeviceId == device.Id);

        var created = alert.DomainEvents.OfType<AlertCreatedDomainEvent>().Single();
        created.AlertId.Should().Be(alert.Id);
        created.Severity.Should().Be(AlertSeverity.Critical);
        created.Message.Should().Contain("sw-01");
        created.HasIncident.Should().BeTrue("Critical auto-abre incidente (F6-T003)");
        (await OpenIncidentsAsync(context, device.Id)).Should().NotBeEmpty();
    }

    [Fact]
    public async Task EvaluateVerdict_Warning_ShouldNotRaiseAlertCreated()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        await Evaluator(context).EvaluateVerdictAsync(
            device, CheckKind.Ping, AlertSeverity.Warning,
            "CPU 75% (prevenção).",
            CancellationToken.None);
        await context.SaveChangesAsync();

        var alert = await context.Alerts.SingleAsync(a => a.DeviceId == device.Id);
        alert.DomainEvents.OfType<AlertCreatedDomainEvent>().Should().BeEmpty(
            "Warning não vira notificação (ADR-059 §6)");
    }

    /// <summary>
    /// Prevenção em device com incidente upstream: o alerta nasce
    /// Suppressed → o AlertCreated pendente é descartado (o incidente
    /// upstream já foi notificado pelo IncidentCreated).
    /// </summary>
    [Fact]
    public async Task EvaluateVerdict_CriticalWithUpstreamIncident_ShouldNotRaiseAlertCreated()
    {
        await using var context = await CreateContextAsync();
        var up = await SeedDeviceAsync(context, name: "sw-core");
        var down = await SeedDeviceAsync(context, name: "sw-edge");
        await ConnectAsync(context, up, down);
        var rootIncident = Incident.Create(
            "core fora do ar", IncidentSeverity.Critical, deviceId: up.Id);
        context.Incidents.Add(rootIncident);
        await context.SaveChangesAsync();

        await Evaluator(context).EvaluateVerdictAsync(
            down, CheckKind.Ping, AlertSeverity.Critical,
            "Device 'sw-edge' está fora do ar (ping falhou).",
            CancellationToken.None);
        await context.SaveChangesAsync();

        var alert = await context.Alerts.SingleAsync(a => a.DeviceId == down.Id);
        alert.Status.Should().Be(AlertStatus.Suppressed);
        alert.DomainEvents.OfType<AlertCreatedDomainEvent>().Should().BeEmpty(
            "suprimido não notifica 'Alerta criado'");
        (await OpenIncidentsAsync(context, down.Id)).Should().BeEmpty(
            "downstream não cria incidente próprio enquanto suprimido");
    }
}

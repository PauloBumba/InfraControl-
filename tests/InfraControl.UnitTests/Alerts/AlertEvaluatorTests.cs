using FluentAssertions;
using InfraControl.Application.Alerts;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
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
        => new(new AlertRepository(context), new MonitoringResultRepository(context));

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

using FluentAssertions;
using InfraControl.Application.Alerts;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Monitoring.Events;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using InfraControl.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F16-T001/ADR-023/ADR-027: webhook Zabbix → MonitoringEvent → Alert →
/// Incident. Dedup por eventId (Source, ExternalEventId) — sem título;
/// recovery moderno (recoveryEventId) cria linha própria e faz backfill do
/// problem, legado transiciona in-place; severidade 0..5 mapeada e validações
/// em pt-BR.
/// </summary>
public sealed class ZabbixIngestTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ZabbixIngestTests()
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

    private static IngestZabbixProblemCommandHandler Handler(
        AppDbContext context, ZabbixEventContextDto? eventContext = null)
        => new(
            new MonitoringEventIngestor(
                new MonitoringEventRepository(context),
                new AlertRepository(context),
                new IncidentRepository(context),
                Evaluator(context),
                context,
                NullLogger<MonitoringEventIngestor>.Instance),
            new DeviceRepository(context),
            new StubZabbixEventContextReader(eventContext),
            NullLogger<IngestZabbixProblemCommandHandler>.Instance);

    private sealed class StubZabbixEventContextReader(
        ZabbixEventContextDto? context) : IZabbixEventContextReader
    {
        public Task<ZabbixEventContextDto?> GetAsync(
            string eventId, CancellationToken cancellationToken = default)
            => Task.FromResult(context);
    }

    // F19-T003: o ingestor delega Alert/Incident ao avaliador (mesma seam
    // usada pelo AlertEvaluatorTests — repos reais em Sqlite).
    private static AlertEvaluator Evaluator(AppDbContext context)
        => new(new AlertRepository(context), new MonitoringResultRepository(context),
            new IncidentRepository(context), new UpstreamCorrelationRepository(context),
            new RootCauseResolver(new ConnectionRepository(context),
                new AlertRepository(context), new DeviceRepository(context),
                new UpstreamCorrelationRepository(context),
                new IncidentRepository(context)));

    // F6-T004/F19-T003: aresta de topologia para a correlação BFS.
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


    private static IngestZabbixProblemCommand Problem(
        string eventId,
        string? host = "sw-zabbix",
        string? ip = "192.0.2.71",
        string name = "Ping down",
        int severity = 4,
        long? occurredAt = null)
        => new(eventId, null, host, ip, name, severity, "PROBLEM", occurredAt);

    private static IngestZabbixProblemCommand Ok(
        string eventId,
        string? recoveryEventId = null,
        string? host = "sw-zabbix",
        string? ip = "192.0.2.71",
        string name = "Ping down",
        int severity = 4,
        long? occurredAt = null)
        => new(eventId, recoveryEventId, host, ip, name, severity, "OK", occurredAt);

    private static async Task<Device> SeedDeviceAsync(
        AppDbContext context,
        string ip = "192.0.2.71",
        string name = "sw-zabbix")
    {
        var location = Location.Create("Sala Zabbix");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, name, DeviceType.Switch, ip: ip);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    // ── PROBLEM: criação do triple Event + Alert + Incident ────────────────

    [Fact]
    public async Task Problem_WithMatchingIp_ShouldCreateEventAlertAndIncident()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        var result = await Handler(context).Handle(
            Problem("100", name: "Ping down", severity: 5), CancellationToken.None);

        result.Outcome.Should().Be("created");
        result.IncidentId.Should().NotBeNull();

        var monitoringEvent = await context.MonitoringEvents.AsNoTracking().SingleAsync();
        monitoringEvent.Source.Should().Be(MonitoringSource.Zabbix);
        monitoringEvent.ExternalEventId.Should().Be("100");
        monitoringEvent.ExternalProblemId.Should().Be("100");
        monitoringEvent.ExternalRecoveryId.Should().BeNull();
        monitoringEvent.Status.Should().Be(MonitoringEventStatus.Problem);
        monitoringEvent.Severity.Should().Be(5);
        monitoringEvent.DeviceId.Should().Be(device.Id);
        monitoringEvent.RawPayload.Should().Contain("\"eventId\":\"100\"");

        var alert = await context.Alerts.AsNoTracking().SingleAsync();
        alert.Source.Should().Be(MonitoringSource.Zabbix);
        alert.MonitoringEventId.Should().Be(monitoringEvent.Id);
        alert.ExternalProblemId.Should().Be("100");
        alert.DeviceId.Should().Be(device.Id);
        alert.Kind.Should().BeNull();
        alert.Severity.Should().Be(AlertSeverity.Critical);
        alert.Status.Should().Be(AlertStatus.Open);
        alert.Message.Should().Be("Ping down");

        var incident = await context.Incidents.AsNoTracking()
            .SingleAsync(row => row.Id == result.IncidentId);
        incident.DeviceId.Should().Be(device.Id);
        incident.Title.Should().Be("Ping down");
        incident.Severity.Should().Be(IncidentSeverity.Critical);
        incident.Status.Should().Be(IncidentStatus.Open);
        incident.Description.Should()
            .Contain("evento 100").And.Contain("sw-zabbix").And.Contain("192.0.2.71");

        var link = await context.IncidentAlerts.AsNoTracking().SingleAsync();
        link.IncidentId.Should().Be(incident.Id);
        link.AlertId.Should().Be(alert.Id);
    }

    [Fact]
    public async Task Problem_ShouldPersistZabbixEventContextForRouting()
    {
        await using var context = await CreateContextAsync();
        var eventContext = new ZabbixEventContextDto(
            "200",
            ["10"],
            ["4"],
            [new ZabbixEventTagDto("service", "web")]);

        await Handler(context, eventContext).Handle(
            Problem("100"), CancellationToken.None);

        var monitoringEvent = await context.MonitoringEvents.AsNoTracking().SingleAsync();
        monitoringEvent.SourceContextJson.Should().Contain("\"TriggerId\":\"200\"")
            .And.Contain("\"HostGroupIds\":[\"4\"]")
            .And.Contain("\"Tag\":\"service\"");
    }

    [Fact]
    public async Task DuplicateProblem_ShouldBackfillMissingZabbixContext()
    {
        await using var context = await CreateContextAsync();
        var handlerWithoutContext = Handler(context);
        await handlerWithoutContext.Handle(Problem("100"), CancellationToken.None);

        var eventContext = new ZabbixEventContextDto("200", ["10"], ["4"], []);
        var duplicate = await Handler(context, eventContext).Handle(
            Problem("100"), CancellationToken.None);

        duplicate.Outcome.Should().Be("duplicate");
        var monitoringEvent = await context.MonitoringEvents.AsNoTracking().SingleAsync();
        monitoringEvent.SourceContextJson.Should().Contain("\"TriggerId\":\"200\"");
    }

    [Fact]
    public async Task ReplayedRecovery_ShouldBackfillMissingProblemContext()
    {
        await using var context = await CreateContextAsync();
        await Handler(context).Handle(Problem("101"), CancellationToken.None);
        await Handler(context).Handle(Ok("101", "102"), CancellationToken.None);

        var eventContext = new ZabbixEventContextDto("200", ["10"], ["4"], []);
        var replay = await Handler(context, eventContext).Handle(
            Ok("101", "102"), CancellationToken.None);

        replay.Outcome.Should().Be("resolved");
        var problemEvent = await context.MonitoringEvents.AsNoTracking()
            .SingleAsync(row => row.ExternalEventId == "101");
        problemEvent.SourceContextJson.Should().Contain("\"TriggerId\":\"200\"");
    }

    [Fact]
    public async Task Problem_WithoutKnownDevice_ShouldCreateAllWithoutDevice()
    {
        await using var context = await CreateContextAsync();

        var result = await Handler(context).Handle(
            Problem("200", host: "host-desconhecido", ip: "198.51.100.9",
                name: "Disco cheio", severity: 3),
            CancellationToken.None);

        result.Outcome.Should().Be("created");

        var alert = await context.Alerts.AsNoTracking().SingleAsync();
        alert.DeviceId.Should().BeNull();
        alert.Severity.Should().Be(AlertSeverity.Critical);

        var incident = await context.Incidents.AsNoTracking()
            .SingleAsync(row => row.Id == result.IncidentId);
        incident.DeviceId.Should().BeNull();
        incident.Severity.Should().Be(IncidentSeverity.High);
        incident.Description.Should()
            .Contain("host-desconhecido").And.Contain("198.51.100.9");
    }

    [Fact]
    public async Task Problem_IpUnknown_HostFallback_ShouldMatchByName()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        var result = await Handler(context).Handle(
            Problem("300", host: "sw-zabbix", ip: "198.51.100.7", name: "Link down"),
            CancellationToken.None);

        result.Outcome.Should().Be("created");

        var incident = await context.Incidents.AsNoTracking()
            .SingleAsync(row => row.Id == result.IncidentId);
        incident.DeviceId.Should().Be(device.Id);
    }

    // ── Cenário 2: replay do mesmo eventId = duplicate, sem duplicação ─────

    [Fact]
    public async Task Problem_DuplicateEventId_ShouldBeIdempotent()
    {
        await using var context = await CreateContextAsync();
        var handler = Handler(context);
        var command = Problem("400");

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        first.Outcome.Should().Be("created");
        second.Outcome.Should().Be("duplicate");
        second.IncidentId.Should().Be(first.IncidentId);

        (await context.MonitoringEvents.CountAsync()).Should().Be(1);
        (await context.Alerts.CountAsync()).Should().Be(1);
        (await context.Incidents.CountAsync()).Should().Be(1);
    }

    /// <summary>
    /// F19-T003/regra 4 (handoff): Problem 100 → Alert 100 → Incident 50;
    /// o REENVIO do mesmo problem é no-op — não cria Alert novo nem Incident
    /// novo (idempotência anti-storm por identidade, nunca por título).
    /// </summary>
    [Fact]
    public async Task Problem_Resend_ShouldNotCreateNewAlertNorNewIncident()
    {
        await using var context = await CreateContextAsync();
        var handler = Handler(context);
        var problem = Problem("100", name: "Ping down", severity: 5);

        var first = await handler.Handle(problem, CancellationToken.None);
        var resend = await handler.Handle(problem, CancellationToken.None);

        first.Outcome.Should().Be("created");
        first.IncidentId.Should().NotBeNull();

        // Alert 100: mesma row antes e depois do reenvio (o DTO público
        // expõe só incidentId/outcome — contrato docs/27 — então a prova do
        // "mesmo alert" é a row no banco).
        var alertId = (await context.Alerts.AsNoTracking().SingleAsync()).Id;
        var incidentId = first.IncidentId;

        resend.Outcome.Should().Be("duplicate");
        resend.IncidentId.Should().Be(incidentId);

        (await context.MonitoringEvents.CountAsync()).Should().Be(1);
        (await context.Alerts.AsNoTracking().SingleAsync()).Id.Should().Be(alertId);
        (await context.Incidents.CountAsync()).Should().Be(1);
    }

    // ── F19-T003: Incident SOMENTE se Critical ─────────────────────────────

    [Fact]
    public async Task Problem_WarningSeverity_ShouldCreateAlertWithoutIncident()
    {
        await using var context = await CreateContextAsync();
        await SeedDeviceAsync(context);

        var result = await Handler(context).Handle(
            Problem("910", name: "CPU alta", severity: 2), CancellationToken.None);

        result.Outcome.Should().Be("created");
        result.IncidentId.Should().BeNull();

        var alert = await context.Alerts.AsNoTracking().SingleAsync();
        alert.Severity.Should().Be(AlertSeverity.Warning);
        alert.Status.Should().Be(AlertStatus.Open);

        (await context.Incidents.CountAsync()).Should().Be(0);
    }

    // ── F19-T003: correlação BFS + supressão topológica ────────────────────

    [Fact]
    public async Task Problem_DownstreamWithUpstreamIncident_ShouldSuppressAndLinkWithoutOwnIncident()
    {
        await using var context = await CreateContextAsync();
        var up = await SeedDeviceAsync(context, ip: "192.0.2.71", name: "sw-core");
        var down = await SeedDeviceAsync(context, ip: "192.0.2.72", name: "sw-edge");
        await ConnectAsync(context, up, down);

        var rootIncident = Incident.Create(
            "core fora do ar", IncidentSeverity.Critical, deviceId: up.Id);
        context.Incidents.Add(rootIncident);
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(
            Problem("920", host: "sw-edge", ip: "192.0.2.72",
                name: "Link down", severity: 5),
            CancellationToken.None);

        // Alert externo nasce Suppressed e entra no incidente do upstream —
        // sem incidente próprio (regra 1 do handoff: supressão antes do Incident).
        result.Outcome.Should().Be("created");
        result.IncidentId.Should().Be(rootIncident.Id);

        var alert = await context.Alerts.AsNoTracking().SingleAsync();
        alert.Status.Should().Be(AlertStatus.Suppressed);
        alert.DeviceId.Should().Be(down.Id);

        var link = await context.IncidentAlerts.AsNoTracking()
            .SingleAsync(row => row.AlertId == alert.Id);
        link.IncidentId.Should().Be(rootIncident.Id);

        (await context.Incidents.CountAsync()).Should().Be(1);
    }

    /// <summary>
    /// F19-T003 + regra 5: o recovery de um alerta Suppressed (vítima)
    /// NÃO pode fechar o incidente upstream enquanto os demais alerts
    /// vinculados ainda estiverem abertos.
    /// </summary>
    [Fact]
    public async Task Ok_OnSuppressedAlert_ShouldKeepUpstreamIncidentWhileSiblingOpen()
    {
        await using var context = await CreateContextAsync();
        var up = await SeedDeviceAsync(context, ip: "192.0.2.71", name: "sw-core");
        var down = await SeedDeviceAsync(context, ip: "192.0.2.72", name: "sw-edge");
        await ConnectAsync(context, up, down);
        var handler = Handler(context);

        // Incidente do upstream + alert dele ainda Open (a causa persiste).
        var upstreamProblem = await handler.Handle(
            Problem("1000", host: "sw-core", ip: "192.0.2.71",
                name: "Core fora do ar", severity: 5), CancellationToken.None);
        upstreamProblem.IncidentId.Should().NotBeNull();

        // Vítima: filho suprimido pelo incidente do pai.
        var suppressed = await handler.Handle(
            Problem("1010", host: "sw-edge", ip: "192.0.2.72",
                name: "Link down", severity: 5), CancellationToken.None);
        suppressed.IncidentId.Should().Be(upstreamProblem.IncidentId);

        // Recovery só do filho: alert do pai continua Open → incidente
        // upstream deve permanecer não-Resolved (regra 5).
        var resolved = await handler.Handle(
            Ok("1010", recoveryEventId: "1011", host: "sw-edge",
                ip: "192.0.2.72", name: "Link down", severity: 5),
            CancellationToken.None);
        resolved.Outcome.Should().Be("resolved");

        var upstreamIncident = await context.Incidents.AsNoTracking()
            .SingleAsync(row => row.Id == upstreamProblem.IncidentId);
        upstreamIncident.Status.Should().NotBe(IncidentStatus.Resolved);

        var siblingAlert = await context.Alerts.AsNoTracking()
            .SingleAsync(row => row.ExternalProblemId == "1000");
        siblingAlert.Status.Should().Be(AlertStatus.Open);
    }


    [Fact]
    public async Task Problem_SameTitleDifferentEvents_ShouldCreateBoth()
    {
        await using var context = await CreateContextAsync();
        await SeedDeviceAsync(context, ip: "192.0.2.71", name: "sw-zabbix");
        await SeedDeviceAsync(context, ip: "192.0.2.72", name: "sw-zabbix-2");
        var handler = Handler(context);

        var first = await handler.Handle(
            Problem("100", host: "sw-zabbix", ip: "192.0.2.71"),
            CancellationToken.None);
        var second = await handler.Handle(
            Problem("200", host: "sw-zabbix-2", ip: "192.0.2.72"),
            CancellationToken.None);

        first.Outcome.Should().Be("created");
        second.Outcome.Should().Be("created");
        second.IncidentId.Should().NotBe(first.IncidentId!.Value);
        (await context.MonitoringEvents.CountAsync()).Should().Be(2);
        (await context.Incidents.CountAsync()).Should().Be(2);
    }

    // ── Cenário 3: recovery moderno (recoveryEventId) ──────────────────────

    [Fact]
    public async Task Ok_WithRecoveryEventId_ShouldCreateRecoveryRowAndBackfillProblem()
    {
        await using var context = await CreateContextAsync();
        var handler = Handler(context);

        var created = await handler.Handle(
            Problem("100", severity: 5), CancellationToken.None);
        var resolved = await handler.Handle(
            Ok("100", recoveryEventId: "150"), CancellationToken.None);

        resolved.Outcome.Should().Be("resolved");
        resolved.IncidentId.Should().Be(created.IncidentId);

        var events = await context.MonitoringEvents.AsNoTracking()
            .OrderBy(e => e.ExternalEventId)
            .ToListAsync();
        events.Should().HaveCount(2);

        var problemRow = events.Single(e => e.ExternalEventId == "100");
        problemRow.Status.Should().Be(MonitoringEventStatus.Recovered);
        problemRow.ExternalRecoveryId.Should().Be("150");
        problemRow.RecoveredAt.Should().NotBeNull();

        var recoveryRow = events.Single(e => e.ExternalEventId == "150");
        recoveryRow.Status.Should().Be(MonitoringEventStatus.Recovered);
        recoveryRow.ExternalProblemId.Should().Be("100");
        recoveryRow.ExternalRecoveryId.Should().Be("150");

        var alert = await context.Alerts.AsNoTracking().SingleAsync();
        alert.Status.Should().Be(AlertStatus.Resolved);
        alert.ResolvedAt.Should().NotBeNull();

        var incident = await context.Incidents.AsNoTracking().SingleAsync();
        incident.Status.Should().Be(IncidentStatus.Resolved);
        incident.ResolvedAt.Should().NotBeNull();
    }

    // ── Cenário 4: replay do recovery = idempotente, sem duplicação ────────

    [Fact]
    public async Task Ok_Replay_ShouldBeIdempotentWithoutDuplicates()
    {
        await using var context = await CreateContextAsync();
        var handler = Handler(context);

        await handler.Handle(Problem("100"), CancellationToken.None);
        var first = await handler.Handle(
            Ok("100", recoveryEventId: "150"), CancellationToken.None);
        var second = await handler.Handle(
            Ok("100", recoveryEventId: "150"), CancellationToken.None);

        first.Outcome.Should().Be("resolved");
        second.Outcome.Should().Be("resolved");
        second.IncidentId.Should().Be(first.IncidentId);

        (await context.MonitoringEvents.CountAsync()).Should().Be(2);
        (await context.Alerts.CountAsync()).Should().Be(1);
        (await context.Incidents.CountAsync()).Should().Be(1);
        (await context.Incidents.SingleAsync()).Status
            .Should().Be(IncidentStatus.Resolved);
    }

    // ── Cenário 5: OK legado sem recoveryEventId = transição in-place ──────

    [Fact]
    public async Task Ok_WithoutRecoveryEventId_ShouldTransitionProblemInPlace()
    {
        await using var context = await CreateContextAsync();
        var handler = Handler(context);

        var created = await handler.Handle(
            Problem("500", name: "Ping down"), CancellationToken.None);
        var resolved = await handler.Handle(
            Ok("500", recoveryEventId: null), CancellationToken.None);

        resolved.Outcome.Should().Be("resolved");
        resolved.IncidentId.Should().Be(created.IncidentId);

        // Sem linha nova: unicidade (source, external_event_id) impediria.
        var monitoringEvent = await context.MonitoringEvents.AsNoTracking().SingleAsync();
        monitoringEvent.ExternalEventId.Should().Be("500");
        monitoringEvent.Status.Should().Be(MonitoringEventStatus.Recovered);
        monitoringEvent.RecoveredAt.Should().NotBeNull();

        var alert = await context.Alerts.AsNoTracking().SingleAsync();
        alert.Status.Should().Be(AlertStatus.Resolved);

        var incident = await context.Incidents.AsNoTracking().SingleAsync();
        incident.Status.Should().Be(IncidentStatus.Resolved);
        incident.ResolvedAt.Should().NotBeNull();
    }

    // ── Cenário 6: OK órfão (nunca houve PROBLEM) = none, sem erro ─────────

    [Fact]
    public async Task Ok_WithoutMatchingProblem_ShouldReturnNoneAndWriteOrphanRow()
    {
        await using var context = await CreateContextAsync();

        var result = await Handler(context).Handle(
            Ok("600", host: "host-x", ip: "203.0.113.9",
                name: "Janela de manutenção", severity: 1),
            CancellationToken.None);

        result.Outcome.Should().Be("none");
        result.IncidentId.Should().BeNull();
        (await context.Incidents.CountAsync()).Should().Be(0);
        (await context.Alerts.CountAsync()).Should().Be(0);

        // A evidência órfã é preservada (row Recovered) e o replay não
        // estoura a unicidade.
        var orphan = await context.MonitoringEvents.AsNoTracking().SingleAsync();
        orphan.ExternalEventId.Should().Be("600");
        orphan.Status.Should().Be(MonitoringEventStatus.Recovered);

        var replay = await Handler(context).Handle(
            Ok("600", host: "host-x", ip: "203.0.113.9",
                name: "Janela de manutenção", severity: 1),
            CancellationToken.None);
        replay.Outcome.Should().Be("none");
        (await context.MonitoringEvents.CountAsync()).Should().Be(1);
    }

    // ── Outros comportamentos ──────────────────────────────────────────────

    [Fact]
    public async Task Problem_AfterResolved_ShouldCreateNewIncident()
    {
        await using var context = await CreateContextAsync();
        var handler = Handler(context);

        await handler.Handle(Problem("700"), CancellationToken.None);
        await handler.Handle(Ok("700", recoveryEventId: "701"), CancellationToken.None);
        var reopened = await handler.Handle(Problem("702"), CancellationToken.None);

        reopened.Outcome.Should().Be("created");
        (await context.Incidents.CountAsync()).Should().Be(2);
        (await context.MonitoringEvents.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Ok_ShouldResolveEvenWhenIncidentIsInvestigating()
    {
        await using var context = await CreateContextAsync();
        var handler = Handler(context);

        var created = await handler.Handle(
            Problem("800"), CancellationToken.None);
        var incident = await context.Incidents.SingleAsync(
            row => row.Id == created.IncidentId);
        incident.ChangeStatus(IncidentStatus.Investigating);
        await context.SaveChangesAsync();

        var resolved = await handler.Handle(
            Ok("800", recoveryEventId: "801"), CancellationToken.None);

        resolved.Outcome.Should().Be("resolved");
        (await context.Incidents.SingleAsync()).Status
            .Should().Be(IncidentStatus.Resolved);
    }

    [Fact]
    public async Task OccurredAt_ShouldPersistFromPayload()
    {
        await using var context = await CreateContextAsync();

        // 2026-10-01T12:00:00Z
        await Handler(context).Handle(
            Problem("900", occurredAt: 1790865600), CancellationToken.None);

        var monitoringEvent = await context.MonitoringEvents.AsNoTracking().SingleAsync();
        monitoringEvent.OccurredAt.Should().Be(
            DateTimeOffset.FromUnixTimeSeconds(1790865600).UtcDateTime);
    }

    // F19-T003/regra 1: severidade mapeada no Alert sempre; Incidente
    // SOMENTE se Critical (0..2 = Warning → sem incidente).
    [Theory]
    [InlineData(0, null, AlertSeverity.Warning)]
    [InlineData(1, null, AlertSeverity.Warning)]
    [InlineData(2, null, AlertSeverity.Warning)]
    [InlineData(3, IncidentSeverity.High, AlertSeverity.Critical)]
    [InlineData(4, IncidentSeverity.Critical, AlertSeverity.Critical)]
    [InlineData(5, IncidentSeverity.Critical, AlertSeverity.Critical)]
    public async Task Severity_ZabbixLevels_ShouldMapAlertAndIncidentOnlyWhenCritical(
        int zabbixLevel, IncidentSeverity? expectedIncident, AlertSeverity expectedAlert)
    {
        await using var context = await CreateContextAsync();

        await Handler(context).Handle(
            Problem($"sev-{zabbixLevel}", host: null, ip: null,
                name: $"Problema nível {zabbixLevel}", severity: zabbixLevel),
            CancellationToken.None);

        var alert = await context.Alerts.AsNoTracking().SingleAsync();
        alert.Severity.Should().Be(expectedAlert);

        var incident = await context.Incidents.AsNoTracking().FirstOrDefaultAsync();
        if (expectedIncident is null)
        {
            incident.Should().BeNull();
        }
        else
        {
            incident.Should().NotBeNull();
            incident!.Severity.Should().Be(expectedIncident);
        }
    }

    // ── Validações pt-BR ───────────────────────────────────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public async Task InvalidSeverity_ShouldThrowPtBr(int severity)
    {
        await using var context = await CreateContextAsync();

        var action = () => Handler(context).Handle(
            new IngestZabbixProblemCommand(
                null, null, null, null, "Problema", severity, "PROBLEM", null),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage($"Severidade inválida: {severity}. Use 0 (Not classified) a 5 (Disaster).*");
    }

    [Fact]
    public async Task MissingSeverity_ShouldThrowPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => Handler(context).Handle(
            new IngestZabbixProblemCommand(
                null, null, null, null, "Problema", null, "PROBLEM", null),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("A severidade é obrigatória (0 a 5).*");
    }

    [Fact]
    public async Task EmptyName_ShouldThrowPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => Handler(context).Handle(
            new IngestZabbixProblemCommand(
                null, null, null, null, "   ", 3, "PROBLEM", null),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O nome do problema é obrigatório.*");
    }

    [Fact]
    public async Task InvalidStatus_ShouldThrowPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => Handler(context).Handle(
            new IngestZabbixProblemCommand(
                null, null, null, null, "Problema", 3, "DOWN", null),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Status inválido: 'DOWN'. Use 'PROBLEM' ou 'OK'.*");
    }

    [Fact]
    public async Task MissingEventId_ShouldThrowPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => Handler(context).Handle(
            new IngestZabbixProblemCommand(
                null, null, null, null, "Problema", 3, "PROBLEM", null),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O eventId é obrigatório*");
    }

    [Fact]
    public async Task EventId_TooLong_ShouldThrowPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => Handler(context).Handle(
            Problem(new string('9', 65)), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O eventId excede 64 caracteres.*");
    }
}

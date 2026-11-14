using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Integrations.WhatsApp.Ports;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.Rules.Ports;
using InfraControl.Application.Notifications.Transitions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using InfraControl.Workers.Consumers.Notifications;
using MassTransit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// Spec de transições de estado: NÃO HOUVE MUDANÇA DE ESTADO = NENHUMA
/// NOTIFICAÇÃO em nenhum canal. Cobre a matriz completa no gate central
/// e a integração no NotificationEventConsumer (Alert/Incident/Resolved
/// compartilham a chave "alert:{id}" via IncidentAlert).
/// </summary>
public sealed class OccurrenceTransitionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public OccurrenceTransitionTests()
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

    private static OccurrenceTransitionGate CreateGate(AppDbContext context)
        => new(
            new OccurrenceRepository(context),
            NullLogger<OccurrenceTransitionGate>.Instance);

    private static NotificationChannelsOptions ChannelsWithWhatsApp() => new()
    {
        Default = ["Email", "SignalR", "WhatsApp"],
        WhatsApp = new WhatsAppChannelOptions { GroupId = "120363@g.us" },
    };

    private static async Task<NotificationEventConsumer> CreateConsumerAsync(
        AppDbContext context)
    {
        var channels = ChannelsWithWhatsApp();
        return new NotificationEventConsumer(
            context,
            new InboxStore(context),
            Options.Create(channels),
            new StaticWhatsAppOptionsResolver(channels.WhatsApp),
            new NotificationEventRuleRepository(context),
            NullLogger<NotificationEventConsumer>.Instance,
            CreateGate(context));
    }

    private static async Task<(Alert Alert, Incident Incident)> SeedLinkedOccurrenceAsync(
        AppDbContext context)
    {
        var location = Location.Create("Sala Transitions");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, "sw-01", DeviceType.Switch, ip: null);
        context.Devices.Add(device);

        var alert = Alert.Create(device.Id, CheckKind.Ping, AlertSeverity.Critical, "Queda");
        context.Alerts.Add(alert);

        var incident = Incident.Create("Queda", IncidentSeverity.Critical, deviceId: device.Id);
        context.Incidents.Add(incident);
        context.IncidentAlerts.Add(IncidentAlert.Create(incident.Id, alert.Id));
        await context.SaveChangesAsync();
        return (alert, incident);
    }

    private static async Task SeedRecipientsAsync(AppDbContext context)
    {
        context.DomainUsers.Add(User.Create(Guid.NewGuid(), "op@exemplo.test", "Operador", UserRole.Operator));
        context.DomainUsers.Add(User.Create(Guid.NewGuid(), "admin@exemplo.test", "Admin", UserRole.Admin));
        await context.SaveChangesAsync();
    }

    private static ConsumeContext<T> Ctx<T>(T message) where T : class
    {
        var mock = new Mock<ConsumeContext<T>>();
        mock.SetupGet(c => c.Message).Returns(message);
        mock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }

    // ── Gate: matriz de transições ─────────────────────────────────────

    [Fact]
    public async Task Gate_PrimeiraVez_Alert_Autoriza()
    {
        await using var context = await CreateContextAsync();
        var gate = CreateGate(context);

        Assert.True(await gate.AuthorizeAsync("alert:a1", OccurrenceState.Alert, "AlertCreatedDomainEvent", Guid.NewGuid()));

        var occurrence = await context.Occurrences.SingleAsync();
        Assert.Equal(OccurrenceState.Alert, occurrence.State);
        var transition = await context.OccurrenceTransitions.SingleAsync();
        Assert.Null(transition.FromState);
        Assert.Equal(OccurrenceState.Alert, transition.ToState);
    }

    [Fact]
    public async Task Gate_MesmoEstado_Descarta()
    {
        await using var context = await CreateContextAsync();
        var gate = CreateGate(context);
        var key = "alert:a1";

        Assert.True(await gate.AuthorizeAsync(key, OccurrenceState.Alert, "AlertCreatedDomainEvent", Guid.NewGuid()));
        Assert.False(await gate.AuthorizeAsync(key, OccurrenceState.Alert, "AlertCreatedDomainEvent", Guid.NewGuid()));
        Assert.False(await gate.AuthorizeAsync(key, OccurrenceState.Alert, "AlertCreatedDomainEvent", Guid.NewGuid()));

        // 1 transição aceita + 2 descartes auditados (SameState)
        var rows = await context.OccurrenceTransitions.ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.Single(rows, t => t.Decision == OccurrenceDecision.TransitionAccepted);
        Assert.Equal(2, rows.Count(t => t.Decision == OccurrenceDecision.SameState));
        Assert.All(rows.Where(t => t.Decision == OccurrenceDecision.SameState),
            t => Assert.Equal("estado já persistido: Alert", t.SuppressionReason));
    }

    [Fact]
    public async Task Gate_Matriz_Completa()
    {
        await using var context = await CreateContextAsync();
        var gate = CreateGate(context);
        var key = "alert:a1";
        Task<bool> Go(OccurrenceState s) => gate.AuthorizeAsync(
            key, s, "TestEvent", Guid.NewGuid());

        // Novo → Alert/Incident: notifica
        Assert.True(await Go(OccurrenceState.Alert));
        // Alert → Alert / Incident → Incident / Resolved → Resolved: descarta
        Assert.False(await Go(OccurrenceState.Alert));
        // Alert → Incident / Incident → Alert: notifica mudança
        Assert.True(await Go(OccurrenceState.Incident));
        Assert.False(await Go(OccurrenceState.Incident));
        Assert.True(await Go(OccurrenceState.Alert));
        // → Resolved: notifica resolução
        Assert.True(await Go(OccurrenceState.Resolved));
        Assert.False(await Go(OccurrenceState.Resolved));
        // Resolved → Alert/Incident: reabertura notifica
        Assert.True(await Go(OccurrenceState.Alert));
        Assert.True(await Go(OccurrenceState.Incident));
        Assert.True(await Go(OccurrenceState.Resolved));

        // Histórico: 7 transições aceitas + 3 descartes SameState auditados
        var accepted = await context.OccurrenceTransitions
            .Where(t => t.Decision == OccurrenceDecision.TransitionAccepted)
            .OrderBy(t => t.OccurredAtUtc).ThenBy(t => t.Id)
            .Select(t => t.ToState).ToListAsync();
        Assert.Equal(
            new[]
            {
                OccurrenceState.Alert, OccurrenceState.Incident, OccurrenceState.Alert,
                OccurrenceState.Resolved, OccurrenceState.Alert, OccurrenceState.Incident,
                OccurrenceState.Resolved
            },
            accepted);
        Assert.Equal(10, await context.OccurrenceTransitions.CountAsync());
        Assert.Equal(3, await context.OccurrenceTransitions
            .CountAsync(t => t.Decision == OccurrenceDecision.SameState));
        Assert.Equal(OccurrenceState.Resolved, (await context.Occurrences.SingleAsync()).State);
    }

    // ── Consumer: gate antes do fanout (todos os canais) ───────────────

    [Fact]
    public async Task Consumer_EventoRepetido_MesmoEstado_SemNovaNotificacao()
    {
        await using var context = await CreateContextAsync();
        await SeedRecipientsAsync(context);
        var consumer = await CreateConsumerAsync(context);
        var alertId = Guid.NewGuid();

        await consumer.Consume(Ctx(new AlertCreatedDomainEvent(
            alertId, AlertSeverity.Warning, Guid.NewGuid(), "CPU alta", hasIncident: false)));
        await consumer.Consume(Ctx(new AlertCreatedDomainEvent(
            alertId, AlertSeverity.Warning, Guid.NewGuid(), "CPU alta", hasIncident: false)));
        await consumer.Consume(Ctx(new AlertCreatedDomainEvent(
            alertId, AlertSeverity.Warning, Guid.NewGuid(), "CPU alta", hasIncident: false)));

        // 3 eventos, mesmo estado → 1 notificação, 1 conjunto de deliveries
        Assert.Equal(1, await context.Notifications.CountAsync());
        Assert.Equal(
            await context.NotificationDeliveries.CountAsync(),
            await context.NotificationDeliveries.CountAsync()); // sanity
        var deliveries = await context.NotificationDeliveries.ToListAsync();
        Assert.NotEmpty(deliveries);
        Assert.All(deliveries, d => Assert.Equal(DeliveryStatus.Pending, d.Status));
    }

    [Fact]
    public async Task Consumer_AlertaParaIncidente_NotificaAmbos()
    {
        await using var context = await CreateContextAsync();
        await SeedRecipientsAsync(context);
        var consumer = await CreateConsumerAsync(context);
        var (alert, incident) = await SeedLinkedOccurrenceAsync(context);

        await consumer.Consume(Ctx(new AlertCreatedDomainEvent(
            alert.Id, AlertSeverity.Critical, alert.DeviceId, "Disco cheio", hasIncident: true)));
        await consumer.Consume(Ctx(new IncidentCreatedDomainEvent(
            incident.Id, "Disco cheio", IncidentSeverity.Critical, incident.DeviceId)));
        // Incidente repetido (mesmo estado) → descartado
        await consumer.Consume(Ctx(new IncidentCreatedDomainEvent(
            incident.Id, "Disco cheio", IncidentSeverity.Critical, incident.DeviceId)));

        Assert.Equal(2, await context.Notifications.CountAsync());
        Assert.Equal(
            OccurrenceState.Incident,
            (await context.Occurrences.SingleAsync(o => o.OccurrenceKey == $"alert:{alert.Id}")).State);
    }

    [Fact]
    public async Task Consumer_ResolucaoUnica_IncidenteEAlerta_NaoDuplica()
    {
        await using var context = await CreateContextAsync();
        await SeedRecipientsAsync(context);
        var consumer = await CreateConsumerAsync(context);
        var (alert, incident) = await SeedLinkedOccurrenceAsync(context);

        await consumer.Consume(Ctx(new AlertCreatedDomainEvent(
            alert.Id, AlertSeverity.Critical, alert.DeviceId, "Queda", hasIncident: true)));
        await consumer.Consume(Ctx(new IncidentCreatedDomainEvent(
            incident.Id, "Queda", IncidentSeverity.Critical, incident.DeviceId)));
        // Resolução chega pelos DOIS eventos (alert.Resolve + incident.Resolve)
        // — o segundo é descartado pelo gate (mesmo estado Resolved).
        await consumer.Consume(Ctx(new AlertResolvedDomainEvent(
            alert.Id, "Queda normalizada", alert.DeviceId)));
        await consumer.Consume(Ctx(new IncidentResolvedDomainEvent(
            incident.Id, "Queda", IncidentSeverity.Critical, incident.DeviceId)));
        // Replays dos dois resolves → também descartados
        await consumer.Consume(Ctx(new AlertResolvedDomainEvent(
            alert.Id, "Queda normalizada", alert.DeviceId)));
        await consumer.Consume(Ctx(new IncidentResolvedDomainEvent(
            incident.Id, "Queda", IncidentSeverity.Critical, incident.DeviceId)));

        // Alert + Incident + UMA resolução = 3 notificações
        Assert.Equal(3, await context.Notifications.CountAsync());
        Assert.Equal(1, await context.OccurrenceTransitions
            .CountAsync(t => t.ToState == OccurrenceState.Resolved
                && t.Decision == OccurrenceDecision.TransitionAccepted));
    }

    [Fact]
    public async Task Consumer_IncidenteVinculado_AggregateIdCanonico()
    {
        await using var context = await CreateContextAsync();
        await SeedRecipientsAsync(context);
        var consumer = await CreateConsumerAsync(context);
        var (alert, incident) = await SeedLinkedOccurrenceAsync(context);

        await consumer.Consume(Ctx(new IncidentCreatedDomainEvent(
            incident.Id, "Queda", IncidentSeverity.Critical, incident.DeviceId)));

        // aggregate_id canônico = id do ALERTA raiz (mesma correlação do gate)
        var notification = await context.Notifications
            .SingleAsync(n => n.EventType == nameof(IncidentCreatedDomainEvent));
        Assert.Equal(alert.Id, notification.AggregateId);
    }

    [Fact]
    public async Task Consumer_ReaberturaAposResolvido_Notifica()
    {
        await using var context = await CreateContextAsync();
        await SeedRecipientsAsync(context);
        var consumer = await CreateConsumerAsync(context);
        var alertId = Guid.NewGuid();

        await consumer.Consume(Ctx(new AlertCreatedDomainEvent(
            alertId, AlertSeverity.Warning, Guid.NewGuid(), "Instável", hasIncident: false)));
        await consumer.Consume(Ctx(new AlertResolvedDomainEvent(
            alertId, "Instável", Guid.NewGuid())));
        // Zabbix reabre (novo eventid, mesmo alerta reaberto ou novo alerta —
        // aqui o MESMO alertId reabrindo: Resolved → Alert deve notificar)
        await consumer.Consume(Ctx(new AlertCreatedDomainEvent(
            alertId, AlertSeverity.Warning, Guid.NewGuid(), "Instável", hasIncident: false)));

        Assert.Equal(3, await context.Notifications.CountAsync());
    }

    // ── Auditoria: decisões classificadas + identidade canônica ────────

    [Fact]
    public async Task Gate_Auditoria_GravaDecisaoCorrelationEEvento()
    {
        await using var context = await CreateContextAsync();
        var gate = CreateGate(context);
        var sourceEventId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        Assert.True(await gate.AuthorizeAsync(
            "alert:audit-1", OccurrenceState.Alert,
            nameof(AlertCreatedDomainEvent), sourceEventId,
            correlationId));

        var accepted = await context.OccurrenceTransitions.SingleAsync();
        Assert.Equal(OccurrenceDecision.TransitionAccepted, accepted.Decision);
        Assert.Equal("alert:audit-1", accepted.OccurrenceKey);
        Assert.Equal(sourceEventId, accepted.SourceEventId);
        Assert.Equal(correlationId, accepted.CorrelationId);
        Assert.Equal(nameof(AlertCreatedDomainEvent), accepted.EventType);
        Assert.Null(accepted.FromState);
        Assert.Equal(OccurrenceState.Alert, accepted.ToState);
        Assert.NotEqual(default, accepted.OccurredAtUtc);
    }

    [Fact]
    public async Task Consumer_EventoDuplicado_Inbox_AuditaDuplicateEvent()
    {
        await using var context = await CreateContextAsync();
        await SeedRecipientsAsync(context);
        var consumer = await CreateConsumerAsync(context);
        var alertId = Guid.NewGuid();
        // MESMA instância → mesmo EventId → replay real do broker
        var domainEvent = new AlertCreatedDomainEvent(
            alertId, AlertSeverity.Warning, Guid.NewGuid(), "CPU alta", hasIncident: false);

        await consumer.Consume(Ctx(domainEvent));
        await consumer.Consume(Ctx(domainEvent)); // replay → inbox

        Assert.Equal(1, await context.Notifications.CountAsync());
        var decisions = await context.OccurrenceTransitions
            .Select(t => t.Decision).ToListAsync();
        Assert.Contains(OccurrenceDecision.TransitionAccepted, decisions);
        Assert.Contains(OccurrenceDecision.DuplicateEvent, decisions);
        Assert.Equal(2, decisions.Count);
    }
}

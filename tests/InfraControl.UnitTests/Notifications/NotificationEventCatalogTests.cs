using FluentAssertions;
using InfraControl.Application.Notifications.Events;
using InfraControl.Application.Notifications.Webhook;
using InfraControl.Domain.Events;
using InfraControl.Workers.Consumers.Notifications;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// Fase 05 (Plano Mestre §10): catálogo de eventos para o n8n — fonte
/// única dos tipos de evento que viram notificação (espelho EXATO dos
/// consumidores do NotificationEventConsumer) + payload do webhook com
/// <c>eventType</c> para o workflow ramificar sem parsear texto.
/// </summary>
public sealed class NotificationEventCatalogTests
{
    [Fact]
    public void Catalog_ShouldContainExactlyTheEventsConsumedByNotificationConsumer()
    {
        // Arranjo: reflete o que o NotificationEventConsumer implementa.
        string[] expected =
        [
            nameof(UserCreatedDomainEvent),
            nameof(UserDisabledDomainEvent),
            nameof(UserUpdatedDomainEvent),
            nameof(UserDeletedDomainEvent),
            nameof(UserRoleAssignedDomainEvent),
            nameof(UserRoleRemovedDomainEvent),
            nameof(InvitationAcceptedDomainEvent),
            nameof(IncidentCreatedDomainEvent),
            nameof(AlertCreatedDomainEvent),
            nameof(IncidentResolvedDomainEvent),
            nameof(IncidentUpdatedDomainEvent),
            nameof(AlertResolvedDomainEvent),
            nameof(PdiCreatedDomainEvent),
            nameof(PdiUpdatedDomainEvent),
            nameof(TaskItemCreatedDomainEvent),
            nameof(TaskAssigneeCreatedDomainEvent),
            nameof(TaskResolvedDomainEvent),
            nameof(ProjectStatusChangedDomainEvent),
        ];

        // Ação
        var types = NotificationEventCatalog.All.Select(e => e.EventType).ToArray();

        // Asserção
        types.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Catalog_ShouldExposeStableNamesAndPtBrDescriptions()
    {
        var catalog = NotificationEventCatalog.All;

        catalog.Should().OnlyContain(entry =>
            !string.IsNullOrWhiteSpace(entry.EventType)
            && !string.IsNullOrWhiteSpace(entry.Description)
            && !string.IsNullOrWhiteSpace(entry.Category));

        catalog.Select(e => e.EventType).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Catalog_ShouldGroupOperationalEventsSeparatelyFromIdentityAndPdi()
    {
        var operational = NotificationEventCatalog.ByCategory("Operational");
        var identity = NotificationEventCatalog.ByCategory("Identity");
        var pdi = NotificationEventCatalog.ByCategory("PDI");

        operational.Select(e => e.EventType).Should().BeEquivalentTo(
        [
            nameof(IncidentCreatedDomainEvent),
            nameof(AlertCreatedDomainEvent),
            nameof(IncidentResolvedDomainEvent),
            nameof(IncidentUpdatedDomainEvent),
            nameof(AlertResolvedDomainEvent),
        ]);
        identity.Select(e => e.EventType).Should().Contain(
            nameof(UserCreatedDomainEvent));
        pdi.Select(e => e.EventType).Should().BeEquivalentTo(
        [
            nameof(PdiCreatedDomainEvent),
            nameof(PdiUpdatedDomainEvent),
        ]);
    }

    [Fact]
    public void Catalog_ShouldMirrorNotificationConsumerImplementations()
    {
        // Garante que o catálogo não envelhece sozinho: cada EventType do
        // catálogo deve ter um consumidor correspondente no consumer.
        var consumerInterfaces = typeof(NotificationEventConsumer)
            .GetInterfaces()
            .Where(i => i.IsGenericType
                && i.GetGenericTypeDefinition().Name.StartsWith("IConsumer", StringComparison.Ordinal))
            .Select(i => i.GetGenericArguments()[0].Name)
            .ToArray();

        NotificationEventCatalog.All.Select(e => e.EventType)
            .Should().BeEquivalentTo(consumerInterfaces);
    }

    // ── Payload do webhook com eventType (Fase 05) ──────────────────

    [Fact]
    public void WebhookMessage_ShouldCarryEventTypeForN8nBranching()
    {
        var message = new WebhookNotificationMessage(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "https://n8n.exemplo.test/webhook/infracontrol",
            "Incidente aberto",
            "O incidente 'Carga de CPU' foi aberto (Critical).",
            DateTime.UtcNow,
            EventType: "IncidentCreatedDomainEvent");

        message.EventType.Should().Be("IncidentCreatedDomainEvent");
    }

    [Fact]
    public void WebhookMessage_ShouldDefaultEventTypeToUnknownWhenMissing()
    {
        // Compat: construções antigas (sem eventType) não quebram — o n8n
        // vê "Unknown" e cai no default do switch.
        var message = new WebhookNotificationMessage(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "https://n8n.exemplo.test/webhook/infracontrol",
            "Título",
            "Mensagem.",
            DateTime.UtcNow);

        message.EventType.Should().Be("Unknown");
    }

    [Fact]
    public void CatalogEntry_ShouldBeRecordWithCategoryAndDescription()
    {
        var entry = new NotificationEventCatalogEntry(
            nameof(IncidentCreatedDomainEvent),
            "Operational",
            "Incidente aberto no InfraControl (alerta crítico, webhook Zabbix ou abertura manual).");

        entry.EventType.Should().Be("IncidentCreatedDomainEvent");
        entry.Category.Should().Be("Operational");
        entry.Description.Should().Contain("Incidente");
    }
}

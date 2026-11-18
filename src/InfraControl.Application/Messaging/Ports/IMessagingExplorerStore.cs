using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Messaging.Ports;

/// <summary>Filtros do Message Explorer (F55-T004) — todos opcionais.</summary>
public sealed record MessagingMessageFilter(
    Guid? MessageId,
    string? CorrelationId,
    string? EventType,
    string? State,               // pending|processed|failed|exhausted
    string? Text,                // busca em payload/eventType
    int? MinAttempts,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page = 1,
    int PageSize = 25);

/// <summary>Filtros do explorer de deliveries (canais de notificação).</summary>
public sealed record MessagingDeliveryFilter(
    Guid? DeliveryId,
    Guid? NotificationId,
    string? Channel,
    string? Status,
    int? MinAttempts,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page = 1,
    int PageSize = 25);

/// <summary>
/// F55-T004: leitura paginada de outbox/inbox/deliveries para o Message
/// Explorer. Retorna entidades de domínio; o DTO sanitizado é montado na
/// camada Application.
/// </summary>
public interface IMessagingExplorerStore
{
    Task<PagedResult<OutboxMessage>> SearchOutboxAsync(
        MessagingMessageFilter filter, CancellationToken cancellationToken = default);

    Task<OutboxMessage?> GetOutboxAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>Registros de inbox (ProcessedMessage) ligados a um eventId.</summary>
    Task<IReadOnlyList<ProcessedMessage>> GetInboxByEventIdAsync(
        Guid eventId, CancellationToken cancellationToken = default);

    Task<PagedResult<NotificationDelivery>> SearchDeliveriesAsync(
        MessagingDeliveryFilter filter, CancellationToken cancellationToken = default);

    Task<NotificationDelivery?> GetDeliveryAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// F55-T005: deliveries Failed separando retry agendado
    /// (<paramref name="deadLetter"/>=false, NextAttemptAt futuro) de DLQ
    /// (<paramref name="deadLetter"/>=true, sem próxima tentativa).
    /// </summary>
    Task<PagedResult<NotificationDelivery>> SearchFailureDeliveriesAsync(
        MessagingDeliveryFilter filter, bool deadLetter,
        CancellationToken cancellationToken = default);
}

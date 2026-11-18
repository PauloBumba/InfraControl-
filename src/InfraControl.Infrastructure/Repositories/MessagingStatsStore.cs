using InfraControl.Application.Messaging.Ports;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F55-T001: contagens reais de mensageria no banco — outbox (pendente ×
/// esgotado × processado), inbox e deliveries por status (retry × DLQ
/// lógico = Failed sem NextAttemptAt). Latência = média do outbox nos
/// últimos 200 processados (OccurredAt → ProcessedAt), sempre que houver
/// dado; sem linha → null, nunca zero inventado.
/// </summary>
public sealed class MessagingStatsStore(AppDbContext dbContext)
    : IMessagingStatsStore
{
    private const int LatencySampleSize = 200;

    public async Task<MessagingAppStats> GetStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var outbox = dbContext.OutboxMessages.AsNoTracking();
        var outboxPending = await outbox
            .CountAsync(m => m.ProcessedAt == null && m.RetryCount < m.MaxRetries,
                cancellationToken);
        var outboxExhausted = await outbox
            .CountAsync(m => m.ProcessedAt == null && m.RetryCount >= m.MaxRetries,
                cancellationToken);
        var outboxProcessed = await outbox
            .CountAsync(m => m.ProcessedAt != null, cancellationToken);

        var inboxProcessed = await dbContext.ProcessedMessages.AsNoTracking()
            .LongCountAsync(cancellationToken);

        var recentProcessed = await dbContext.OutboxMessages.AsNoTracking()
            .Where(m => m.ProcessedAt != null)
            .OrderByDescending(m => m.ProcessedAt)
            .Take(LatencySampleSize)
            .Select(m => new { m.OccurredAt, m.ProcessedAt })
            .ToListAsync(cancellationToken);
        double? avgLatencySeconds = recentProcessed.Count == 0
            ? null
            : recentProcessed.Average(m =>
                (m.ProcessedAt!.Value - m.OccurredAt).TotalSeconds);

        var byStatus = await dbContext.NotificationDeliveries.AsNoTracking()
            .GroupBy(delivery => delivery.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Status, group => group.Count,
                cancellationToken);

        int CountOf(DeliveryStatus status) => byStatus.GetValueOrDefault(status);

        var retryDue = await dbContext.NotificationDeliveries.AsNoTracking()
            .CountAsync(
                delivery => delivery.Status == DeliveryStatus.Failed
                    && delivery.NextAttemptAt != null,
                cancellationToken);

        return new MessagingAppStats(
            OutboxPending: outboxPending,
            OutboxExhausted: outboxExhausted,
            OutboxProcessedTotal: outboxProcessed,
            AvgOutboxLatencySeconds: avgLatencySeconds,
            InboxProcessedTotal: inboxProcessed,
            DeliveriesPending: CountOf(DeliveryStatus.Pending),
            DeliveriesProcessing: CountOf(DeliveryStatus.Processing),
            DeliveriesSent: CountOf(DeliveryStatus.Sent),
            DeliveriesFailed: CountOf(DeliveryStatus.Failed),
            DeliveriesRetryDue: retryDue,
            DeliveriesDlq: CountOf(DeliveryStatus.Failed) - retryDue,
            DeliveriesSuppressed: CountOf(DeliveryStatus.Suppressed),
            DeliveriesCancelled: CountOf(DeliveryStatus.Cancelled));
    }
}

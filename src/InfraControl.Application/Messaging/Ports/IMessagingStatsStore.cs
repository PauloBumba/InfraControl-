namespace InfraControl.Application.Messaging.Ports;

/// <summary>
/// F55-T001: contagens da aplicação (banco) para o overview de mensageria —
/// outbox, inbox e deliveries. Fonte "database", sem simulação.
/// </summary>
public sealed record MessagingAppStats(
    int OutboxPending,
    int OutboxExhausted,
    long OutboxProcessedTotal,
    double? AvgOutboxLatencySeconds,
    long InboxProcessedTotal,
    int DeliveriesPending,
    int DeliveriesProcessing,
    int DeliveriesSent,
    int DeliveriesFailed,
    int DeliveriesRetryDue,
    int DeliveriesDlq,
    int DeliveriesSuppressed,
    int DeliveriesCancelled);

/// <summary>Porta de leitura das estatísticas de mensageria no banco.</summary>
public interface IMessagingStatsStore
{
    Task<MessagingAppStats> GetStatsAsync(
        CancellationToken cancellationToken = default);
}

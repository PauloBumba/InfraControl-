using InfraControl.Application.Messaging.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Messaging.Flow;

/// <summary>Estágio do pipeline de mensageria com métricas reais por fonte.</summary>
public sealed record MessagingFlowStageDto(
    string Id,
    string Name,
    string Service,
    string Description,
    string Status,               // ok|degraded|unavailable|paused
    IReadOnlyDictionary<string, MessagingMetricDto> Metrics);

public sealed record MessagingMetricDto(
    string? Value,
    string Source);              // "broker"|"database"|"indisponível"

public sealed record MessagingFlowResult(
    bool BrokerReachable,
    string? BrokerUnavailableReason,
    IReadOnlyList<MessagingFlowStageDto> Stages);

/// <summary>
/// F55-T006: Message Flow — estágios reais do pipeline
/// (Outbox → Broker → Consumers → Inbox → Deliveries → Canais) com
/// contagens verdadeiras de cada fonte. TraceId/CorrelationId ficam nos
/// logs/traces (Message Explorer), não como label de métrica.
/// </summary>
public sealed record GetMessagingFlowQuery
    : BaseQuery<MessagingFlowResult>, IAuthorizedRequest
{
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(10);

    public override IReadOnlyCollection<string> CacheTags => ["messaging"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetMessagingFlowQueryHandler(
    IRabbitMqManagementClient broker,
    IMessagingStatsStore stats)
    : IRequestHandler<GetMessagingFlowQuery, MessagingFlowResult>
{
    public async Task<MessagingFlowResult> Handle(
        GetMessagingFlowQuery request, CancellationToken cancellationToken)
    {
        var overviewTask = broker.GetOverviewAsync(cancellationToken);
        var statsTask = stats.GetStatsAsync(cancellationToken);
        await Task.WhenAll(overviewTask, statsTask);
        var overview = overviewTask.Result;
        var app = statsTask.Result;

        var brokerOk = overview.Reachable;
        var stages = new List<MessagingFlowStageDto>
        {
            new("outbox", "Outbox (API)", "InfraControl.Api",
                "Eventos de domínio persistidos antes de publicar no broker.",
                app.OutboxPending + app.OutboxExhausted > 0 ? "degraded" : "ok",
                new Dictionary<string, MessagingMetricDto>
                {
                    ["Pendentes"] = M(app.OutboxPending.ToString(), "database"),
                    ["Esgotados"] = M(app.OutboxExhausted.ToString(), "database"),
                    ["Processados"] = M(app.OutboxProcessedTotal.ToString(), "database"),
                    ["Latência média"] = app.AvgOutboxLatencySeconds is { } lat
                        ? M($"{lat:F2}s", "database") : M(null, "indisponível"),
                }),
            new("broker", "RabbitMQ Broker", "rabbitmq",
                "Filas, exchanges, consumers e throughput do broker.",
                brokerOk ? "ok" : "unavailable",
                new Dictionary<string, MessagingMetricDto>
                {
                    ["Filas"] = brokerOk ? M(overview.Queues.ToString(), "broker") : M(null, "indisponível"),
                    ["Consumers"] = brokerOk ? M(overview.Consumers.ToString(), "broker") : M(null, "indisponível"),
                    ["Ready"] = brokerOk ? M(overview.MessagesReady.ToString(), "broker") : M(null, "indisponível"),
                    ["Unacked"] = brokerOk ? M(overview.MessagesUnacked.ToString(), "broker") : M(null, "indisponível"),
                    ["Publish/s"] = brokerOk && overview.PublishRatePerSecond is { } pr
                        ? M($"{pr:F1}", "broker") : M(null, "indisponível"),
                    ["Deliver/s"] = brokerOk && overview.DeliverRatePerSecond is { } dr
                        ? M($"{dr:F1}", "broker") : M(null, "indisponível"),
                }),
            new("inbox", "Inbox (deduplicação)", "InfraControl.Workers",
                "ProcessedMessage — garante exactly-once por consumer.",
                "ok",
                new Dictionary<string, MessagingMetricDto>
                {
                    ["Processados"] = M(app.InboxProcessedTotal.ToString(), "database"),
                }),
            new("deliveries", "NotificationDelivery", "InfraControl.Workers",
                "Entregas por canal: pending → processing → sent/failed/DLQ.",
                app.DeliveriesDlq + app.DeliveriesFailed > 0 ? "degraded" : "ok",
                new Dictionary<string, MessagingMetricDto>
                {
                    ["Pendentes"] = M(app.DeliveriesPending.ToString(), "database"),
                    ["Processando"] = M(app.DeliveriesProcessing.ToString(), "database"),
                    ["Enviadas"] = M(app.DeliveriesSent.ToString(), "database"),
                    ["Falhas"] = M(app.DeliveriesFailed.ToString(), "database"),
                    ["Retry devido"] = M(app.DeliveriesRetryDue.ToString(), "database"),
                    ["DLQ"] = M(app.DeliveriesDlq.ToString(), "database"),
                    ["Suprimidas"] = M(app.DeliveriesSuppressed.ToString(), "database"),
                    ["Canceladas"] = M(app.DeliveriesCancelled.ToString(), "database"),
                }),
        };

        return new MessagingFlowResult(
            BrokerReachable: brokerOk,
            BrokerUnavailableReason: overview.UnavailableReason,
            Stages: stages);
    }

    private static MessagingMetricDto M(string? value, string source)
        => new(value, source);
}

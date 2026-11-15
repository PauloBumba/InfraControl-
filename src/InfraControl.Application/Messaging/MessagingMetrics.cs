using System.Diagnostics.Metrics;

namespace InfraControl.Application.Messaging;

/// <summary>
/// F55-T010: métricas OTel do pipeline de mensageria
/// (<c>infracontrol_messaging_*</c>). Labels de baixa cardinalidade
/// (source/channel/result/queue) — MessageId/CorrelationId ficam em
/// logs e traces, NUNCA como label.
/// </summary>
public static class MessagingMetrics
{
    public const string MeterName = "InfraControl.Messaging";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> MessagesProcessed = Meter.CreateCounter<long>(
        "infracontrol_messaging_messages_processed_total",
        description: "Mensagens processadas com sucesso por fonte.");

    private static readonly Counter<long> MessagesFailed = Meter.CreateCounter<long>(
        "infracontrol_messaging_messages_failed_total",
        description: "Mensagens que falharam por fonte.");

    private static readonly Counter<long> Retries = Meter.CreateCounter<long>(
        "infracontrol_messaging_retry_total",
        description: "Retries agendados por fonte.");

    private static readonly Histogram<double> ProcessingDuration =
        Meter.CreateHistogram<double>(
            "infracontrol_messaging_processing_duration_seconds",
            unit: "s",
            description: "Duração do processamento por fonte.");

    /// <summary>Último snapshot do broker para os gauges (queue ready/unacked).</summary>
    public static readonly BrokerSnapshotSnapshot BrokerSnapshot = new();

    public static void Processed(string source, string? channel = null)
        => MessagesProcessed.Add(1, Tags(source, channel));

    public static void Failed(string source, string? channel = null)
        => MessagesFailed.Add(1, Tags(source, channel));

    public static void Retry(string source, string? channel = null)
        => Retries.Add(1, Tags(source, channel));

    public static void Duration(string source, double seconds, string? channel = null)
        => ProcessingDuration.Record(seconds, Tags(source, channel));

    private static KeyValuePair<string, object?>[] Tags(
        string source, string? channel)
    {
        var tags = new List<KeyValuePair<string, object?>>
        {
            Tag("source", source)
        };
        if (!string.IsNullOrEmpty(channel)) tags.Add(Tag("channel", channel));
        return tags.ToArray();
    }

    private static KeyValuePair<string, object?> Tag(string key, string value)
        => new(key, value);

    /// <summary>
    /// Snapshot thread-safe das filas do broker — alimentado pelo
    /// MessagingBrokerMetricsWorker (poll 30s). Vazio = broker indisponível
    /// (gauges reportam 0 medições, nunca valor inventado).
    /// </summary>
    public sealed class BrokerSnapshotSnapshot
    {
        private volatile IReadOnlyList<QueueGauge> _queues = [];

        public void Update(IReadOnlyList<QueueGauge> queues) => _queues = queues;

        public IReadOnlyList<QueueGauge> Queues => _queues;

        public void Clear() => _queues = [];
    }

    public sealed record QueueGauge(
        string Name, long Ready, long Unacked, int Consumers);

    /// <summary>Registra os gauges observáveis — chamar uma vez no boot.</summary>
    public static void RegisterGauges()
    {
        Meter.CreateObservableGauge("infracontrol_messaging_queue_ready",
            () => BrokerSnapshot.Queues.Select(q => new Measurement<long>(
                q.Ready, Tag("queue", q.Name))),
            unit: "{message}",
            description: "Mensagens ready por fila (snapshot do broker).");

        Meter.CreateObservableGauge("infracontrol_messaging_queue_unacked",
            () => BrokerSnapshot.Queues.Select(q => new Measurement<long>(
                q.Unacked, Tag("queue", q.Name))),
            unit: "{message}",
            description: "Mensagens unacked por fila (snapshot do broker).");

        Meter.CreateObservableGauge("infracontrol_messaging_worker_status",
            () => BrokerSnapshot.Queues.Select(q => new Measurement<double>(
                q.Consumers > 0 ? 1 : 0, Tag("queue", q.Name))),
            unit: "{status}",
            description: "1 = fila com consumers ativos, 0 = sem consumers.");
    }
}

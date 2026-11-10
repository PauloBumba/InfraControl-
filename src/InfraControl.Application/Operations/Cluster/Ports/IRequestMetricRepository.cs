using InfraControl.Domain.Entities;

namespace InfraControl.Application.Operations.Cluster.Ports;

/// <summary>
/// Delta agregado de um minuto para persistência — produzido pelo
/// <c>RequestMetricsRecorder</c> (memória) e gravado pelo flusher.
/// LatencyBuckets: [≤50ms, ≤200ms, ≤500ms, ≤1500ms, &gt;1500ms].
/// </summary>
public sealed record RequestMetricDelta(
    DateTime MinuteUtc,
    string Route,
    string StatusClass,
    string Instance,
    int Count,
    int Errors,
    long LatencySumMs,
    int[] LatencyBuckets);

/// <summary>Persistência e leitura dos baldes de métricas de requisição.</summary>
public interface IRequestMetricRepository
{
    /// <summary>Buckets no intervalo [fromUtc, toUtc) para agregação.</summary>
    Task<IReadOnlyList<RequestMetricBucket>> GetBucketsAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>Merge incremental dos deltas (insert ou soma no bucket existente).</summary>
    Task UpsertDeltasAsync(
        IReadOnlyList<RequestMetricDelta> deltas, CancellationToken cancellationToken = default);
}

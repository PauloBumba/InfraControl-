using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Balde agregado de métricas de requisição HTTP (tráfego interno da
/// plataforma). Uma linha por (minuto, rota, classe de status) — o
/// <c>RequestMetricsFlushService</c> faz merge incremental, então a tabela
/// cresce ~1 linha/minuto/rota em vez de 1 linha/request.
/// Os campos <c>Lat*</c> são histograma de latência em ms para estimar
/// P50/P95/P99 sem guardar cada amostra.
/// </summary>
public sealed class RequestMetricBucket : Entity
{
    public const int RouteMaxLength = 200;
    public const int StatusClassMaxLength = 3;
    public const int InstanceMaxLength = 120;

    /// <summary>Minuto UTC truncado (ex.: 2026-11-08 03:45:00).</summary>
    public DateTime MinuteUtc { get; private set; }

    /// <summary>Template da rota (ex.: "api/v1/admin/cluster/overview").</summary>
    public string Route { get; private set; } = string.Empty;

    /// <summary>Classe do status HTTP: "1xx".."5xx" ou "err" para exceção.</summary>
    public string StatusClass { get; private set; } = string.Empty;

    /// <summary>
    /// Instância que atendeu a requisição (hostname do container / réplica).
    /// Vazio em linhas antigas anteriores à dimensão de instância.
    /// </summary>
    public string Instance { get; private set; } = string.Empty;

    public int Count { get; private set; }
    public int Errors { get; private set; }
    public long LatencySumMs { get; private set; }

    // Histograma de latência (limites superiores em ms): ≤50, ≤200, ≤500,
    // ≤1500, >1500.
    public int LatLe50 { get; private set; }
    public int LatLe200 { get; private set; }
    public int LatLe500 { get; private set; }
    public int LatLe1500 { get; private set; }
    public int LatGt1500 { get; private set; }

    private RequestMetricBucket() { }

    public static RequestMetricBucket Create(
        DateTime minuteUtc, string route, string statusClass, string instance,
        int count, int errors, long latencySumMs,
        int latLe50, int latLe200, int latLe500, int latLe1500, int latGt1500)
    {
        var bucket = new RequestMetricBucket();
        bucket.MinuteUtc = minuteUtc;
        bucket.Route = route.Length > RouteMaxLength ? route[..RouteMaxLength] : route;
        bucket.StatusClass = statusClass;
        bucket.Instance = instance.Length > InstanceMaxLength
            ? instance[..InstanceMaxLength]
            : instance;
        bucket.Count = count;
        bucket.Errors = errors;
        bucket.LatencySumMs = latencySumMs;
        bucket.LatLe50 = latLe50;
        bucket.LatLe200 = latLe200;
        bucket.LatLe500 = latLe500;
        bucket.LatLe1500 = latLe1500;
        bucket.LatGt1500 = latGt1500;
        return bucket;
    }

    /// <summary>Merge incremental de um delta do recorder em memória.</summary>
    public void Merge(
        int count, int errors, long latencySumMs,
        int latLe50, int latLe200, int latLe500, int latLe1500, int latGt1500)
    {
        Count += count;
        Errors += errors;
        LatencySumMs += latencySumMs;
        LatLe50 += latLe50;
        LatLe200 += latLe200;
        LatLe500 += latLe500;
        LatLe1500 += latLe1500;
        LatGt1500 += latGt1500;
    }
}

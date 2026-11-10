using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Operations.Cluster.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Operations.Cluster;

// ------------------------------------------------------------------
// Tráfego interno da plataforma (spec evolução §4): volume, erros e
// latência por rota agregados dos RequestMetricBucket — sem Prometheus,
// os dados nascem no middleware e vivem no próprio Postgres.
// ------------------------------------------------------------------

public sealed record RouteTrafficDto(
    string Route,
    int Requests,
    int Errors,
    double ErrorRatePercent,
    double AvgLatencyMs,
    int? P50Ms,
    int? P95Ms,
    int? P99Ms);

public sealed record MinuteTrafficDto(
    DateTime MinuteUtc,
    int Requests,
    int Errors);

/// <summary>Distribuição de requisições de uma instância/réplica na janela.</summary>
public sealed record InstanceTrafficDto(
    string Instance,
    int Requests,
    int Errors,
    double AvgLatencyMs,
    double SharePercent);

public sealed record TrafficMetricsDto(
    int WindowMinutes,
    long TotalRequests,
    long TotalErrors,
    double ErrorRatePercent,
    double AvgLatencyMs,
    int? P50Ms,
    int? P95Ms,
    int? P99Ms,
    double RequestsPerMinute,
    IReadOnlyList<RouteTrafficDto> Routes,
    IReadOnlyList<MinuteTrafficDto> Timeline,
    IReadOnlyList<InstanceTrafficDto> Instances);

public sealed record GetTrafficMetricsQuery(int WindowMinutes = 60)
    : IQuery<TrafficMetricsDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed class GetTrafficMetricsQueryHandler(IRequestMetricRepository repo)
    : IRequestHandler<GetTrafficMetricsQuery, TrafficMetricsDto>
{
    // Limites do histograma persistido (ms) — índice 4 = ">1500".
    private static readonly int[] BucketUpperBounds = [50, 200, 500, 1500, int.MaxValue];

    public async Task<TrafficMetricsDto> Handle(
        GetTrafficMetricsQuery request, CancellationToken cancellationToken)
    {
        var minutes = Math.Clamp(request.WindowMinutes, 5, 24 * 60);
        var to = DateTime.UtcNow;
        var from = to.AddMinutes(-minutes);

        var buckets = await repo.GetBucketsAsync(from, to, cancellationToken);

        // Agregação global (todos os status) para resumo + percentis.
        var globalCounts = new int[5];
        long totalRequests = 0, totalErrors = 0, latencySum = 0;
        var byRoute = new Dictionary<string, RouteAccum>(StringComparer.OrdinalIgnoreCase);
        var byMinute = new Dictionary<DateTime, (int Req, int Err)>();
        var byInstance = new Dictionary<string, InstanceAccum>(StringComparer.OrdinalIgnoreCase);

        foreach (var b in buckets)
        {
            totalRequests += b.Count;
            totalErrors += b.Errors;
            latencySum += b.LatencySumMs;
            globalCounts[0] += b.LatLe50;
            globalCounts[1] += b.LatLe200;
            globalCounts[2] += b.LatLe500;
            globalCounts[3] += b.LatLe1500;
            globalCounts[4] += b.LatGt1500;

            if (!byRoute.TryGetValue(b.Route, out var r))
                byRoute[b.Route] = r = new RouteAccum();
            r.Requests += b.Count;
            r.Errors += b.Errors;
            r.LatencySum += b.LatencySumMs;
            r.Buckets[0] += b.LatLe50;
            r.Buckets[1] += b.LatLe200;
            r.Buckets[2] += b.LatLe500;
            r.Buckets[3] += b.LatLe1500;
            r.Buckets[4] += b.LatGt1500;

            var minute = b.MinuteUtc;
            var cur = byMinute.TryGetValue(minute, out var m) ? m : (0, 0);
            byMinute[minute] = (cur.Item1 + b.Count, cur.Item2 + b.Errors);

            var inst = b.Instance.Length > 0 ? b.Instance : "desconhecida";
            if (!byInstance.TryGetValue(inst, out var ia))
                byInstance[inst] = ia = new InstanceAccum();
            ia.Requests += b.Count;
            ia.Errors += b.Errors;
            ia.LatencySum += b.LatencySumMs;
        }

        var routes = byRoute
            .Select(kv => ToRouteDto(kv.Key, kv.Value))
            .OrderByDescending(r => r.Requests)
            .ToArray();

        var timeline = byMinute
            .OrderBy(kv => kv.Key)
            .Select(kv => new MinuteTrafficDto(kv.Key, kv.Value.Req, kv.Value.Err))
            .ToArray();

        var instances = byInstance
            .Select(kv => new InstanceTrafficDto(
                kv.Key,
                kv.Value.Requests,
                kv.Value.Errors,
                kv.Value.Requests > 0
                    ? Math.Round(kv.Value.LatencySum / (double)kv.Value.Requests, 1)
                    : 0,
                totalRequests > 0
                    ? Math.Round(kv.Value.Requests * 100.0 / totalRequests, 1)
                    : 0))
            .OrderByDescending(i => i.Requests)
            .ToArray();

        return new TrafficMetricsDto(
            minutes,
            totalRequests,
            totalErrors,
            totalRequests > 0 ? Math.Round(totalErrors * 100.0 / totalRequests, 2) : 0,
            totalRequests > 0 ? Math.Round(latencySum / (double)totalRequests, 1) : 0,
            Percentile(globalCounts, 50),
            Percentile(globalCounts, 95),
            Percentile(globalCounts, 99),
            Math.Round(totalRequests / (double)minutes, 2),
            routes,
            timeline,
            instances);
    }

    private static RouteTrafficDto ToRouteDto(string route, RouteAccum r)
    {
        return new RouteTrafficDto(
            route,
            r.Requests,
            r.Errors,
            r.Requests > 0 ? Math.Round(r.Errors * 100.0 / r.Requests, 2) : 0,
            r.Requests > 0 ? Math.Round(r.LatencySum / (double)r.Requests, 1) : 0,
            Percentile(r.Buckets, 50),
            Percentile(r.Buckets, 95),
            Percentile(r.Buckets, 99));
    }

    /// <summary>
    /// Percentil aproximado pelo histograma: retorna o limite superior do
    /// primeiro bucket cuja frequência acumulada atinge o percentil.
    /// No bucket ">1500" retorna 1500 como piso (o valor real é maior).
    /// </summary>
    private static int? Percentile(int[] buckets, int percentile)
    {
        var total = buckets.Sum();
        if (total == 0) return null;
        var target = (long)Math.Ceiling(total * percentile / 100.0);
        long cumulative = 0;
        for (var i = 0; i < buckets.Length; i++)
        {
            cumulative += buckets[i];
            if (cumulative >= target)
                return i == buckets.Length - 1 ? 1500 : BucketUpperBounds[i];
        }
        return null;
    }

    private sealed class RouteAccum
    {
        public int Requests;
        public int Errors;
        public long LatencySum;
        public readonly int[] Buckets = new int[5];
    }

    private sealed class InstanceAccum
    {
        public int Requests;
        public int Errors;
        public long LatencySum;
    }
}

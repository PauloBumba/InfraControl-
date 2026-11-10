using System.Collections.Concurrent;
using InfraControl.Application.Operations.Cluster.Ports;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Acumulador em memória das requisições HTTP (singleton). O middleware
/// chama <see cref="Record"/> por request; o
/// <see cref="RequestMetricsFlushService"/> drena a cada 30s e faz merge
/// no Postgres — o caminho quente não toca o banco.
/// </summary>
public sealed class RequestMetricsRecorder
{
    private sealed record Key(DateTime MinuteUtc, string Route, string StatusClass, string Instance);

    private sealed class Accum
    {
        public int Count;
        public int Errors;
        public long LatencySumMs;
        public readonly int[] Buckets = new int[5];
    }

    /// <summary>
    /// Instância que atende as requisições deste processo — hostname do
    /// container (único por réplica). Sobrescrevível via INFRA_INSTANCE_NAME
    /// para batizar réplicas de forma legível.
    /// </summary>
    private static readonly string Instance =
        Environment.GetEnvironmentVariable("INFRA_INSTANCE_NAME") is { Length: > 0 } custom
            ? custom
            : Environment.GetEnvironmentVariable("HOSTNAME") is { Length: > 0 } host
                ? host
                : "desconhecida";

    private ConcurrentDictionary<Key, Accum> _map = new();

    /// <summary>Registra uma requisição finalizada.</summary>
    public void Record(string route, int statusCode, double elapsedMs)
    {
        var minute = DateTime.UtcNow;
        minute = new DateTime(
            minute.Year, minute.Month, minute.Day,
            minute.Hour, minute.Minute, 0, DateTimeKind.Utc);

        var statusClass = statusCode >= 100 && statusCode < 600
            ? $"{statusCode / 100}xx"
            : "err";
        var key = new Key(minute, route, statusClass, Instance);

        var acc = _map.GetOrAdd(key, _ => new Accum());
        lock (acc)
        {
            acc.Count++;
            if (statusCode >= 400) acc.Errors++;
            acc.LatencySumMs += (long)elapsedMs;
            var b = elapsedMs <= 50 ? 0
                : elapsedMs <= 200 ? 1
                : elapsedMs <= 500 ? 2
                : elapsedMs <= 1500 ? 3
                : 4;
            acc.Buckets[b]++;
        }
    }

    /// <summary>Drena os deltas acumulados (troca o mapa por um novo).</summary>
    public IReadOnlyList<RequestMetricDelta> Drain()
    {
        var old = Interlocked.Exchange(ref _map, new ConcurrentDictionary<Key, Accum>());
        var deltas = new List<RequestMetricDelta>(old.Count);
        foreach (var (key, acc) in old)
        {
            lock (acc)
            {
                if (acc.Count == 0) continue;
                deltas.Add(new RequestMetricDelta(
                    key.MinuteUtc, key.Route, key.StatusClass, key.Instance,
                    acc.Count, acc.Errors, acc.LatencySumMs,
                    (int[])acc.Buckets.Clone()));
            }
        }
        return deltas;
    }
}

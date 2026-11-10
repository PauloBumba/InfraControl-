using InfraControl.Application.Operations.Cluster.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>Merge incremental dos baldes de métricas de requisição HTTP.</summary>
public sealed class RequestMetricRepository(AppDbContext dbContext)
    : IRequestMetricRepository
{
    public async Task<IReadOnlyList<RequestMetricBucket>> GetBucketsAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
        await dbContext.RequestMetricBuckets.AsNoTracking()
            .Where(b => b.MinuteUtc >= fromUtc && b.MinuteUtc < toUtc)
            .ToListAsync(cancellationToken);

    public async Task UpsertDeltasAsync(
        IReadOnlyList<RequestMetricDelta> deltas, CancellationToken cancellationToken = default)
    {
        if (deltas.Count == 0) return;

        var minutes = deltas.Select(d => d.MinuteUtc).Distinct().ToArray();
        var routes = deltas.Select(d => d.Route).Distinct().ToArray();
        var existing = await dbContext.RequestMetricBuckets
            .Where(b => minutes.Contains(b.MinuteUtc) && routes.Contains(b.Route))
            .ToListAsync(cancellationToken);
        var lookup = existing.ToDictionary(
            b => (b.MinuteUtc, b.Route, b.StatusClass, b.Instance));

        foreach (var d in deltas)
        {
            var key = (d.MinuteUtc, d.Route, d.StatusClass, d.Instance);
            if (lookup.TryGetValue(key, out var bucket))
            {
                bucket.Merge(
                    d.Count, d.Errors, d.LatencySumMs,
                    d.LatencyBuckets.ElementAtOrDefault(0),
                    d.LatencyBuckets.ElementAtOrDefault(1),
                    d.LatencyBuckets.ElementAtOrDefault(2),
                    d.LatencyBuckets.ElementAtOrDefault(3),
                    d.LatencyBuckets.ElementAtOrDefault(4));
            }
            else
            {
                var created = RequestMetricBucket.Create(
                    d.MinuteUtc, d.Route, d.StatusClass, d.Instance,
                    d.Count, d.Errors, d.LatencySumMs,
                    d.LatencyBuckets.ElementAtOrDefault(0),
                    d.LatencyBuckets.ElementAtOrDefault(1),
                    d.LatencyBuckets.ElementAtOrDefault(2),
                    d.LatencyBuckets.ElementAtOrDefault(3),
                    d.LatencyBuckets.ElementAtOrDefault(4));
                dbContext.RequestMetricBuckets.Add(created);
                lookup[key] = created;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

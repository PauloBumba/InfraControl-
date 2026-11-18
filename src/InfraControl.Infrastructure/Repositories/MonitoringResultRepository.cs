using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class MonitoringResultRepository(AppDbContext dbContext)
    : IMonitoringResultRepository
{
    public async Task<MonitoringResult?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => await dbContext.MonitoringResults
            .SingleOrDefaultAsync(result => result.Id == id, cancellationToken);

    public async Task AddAsync(
        MonitoringResult entity, CancellationToken cancellationToken = default)
        => await dbContext.MonitoringResults.AddAsync(entity, cancellationToken);

    public void Remove(MonitoringResult entity)
        => dbContext.MonitoringResults.Remove(entity);

    public async Task<IReadOnlyList<PollCountRow>> GetPollCountsByMinuteAsync(
        DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken = default)
        => await BucketPollsAsync(
            fromUtc, toUtc, byMinute: true, cancellationToken);

    public async Task<IReadOnlyList<PollCountRow>> GetPollCountsByHourAsync(
        DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken = default)
        => await BucketPollsAsync(
            fromUtc, toUtc, byMinute: false, cancellationToken);

    /// <summary>
    /// Agregação SQL por (Y,M,D,H[,Min]) — o provedor traduz date_part/
    /// strftime nos dois bancos (mesmo GroupBy do TopologyRepository);
    /// montagem do DateTime fica em memória (F54-T003).
    /// </summary>
    private async Task<IReadOnlyList<PollCountRow>> BucketPollsAsync(
        DateTime fromUtc, DateTime toUtc, bool byMinute,
        CancellationToken cancellationToken)
    {
        var source = dbContext.MonitoringResults
            .AsNoTracking()
            .Where(result => result.CreateDateTime >= fromUtc
                && result.CreateDateTime < toUtc);

        var grouped = byMinute
            ? source
                .GroupBy(result => new
                {
                    result.CreateDateTime.Year,
                    result.CreateDateTime.Month,
                    result.CreateDateTime.Day,
                    result.CreateDateTime.Hour,
                    result.CreateDateTime.Minute
                })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Month,
                    group.Key.Day,
                    group.Key.Hour,
                    group.Key.Minute,
                    Total = group.Count(),
                    Failed = group.Count(result => !result.Succeeded)
                })
            : source
                .GroupBy(result => new
                {
                    result.CreateDateTime.Year,
                    result.CreateDateTime.Month,
                    result.CreateDateTime.Day,
                    result.CreateDateTime.Hour
                })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Month,
                    group.Key.Day,
                    group.Key.Hour,
                    Minute = 0,
                    Total = group.Count(),
                    Failed = group.Count(result => !result.Succeeded)
                });

        var rows = await grouped.ToListAsync(cancellationToken);
        return rows
            .Select(row => new PollCountRow(
                new DateTime(row.Year, row.Month, row.Day, row.Hour,
                    row.Minute, 0, DateTimeKind.Utc),
                row.Total, row.Failed))
            .OrderBy(row => row.BucketUtc)
            .ToList();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<MonitoringResult>> GetRecentByDeviceAsync(
        Guid deviceId, int limit, CancellationToken cancellationToken = default)
        => await dbContext.MonitoringResults
            .AsNoTracking()
            .Where(result => result.DeviceId == deviceId)
            .OrderByDescending(result => result.CreateDateTime)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MonitoringResult>> GetRecentByDeviceAndKindAsync(
        Guid deviceId,
        Domain.Enums.CheckKind kind,
        int limit,
        CancellationToken cancellationToken = default)
        => await dbContext.MonitoringResults
            .AsNoTracking()
            .Where(result => result.DeviceId == deviceId && result.Kind == kind)
            .OrderByDescending(result => result.CreateDateTime)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MonitoringResult>> GetLatestPingByDeviceAsync(
        CancellationToken cancellationToken = default)
    {
        // Mesmo GroupBy→First já traduzido no TopologyRepository (F54-T002:
        // status agregado do dashboard — um único SELECT, sem N+1).
        var rows = await dbContext.MonitoringResults
            .AsNoTracking()
            .Where(result => result.Kind == Domain.Enums.CheckKind.Ping)
            .GroupBy(result => result.DeviceId)
            .Select(group => new
            {
                Latest = group
                    .OrderByDescending(result => result.CreateDateTime)
                    .First()
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => row.Latest).ToList();
    }

    public async Task<IReadOnlyList<MonitoringResult>> GetRecentAsync(
        int limit, CancellationToken cancellationToken = default)
        => await dbContext.MonitoringResults
            .AsNoTracking()
            .OrderByDescending(result => result.CreateDateTime)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MonitoringAvailabilityCell>> GetAvailabilityCellsAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.MonitoringResults
            .AsNoTracking()
            .Where(result =>
                result.CreateDateTime >= fromUtc && result.CreateDateTime <= toUtc)
            .GroupBy(result => new
            {
                result.CreateDateTime.Year,
                result.CreateDateTime.Month,
                result.CreateDateTime.Day,
                result.CreateDateTime.Hour,
                MinuteBucket = result.CreateDateTime.Minute / 5,
            })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                group.Key.Hour,
                group.Key.MinuteBucket,
                Total = group.Count(),
                Succeeded = group.Sum(result => result.Succeeded ? 1 : 0),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new MonitoringAvailabilityCell(
                row.Year, row.Month, row.Day, row.Hour,
                row.MinuteBucket, row.Total, row.Succeeded))
            .ToList();
    }

    public async Task<IReadOnlyList<DeviceKindPollCount>> GetSuccessCountsByDeviceKindAsync(
        DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.MonitoringResults
            .AsNoTracking()
            .Where(result => result.CreateDateTime >= sinceUtc)
            .GroupBy(result => new { result.DeviceId, result.Kind })
            .Select(group => new
            {
                group.Key.DeviceId,
                group.Key.Kind,
                Total = group.Count(),
                Succeeded = group.Sum(result => result.Succeeded ? 1 : 0),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new DeviceKindPollCount(
                row.DeviceId, row.Kind, row.Total, row.Succeeded))
            .ToList();
    }
}

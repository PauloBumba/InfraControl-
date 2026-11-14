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
}

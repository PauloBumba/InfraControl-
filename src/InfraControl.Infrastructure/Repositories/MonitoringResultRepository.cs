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
}

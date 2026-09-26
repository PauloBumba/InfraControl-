using InfraControl.Application.Monitoring.Checks.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class MonitoringCheckRepository(AppDbContext dbContext)
    : IMonitoringCheckRepository
{
    public async Task<MonitoringCheck?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => await dbContext.MonitoringChecks
            .SingleOrDefaultAsync(check => check.Id == id, cancellationToken);

    public async Task AddAsync(MonitoringCheck entity, CancellationToken cancellationToken = default)
        => await dbContext.MonitoringChecks.AddAsync(entity, cancellationToken);

    public void Remove(MonitoringCheck entity)
        => dbContext.MonitoringChecks.Remove(entity);

    public async Task<IReadOnlyList<MonitoringCheck>> GetByDeviceAsync(
        Guid deviceId, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.MonitoringChecks
            .AsNoTracking()
            .Where(check => check.DeviceId == deviceId)
            .ToListAsync(cancellationToken);

        // Ordem do enum (Ping primeiro), não a ordem alfabética da coluna
        // string — lista pequena (máx. 20), sort em memória.
        return rows.OrderBy(check => check.Kind).ToList();
    }

    public async Task<IReadOnlyList<MonitoringCheck>> GetEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.MonitoringChecks
            .AsNoTracking()
            .Where(check => check.Enabled)
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(check => check.DeviceId)
            .ThenBy(check => check.Kind)
            .ToList();
    }

    public async Task SynchronizeFromProfileAsync(
        Guid deviceId,
        IEnumerable<CheckKind> checks,
        CancellationToken cancellationToken = default)
    {
        var desired = checks.Distinct().ToList();

        var existing = await dbContext.MonitoringChecks
            .Where(check => check.DeviceId == deviceId)
            .ToListAsync(cancellationToken);

        foreach (var row in existing.Where(row => !desired.Contains(row.Kind)))
            dbContext.MonitoringChecks.Remove(row);

        var present = existing.Select(row => row.Kind).ToHashSet();
        foreach (var kind in desired.Where(kind => !present.Contains(kind)))
            dbContext.MonitoringChecks.Add(MonitoringCheck.Create(deviceId, kind));
    }
}

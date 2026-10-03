using InfraControl.Application.Monitoring.Events.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// ADR-027: eventos externos — lookup dedup por (Source, ExternalEventId)
/// com tracking (MarkRecovered na recuperação).
/// </summary>
public sealed class MonitoringEventRepository(AppDbContext dbContext)
    : IMonitoringEventRepository
{
    public Task<MonitoringEvent?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.MonitoringEvents
            .FirstOrDefaultAsync(monitoringEvent => monitoringEvent.Id == id, cancellationToken);

    public async Task AddAsync(
        MonitoringEvent entity, CancellationToken cancellationToken = default)
        => await dbContext.MonitoringEvents.AddAsync(entity, cancellationToken);

    public void Remove(MonitoringEvent entity) => dbContext.MonitoringEvents.Remove(entity);

    public Task<MonitoringEvent?> GetBySourceAndExternalEventIdAsync(
        MonitoringSource source,
        string externalEventId,
        CancellationToken cancellationToken = default)
        => dbContext.MonitoringEvents
            .FirstOrDefaultAsync(monitoringEvent =>
                monitoringEvent.Source == source
                && monitoringEvent.ExternalEventId == externalEventId,
                cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

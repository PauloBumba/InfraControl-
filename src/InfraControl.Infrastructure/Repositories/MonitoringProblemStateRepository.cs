using InfraControl.Application.Monitoring.Events.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// ADR-068: estados de problema lógico — lookup por (Source,
/// CorrelationKey) com tracking (transições da máquina de estados) e por
/// AlertId (caminho do OK, que já localizou o alert do ciclo).
/// </summary>
public sealed class MonitoringProblemStateRepository(AppDbContext dbContext)
    : IMonitoringProblemStateRepository
{
    public Task<MonitoringProblemState?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.MonitoringProblemStates
            .FirstOrDefaultAsync(state => state.Id == id, cancellationToken);

    public async Task AddAsync(
        MonitoringProblemState entity, CancellationToken cancellationToken = default)
        => await dbContext.MonitoringProblemStates.AddAsync(entity, cancellationToken);

    public void Remove(MonitoringProblemState entity)
        => dbContext.MonitoringProblemStates.Remove(entity);

    public Task<MonitoringProblemState?> GetBySourceAndCorrelationKeyAsync(
        MonitoringSource source,
        string correlationKey,
        CancellationToken cancellationToken = default)
        => dbContext.MonitoringProblemStates
            .FirstOrDefaultAsync(state =>
                state.Source == source
                && state.CorrelationKey == correlationKey,
                cancellationToken);

    public Task<MonitoringProblemState?> GetByAlertIdAsync(
        Guid alertId,
        CancellationToken cancellationToken = default)
        => dbContext.MonitoringProblemStates
            .FirstOrDefaultAsync(state => state.AlertId == alertId, cancellationToken);

    public Task<MonitoringProblemState?> GetLatestByDeviceAsync(
        MonitoringSource source,
        Guid deviceId,
        CancellationToken cancellationToken = default)
        => dbContext.MonitoringProblemStates
            .Where(state => state.Source == source && state.DeviceId == deviceId)
            .OrderByDescending(state => state.UpdateDateTime)
            .ThenByDescending(state => state.OpenedAt)
            .FirstOrDefaultAsync(cancellationToken);
}

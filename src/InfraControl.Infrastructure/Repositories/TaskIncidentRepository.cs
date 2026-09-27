using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F7-T004: incidentes da task com group join por página.</summary>
public sealed class TaskIncidentRepository(AppDbContext dbContext)
    : ITaskIncidentRepository
{
    public Task<TaskIncident?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        // O Id do par (TaskId, IncidentId) é unmapped (padrão IncidentAlert).
        => throw new NotSupportedException(
            "TaskIncident usa FindAsync(taskId, incidentId) — a chave é o par composto.");

    public async Task AddAsync(
        TaskIncident entity, CancellationToken cancellationToken = default)
        => await dbContext.TaskIncidents.AddAsync(entity, cancellationToken);

    public void Remove(TaskIncident entity)
        => dbContext.TaskIncidents.Remove(entity);

    public Task<bool> ExistsAsync(
        Guid taskId, Guid incidentId, CancellationToken cancellationToken = default)
        => dbContext.TaskIncidents.AnyAsync(
            link => link.TaskId == taskId && link.IncidentId == incidentId,
            cancellationToken);

    public Task<TaskIncident?> FindAsync(
        Guid taskId, Guid incidentId, CancellationToken cancellationToken = default)
        => dbContext.TaskIncidents.FirstOrDefaultAsync(
            link => link.TaskId == taskId && link.IncidentId == incidentId,
            cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetIncidentIdsAsync(
        Guid taskId, CancellationToken cancellationToken = default)
        => await dbContext.TaskIncidents.AsNoTracking()
            .Where(link => link.TaskId == taskId)
            .Select(link => link.IncidentId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>>
        GetIncidentIdsByTaskIdsAsync(
            IReadOnlyCollection<Guid> taskIds,
            CancellationToken cancellationToken = default)
    {
        var pairs = await dbContext.TaskIncidents.AsNoTracking()
            .Where(link => taskIds.Contains(link.TaskId))
            .Select(link => new { link.TaskId, link.IncidentId })
            .ToListAsync(cancellationToken);

        return pairs
            .GroupBy(pair => pair.TaskId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<Guid>)group
                    .Select(pair => pair.IncidentId).ToArray());
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

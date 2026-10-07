using InfraControl.Application.Tasks.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F23-T004/P4d: fases da task em ordem de posição.</summary>
public sealed class TaskPhaseRepository(AppDbContext dbContext)
    : ITaskPhaseRepository
{
    public async Task<TaskPhase?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => await dbContext.TaskPhases
            .FirstOrDefaultAsync(phase => phase.Id == id, cancellationToken);

    public async Task AddAsync(
        TaskPhase entity, CancellationToken cancellationToken = default)
        => await dbContext.TaskPhases.AddAsync(entity, cancellationToken);

    public void Remove(TaskPhase entity)
        => dbContext.TaskPhases.Remove(entity);

    public async Task<IReadOnlyCollection<TaskPhase>> GetByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default)
        => await dbContext.TaskPhases
            .Where(phase => phase.TaskId == taskId)
            .OrderBy(phase => phase.Position)
            .ThenBy(phase => phase.Id)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

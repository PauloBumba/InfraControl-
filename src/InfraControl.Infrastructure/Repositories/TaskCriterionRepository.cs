using InfraControl.Application.Tasks.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F23-T003: critérios de aceite da task em ordem de criação.</summary>
public sealed class TaskCriterionRepository(AppDbContext dbContext)
    : ITaskCriterionRepository
{
    public async Task<TaskCriterion?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => await dbContext.TaskCriteria
            .FirstOrDefaultAsync(criterion => criterion.Id == id, cancellationToken);

    public async Task AddAsync(
        TaskCriterion entity, CancellationToken cancellationToken = default)
        => await dbContext.TaskCriteria.AddAsync(entity, cancellationToken);

    public void Remove(TaskCriterion entity)
        => dbContext.TaskCriteria.Remove(entity);

    public async Task<IReadOnlyCollection<TaskCriterion>> GetByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default)
        => await dbContext.TaskCriteria
            .AsNoTracking()
            .Where(criterion => criterion.TaskId == taskId)
            .OrderBy(criterion => criterion.CreateDateTime)
            .ThenBy(criterion => criterion.Id)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

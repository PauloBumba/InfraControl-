using InfraControl.Application.Tasks.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F23-T004/P4c: evidências da task em ordem de upload.</summary>
public sealed class TaskEvidenceRepository(AppDbContext dbContext)
    : ITaskEvidenceRepository
{
    public async Task<TaskEvidence?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => await dbContext.TaskEvidences
            .FirstOrDefaultAsync(evidence => evidence.Id == id, cancellationToken);

    public async Task AddAsync(
        TaskEvidence entity, CancellationToken cancellationToken = default)
        => await dbContext.TaskEvidences.AddAsync(entity, cancellationToken);

    public void Remove(TaskEvidence entity)
        => dbContext.TaskEvidences.Remove(entity);

    public async Task<IReadOnlyCollection<TaskEvidence>> GetByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default)
        => await dbContext.TaskEvidences
            .AsNoTracking()
            .Where(evidence => evidence.TaskId == taskId)
            .OrderBy(evidence => evidence.CreateDateTime)
            .ThenBy(evidence => evidence.Id)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

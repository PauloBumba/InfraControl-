using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F23-T005/P5b: PDIs da task com group join por página.</summary>
public sealed class TaskPdiRepository(AppDbContext dbContext)
    : ITaskPdiRepository
{
    public Task<TaskPdi?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        // O Id do par (TaskId, UserId) é unmapped (padrão TaskIncident).
        => throw new NotSupportedException(
            "TaskPdi usa FindAsync(taskId, userId) — a chave é o par composto.");

    public async Task AddAsync(
        TaskPdi entity, CancellationToken cancellationToken = default)
        => await dbContext.TaskPdis.AddAsync(entity, cancellationToken);

    public void Remove(TaskPdi entity)
        => dbContext.TaskPdis.Remove(entity);

    public Task<bool> ExistsAsync(
        Guid taskId, Guid userId, CancellationToken cancellationToken = default)
        => dbContext.TaskPdis.AnyAsync(
            link => link.TaskId == taskId && link.UserId == userId,
            cancellationToken);

    public Task<TaskPdi?> FindAsync(
        Guid taskId, Guid userId, CancellationToken cancellationToken = default)
        => dbContext.TaskPdis.FirstOrDefaultAsync(
            link => link.TaskId == taskId && link.UserId == userId,
            cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetUserIdsAsync(
        Guid taskId, CancellationToken cancellationToken = default)
        => await dbContext.TaskPdis.AsNoTracking()
            .Where(link => link.TaskId == taskId)
            .Select(link => link.UserId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>>
        GetUserIdsByTaskIdsAsync(
            IReadOnlyCollection<Guid> taskIds,
            CancellationToken cancellationToken = default)
    {
        var pairs = await dbContext.TaskPdis.AsNoTracking()
            .Where(link => taskIds.Contains(link.TaskId))
            .Select(link => new { link.TaskId, link.UserId })
            .ToListAsync(cancellationToken);

        return pairs
            .GroupBy(pair => pair.TaskId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<Guid>)group
                    .Select(pair => pair.UserId).ToArray());
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

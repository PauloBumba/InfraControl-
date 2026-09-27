using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F7-T002: assignees (N:N Task ↔ User) com group join por página.</summary>
public sealed class TaskAssigneeRepository(AppDbContext dbContext)
    : ITaskAssigneeRepository
{
    public Task<TaskAssignee?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        // O Id do par (TaskId, UserId) é unmapped (padrão IncidentAlert) —
        // o acesso correto é FindAsync(taskId, userId).
        => throw new NotSupportedException(
            "TaskAssignee usa FindAsync(taskId, userId) — a chave é o par composto.");

    public async Task AddAsync(
        TaskAssignee entity, CancellationToken cancellationToken = default)
        => await dbContext.TaskAssignees.AddAsync(entity, cancellationToken);

    public void Remove(TaskAssignee entity)
        => dbContext.TaskAssignees.Remove(entity);

    public Task<bool> ExistsAsync(
        Guid taskId, Guid userId, CancellationToken cancellationToken = default)
        => dbContext.TaskAssignees.AnyAsync(
            assignee => assignee.TaskId == taskId && assignee.UserId == userId,
            cancellationToken);

    public Task<bool> UserExistsAsync(
        Guid userId, CancellationToken cancellationToken = default)
        => dbContext.DomainUsers.AnyAsync(user => user.Id == userId, cancellationToken);

    public Task<TaskAssignee?> FindAsync(
        Guid taskId, Guid userId, CancellationToken cancellationToken = default)
        => dbContext.TaskAssignees.FirstOrDefaultAsync(
            assignee => assignee.TaskId == taskId && assignee.UserId == userId,
            cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetUserIdsAsync(
        Guid taskId, CancellationToken cancellationToken = default)
        => await dbContext.TaskAssignees.AsNoTracking()
            .Where(assignee => assignee.TaskId == taskId)
            .Select(assignee => assignee.UserId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>>
        GetUserIdsByTaskIdsAsync(
            IReadOnlyCollection<Guid> taskIds,
            CancellationToken cancellationToken = default)
    {
        var pairs = await dbContext.TaskAssignees.AsNoTracking()
            .Where(assignee => taskIds.Contains(assignee.TaskId))
            .Select(assignee => new { assignee.TaskId, assignee.UserId })
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

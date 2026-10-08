using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F7-T001: tasks com busca paginada por termo/tipo/estado.</summary>
public sealed class TaskItemRepository(AppDbContext dbContext)
    : ITaskItemRepository
{
    public Task<TaskItem?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.Tasks
            .FirstOrDefaultAsync(task => task.Id == id, cancellationToken);

    public async Task AddAsync(
        TaskItem entity, CancellationToken cancellationToken = default)
        => await dbContext.Tasks.AddAsync(entity, cancellationToken);

    public void Remove(TaskItem entity) => dbContext.Tasks.Remove(entity);

    public async Task<PagedResult<TaskItem>> SearchAsync(
        string? term,
        TaskKind? kind,
        TaskState? state,
        int page,
        int pageSize,
        Guid? parentId = null,
        bool rootOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Tasks.AsNoTracking()
            .Where(task =>
                (kind == null || task.Kind == kind)
                && (state == null || task.State == state)
                // F23-T004/P4a — hierarquia: filhos de X / só raízes.
                && (parentId == null || task.ParentTaskId == parentId)
                && (!rootOnly || task.ParentTaskId == null)
                && (term == null
                    || task.Title.Contains(term)
                    || (task.Description != null && task.Description.Contains(term))))
            .OrderByDescending(task => task.CreateDateTime);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var total = await query.CountAsync(cancellationToken);

        return new PagedResult<TaskItem>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyDictionary<Guid, TaskSubtaskStats>> GetSubtaskStatsAsync(
        IReadOnlyCollection<Guid> parentIds,
        CancellationToken cancellationToken = default)
    {
        if (parentIds.Count == 0)
            return new Dictionary<Guid, TaskSubtaskStats>();

        var groups = await dbContext.Tasks.AsNoTracking()
            .Where(task => task.ParentTaskId != null
                && parentIds.Contains(task.ParentTaskId.Value))
            .GroupBy(task => task.ParentTaskId!.Value)
            .Select(group => new
            {
                ParentId = group.Key,
                Total = group.Count(),
                NonCancelled = group.Count(task => task.State != TaskState.Cancelled),
                Done = group.Count(task =>
                    task.State == TaskState.Resolved || task.State == TaskState.Closed)
            })
            .ToListAsync(cancellationToken);

        return groups.ToDictionary(
            group => group.ParentId,
            group => new TaskSubtaskStats(
                group.Total, group.NonCancelled, group.Done));
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetTitlesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        return await dbContext.Tasks.AsNoTracking()
            .Where(task => ids.Contains(task.Id))
            .ToDictionaryAsync(task => task.Id, task => task.Title, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

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
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Tasks.AsNoTracking()
            .Where(task =>
                (kind == null || task.Kind == kind)
                && (state == null || task.State == state)
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

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

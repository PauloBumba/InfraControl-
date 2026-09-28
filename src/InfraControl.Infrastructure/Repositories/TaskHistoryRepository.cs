using InfraControl.Application.Tasks.History.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F7-T006: histórico da task em ordem cronológica, com ator.</summary>
public sealed class TaskHistoryRepository(AppDbContext dbContext)
    : ITaskHistoryRepository
{
    public async Task<TaskHistory?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => await dbContext.TaskHistories
            .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

    public async Task AddAsync(
        TaskHistory entity, CancellationToken cancellationToken = default)
        => await dbContext.TaskHistories.AddAsync(entity, cancellationToken);

    public void Remove(TaskHistory entity)
        => dbContext.TaskHistories.Remove(entity);

    public async Task<IReadOnlyCollection<TaskHistoryWithActor>> GetByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default)
    {
        var rows = await (
            from entry in dbContext.TaskHistories.AsNoTracking()
            where entry.TaskId == taskId
            orderby entry.CreateDateTime, entry.Id
            join actor in dbContext.DomainUsers.AsNoTracking()
                on entry.ActorId equals actor.Id into actorGroup
            from actor in actorGroup.DefaultIfEmpty()
            select new
            {
                Entry = entry,
                ActorName = actor != null ? actor.DisplayName : null
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new TaskHistoryWithActor(row.Entry, row.ActorName))
            .ToArray();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

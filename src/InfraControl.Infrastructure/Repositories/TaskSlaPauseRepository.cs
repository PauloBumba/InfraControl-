using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Sla.TaskSlas.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class TaskSlaPauseRepository(AppDbContext dbContext) : ITaskSlaPauseRepository
{
    public async Task AddAsync(
        TaskSlaPause pause, CancellationToken cancellationToken = default)
    {
        await dbContext.TaskSlasPauses.AddAsync(pause, cancellationToken);
    }

    public Task<TaskSlaPause?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.TaskSlasPauses
            .SingleOrDefaultAsync(pause => pause.Id == id, cancellationToken);

    public void Remove(TaskSlaPause pause)
        => dbContext.TaskSlasPauses.Remove(pause);

    public Task<TaskSlaPause?> GetOpenByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default)
        => dbContext.TaskSlasPauses
            .SingleOrDefaultAsync(
                pause => pause.TaskId == taskId && pause.EndAt == null,
                cancellationToken);

    public async Task<IReadOnlyList<TaskSlaPause>> GetByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default)
        => await dbContext.TaskSlasPauses
            .AsNoTracking()
            .Where(pause => pause.TaskId == taskId)
            .OrderBy(pause => pause.StartAt)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

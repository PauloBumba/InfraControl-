using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Sla.TaskSlas.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class TaskSlaRepository(AppDbContext dbContext) : ITaskSlaRepository
{
    public async Task AddAsync(TaskSla sla, CancellationToken cancellationToken = default)
    {
        await dbContext.TaskSlas.AddAsync(sla, cancellationToken);
    }

    public Task<TaskSla?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => GetByTaskIdAsync(id, cancellationToken);

    // Sem AsNoTracking: o Resume (F8-T004) altera os *DueAt do TaskSla e o
    // SaveChanges persiste; leituras puras não sofrem efeito colateral.
    public Task<TaskSla?> GetByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default)
        => dbContext.TaskSlas
            .SingleOrDefaultAsync(sla => sla.TaskId == taskId, cancellationToken);

    public void Remove(TaskSla sla)
        => dbContext.TaskSlas.Remove(sla);

    public Task<bool> HasPolicySlasAsync(
        Guid slaPolicyId, CancellationToken cancellationToken = default)
        => dbContext.TaskSlas.AnyAsync(
            sla => sla.SlaPolicyId == slaPolicyId, cancellationToken);

    public async Task<int> CountOpenBreachedAsync(
        CancellationToken cancellationToken = default)
    {
        var openStates = new[]
        {
            TaskState.Open, TaskState.InProgress,
            TaskState.Waiting, TaskState.Blocked,
        };
        return await dbContext.TaskSlas
            .AsNoTracking()
            .CountAsync(sla =>
                (sla.ResponseBreachedAt != null || sla.ResolutionBreachedAt != null)
                && dbContext.Tasks.Any(task =>
                    task.Id == sla.TaskId && openStates.Contains(task.State)),
                cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

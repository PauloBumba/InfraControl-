using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Sla.TaskSlas.Ports;
using InfraControl.Domain.Entities;
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

    public Task<TaskSla?> GetByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default)
        => dbContext.TaskSlas
            .AsNoTracking()
            .SingleOrDefaultAsync(sla => sla.TaskId == taskId, cancellationToken);

    public void Remove(TaskSla sla)
        => dbContext.TaskSlas.Remove(sla);

    public Task<bool> HasPolicySlasAsync(
        Guid slaPolicyId, CancellationToken cancellationToken = default)
        => dbContext.TaskSlas.AnyAsync(
            sla => sla.SlaPolicyId == slaPolicyId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

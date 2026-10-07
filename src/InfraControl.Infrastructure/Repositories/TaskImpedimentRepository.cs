using InfraControl.Application.Tasks.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F23-T004/P4b: impedimentos da task em ordem de criação.</summary>
public sealed class TaskImpedimentRepository(AppDbContext dbContext)
    : ITaskImpedimentRepository
{
    public async Task<TaskImpediment?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => await dbContext.TaskImpediments
            .FirstOrDefaultAsync(impediment => impediment.Id == id, cancellationToken);

    public async Task AddAsync(
        TaskImpediment entity, CancellationToken cancellationToken = default)
        => await dbContext.TaskImpediments.AddAsync(entity, cancellationToken);

    public void Remove(TaskImpediment entity)
        => dbContext.TaskImpediments.Remove(entity);

    public async Task<IReadOnlyCollection<TaskImpediment>> GetByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default)
        => await dbContext.TaskImpediments
            .AsNoTracking()
            .Where(impediment => impediment.TaskId == taskId)
            .OrderBy(impediment => impediment.CreateDateTime)
            .ThenBy(impediment => impediment.Id)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

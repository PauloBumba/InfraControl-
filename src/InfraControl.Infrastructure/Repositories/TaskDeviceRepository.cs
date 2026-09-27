using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F7-T003: devices da task com group join por página.</summary>
public sealed class TaskDeviceRepository(AppDbContext dbContext)
    : ITaskDeviceRepository
{
    public Task<TaskDevice?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        // O Id do par (TaskId, DeviceId) é unmapped (padrão IncidentAlert).
        => throw new NotSupportedException(
            "TaskDevice usa FindAsync(taskId, deviceId) — a chave é o par composto.");

    public async Task AddAsync(
        TaskDevice entity, CancellationToken cancellationToken = default)
        => await dbContext.TaskDevices.AddAsync(entity, cancellationToken);

    public void Remove(TaskDevice entity)
        => dbContext.TaskDevices.Remove(entity);

    public Task<bool> ExistsAsync(
        Guid taskId, Guid deviceId, CancellationToken cancellationToken = default)
        => dbContext.TaskDevices.AnyAsync(
            link => link.TaskId == taskId && link.DeviceId == deviceId,
            cancellationToken);

    public Task<TaskDevice?> FindAsync(
        Guid taskId, Guid deviceId, CancellationToken cancellationToken = default)
        => dbContext.TaskDevices.FirstOrDefaultAsync(
            link => link.TaskId == taskId && link.DeviceId == deviceId,
            cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetDeviceIdsAsync(
        Guid taskId, CancellationToken cancellationToken = default)
        => await dbContext.TaskDevices.AsNoTracking()
            .Where(link => link.TaskId == taskId)
            .Select(link => link.DeviceId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>>
        GetDeviceIdsByTaskIdsAsync(
            IReadOnlyCollection<Guid> taskIds,
            CancellationToken cancellationToken = default)
    {
        var pairs = await dbContext.TaskDevices.AsNoTracking()
            .Where(link => taskIds.Contains(link.TaskId))
            .Select(link => new { link.TaskId, link.DeviceId })
            .ToListAsync(cancellationToken);

        return pairs
            .GroupBy(pair => pair.TaskId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<Guid>)group
                    .Select(pair => pair.DeviceId).ToArray());
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

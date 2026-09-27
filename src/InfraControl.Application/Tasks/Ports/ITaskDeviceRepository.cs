using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Tasks.Ports;

/// <summary>F7-T003: devices vinculados à Task (N:N com Device).</summary>
public interface ITaskDeviceRepository : IRepository<TaskDevice>
{
    Task<bool> ExistsAsync(
        Guid taskId, Guid deviceId, CancellationToken cancellationToken = default);

    Task<TaskDevice?> FindAsync(
        Guid taskId, Guid deviceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> GetDeviceIdsAsync(
        Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>Group join por página — evita N+1 no Search (F7-T003).</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetDeviceIdsByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

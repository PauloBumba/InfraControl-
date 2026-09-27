using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Tasks.Ports;

/// <summary>F7-T002: assignees da Task (N:N com User).</summary>
public interface ITaskAssigneeRepository : IRepository<TaskAssignee>
{
    Task<bool> ExistsAsync(
        Guid taskId, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> UserExistsAsync(
        Guid userId, CancellationToken cancellationToken = default);

    Task<TaskAssignee?> FindAsync(
        Guid taskId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> GetUserIdsAsync(
        Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>Group join por página — evita N+1 no Search (F7-T002).</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetUserIdsByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

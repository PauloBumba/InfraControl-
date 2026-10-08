using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Tasks.Ports;

/// <summary>
/// F23-T005/P5b: PDI vinculado à Task (N:N por dono do plano — 1 PDI
/// vigente por usuário, docs/29 §8).
/// </summary>
public interface ITaskPdiRepository : IRepository<TaskPdi>
{
    Task<bool> ExistsAsync(
        Guid taskId, Guid userId, CancellationToken cancellationToken = default);

    Task<TaskPdi?> FindAsync(
        Guid taskId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> GetUserIdsAsync(
        Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>Group join por página — evita N+1 no Search (P5b).</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetUserIdsByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

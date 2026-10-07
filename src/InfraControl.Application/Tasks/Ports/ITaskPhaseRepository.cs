using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Tasks.Ports;

/// <summary>Fases (etapas) da Task (F23-T004/P4d — §7).</summary>
public interface ITaskPhaseRepository : IRepository<TaskPhase>
{
    /// <summary>Fases da task em ordem de posição (com gaps aceitos).</summary>
    Task<IReadOnlyCollection<TaskPhase>> GetByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

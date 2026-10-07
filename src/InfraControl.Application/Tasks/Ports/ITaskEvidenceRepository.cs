using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Tasks.Ports;

/// <summary>Evidências anexadas à Task (F23-T004/P4c — §11, ADR-033).</summary>
public interface ITaskEvidenceRepository : IRepository<TaskEvidence>
{
    Task<IReadOnlyCollection<TaskEvidence>> GetByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

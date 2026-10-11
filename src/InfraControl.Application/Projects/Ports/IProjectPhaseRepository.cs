using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Projects.Ports;

/// <summary>
/// Porta do repositório da entidade ProjectPhase (ADR-043 · docs/38 §3.2).
/// </summary>
public interface IProjectPhaseRepository : IRepository<ProjectPhase>
{
    Task<IReadOnlyCollection<ProjectPhase>> GetByProjectIdAsync(
        Guid projectId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

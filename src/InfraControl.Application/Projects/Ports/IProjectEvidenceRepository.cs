using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Projects.Ports;

public interface IProjectEvidenceRepository : IRepository<ProjectEvidence>
{
    Task<IReadOnlyList<ProjectEvidence>> ListByProjectIdAsync(
        Guid projectId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Projects.Ports;

public interface IProjectProgressRepository : IRepository<ProjectProgress>
{
    Task<IReadOnlyList<ProjectProgress>> ListByProjectIdAsync(
        Guid projectId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

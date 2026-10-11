using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Projects.Ports;

public interface IProjectHistoryRepository : IRepository<ProjectHistoryEntry>
{
    Task<IReadOnlyList<ProjectHistoryEntry>> ListByProjectIdAsync(
        Guid projectId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

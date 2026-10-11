using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Projects.Ports;

public interface IProjectImpedimentRepository : IRepository<ProjectImpediment>
{
    Task<IReadOnlyList<ProjectImpediment>> ListByProjectIdAsync(
        Guid projectId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

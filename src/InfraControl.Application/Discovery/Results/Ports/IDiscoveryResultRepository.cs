using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Discovery.Results.Ports;

public interface IDiscoveryResultRepository : IRepository<DiscoveryResult>
{
    Task<PagedResult<DiscoveryResult>> SearchByRunAsync(
        Guid discoveryRunId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

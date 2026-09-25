using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Discovery.Runs.Ports;

public interface IDiscoveryRunRepository : IRepository<DiscoveryRun>
{
    Task<PagedResult<DiscoveryRun>> SearchAsync(
        Guid? deviceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Inventory.Locations.Ports;

public interface ILocationRepository : IRepository<Location>
{
    /// <summary>Busca por nome sem distinção de maiúsculas/minúsculas.</summary>
    Task<Location?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<PagedResult<Location>> SearchAsync(
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

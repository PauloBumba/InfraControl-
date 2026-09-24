using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Inventory.Racks.Ports;

public interface IRackRepository : IRepository<Rack>
{
    /// <summary>Busca rack por nome DENTRO de um local, sem distinção de maiúsculas/minúsculas.</summary>
    Task<Rack?> GetByNameAsync(Guid locationId, string name, CancellationToken cancellationToken = default);

    Task<PagedResult<Rack>> SearchAsync(
        Guid? locationId,
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

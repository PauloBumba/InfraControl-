using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Network.Vlans.Ports;

public interface IVlanRepository : IRepository<Vlan>
{
    /// <summary>Busca por número 802.1Q (único global).</summary>
    Task<Vlan?> GetByNumberAsync(int vlanNumber, CancellationToken cancellationToken = default);

    /// <summary>Busca por nome, sem distinção de maiúsculas.</summary>
    Task<Vlan?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<PagedResult<Vlan>> SearchAsync(
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

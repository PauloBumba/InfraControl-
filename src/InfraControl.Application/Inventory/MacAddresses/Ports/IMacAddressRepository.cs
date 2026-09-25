using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Inventory.MacAddresses.Ports;

public interface IMacAddressRepository : IRepository<MacAddress>
{
    /// <summary>Busca MAC por valor normalizado — unicidade GLOBAL (case-insensitive).</summary>
    Task<MacAddress?> GetByMacAsync(string mac, CancellationToken cancellationToken = default);

    Task<PagedResult<MacAddress>> SearchAsync(
        Guid? interfaceId,
        Guid? deviceId,
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

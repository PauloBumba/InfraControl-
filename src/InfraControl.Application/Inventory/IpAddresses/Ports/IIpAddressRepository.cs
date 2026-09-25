using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Inventory.IpAddresses.Ports;

public interface IIpAddressRepository : IRepository<IpAddress>
{
    /// <summary>Busca IP por valor canônico — unicidade GLOBAL.</summary>
    Task<IpAddress?> GetByIpAsync(string ip, CancellationToken cancellationToken = default);

    Task<PagedResult<IpAddress>> SearchAsync(
        Guid? interfaceId,
        Guid? deviceId,
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

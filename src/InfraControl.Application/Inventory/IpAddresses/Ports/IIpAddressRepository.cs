using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Inventory.IpAddresses.Ports;

/// <summary>F29-4: IP de interface com o device dono (para conflitos).</summary>
public sealed record IpInterfaceClaim(string Ip, Guid DeviceId);

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

    /// <summary>
    /// F29-4: todos os IPs de interface com o device dono (join
    /// IpAddress→NetworkInterface→DeviceId) — base da detecção de conflito.
    /// </summary>
    Task<IReadOnlyList<IpInterfaceClaim>> ListInterfaceClaimsAsync(
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

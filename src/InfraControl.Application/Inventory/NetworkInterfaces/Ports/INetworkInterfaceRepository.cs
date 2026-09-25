using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Inventory.NetworkInterfaces.Ports;

public interface INetworkInterfaceRepository : IRepository<NetworkInterface>
{
    /// <summary>Busca interface por nome DENTRO de um device, sem distinção de maiúsculas/minúsculas.</summary>
    Task<NetworkInterface?> GetByNameAsync(Guid deviceId, string name, CancellationToken cancellationToken = default);

    /// <summary>Usado pelo DeleteNetworkInterface para bloquear exclusão com MACs (F2-T005 → 409).</summary>
    Task<bool> HasMacsAsync(Guid interfaceId, CancellationToken cancellationToken = default);

    /// <summary>Usado pelo DeleteNetworkInterface para bloquear exclusão com IPs (F2-T006 → 409).</summary>
    Task<bool> HasIpsAsync(Guid interfaceId, CancellationToken cancellationToken = default);

    Task<PagedResult<NetworkInterface>> SearchAsync(
        Guid? deviceId,
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

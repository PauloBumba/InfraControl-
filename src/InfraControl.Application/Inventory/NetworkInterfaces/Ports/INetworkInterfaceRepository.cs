using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Inventory.NetworkInterfaces.Ports;

public interface INetworkInterfaceRepository : IRepository<NetworkInterface>
{
    /// <summary>Busca interface por nome DENTRO de um device, sem distinção de maiúsculas/minúsculas.</summary>
    Task<NetworkInterface?> GetByNameAsync(Guid deviceId, string name, CancellationToken cancellationToken = default);

    Task<PagedResult<NetworkInterface>> SearchAsync(
        Guid? deviceId,
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

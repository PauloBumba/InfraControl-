using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Inventory.Devices.Ports;

public interface IDeviceRepository : IRepository<Device>
{
    /// <summary>Busca device por nome global, sem distinção de maiúsculas/minúsculas.</summary>
    Task<Device?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<PagedResult<Device>> SearchAsync(
        Guid? locationId,
        Guid? rackId,
        DeviceType? type,
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Usado pelo DeleteRack para bloquear exclusão com devices (F2-T003 → 409).</summary>
    Task<bool> HasByRackAsync(Guid rackId, CancellationToken cancellationToken = default);

    /// <summary>Usado pelo DeleteDevice para bloquear exclusão com interfaces (F2-T004 → 409).</summary>
    Task<bool> HasInterfacesAsync(Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>Usado pelo DeleteDevice para bloquear exclusão com portas de switch (F3-T002 → 409).</summary>
    Task<bool> HasPortsAsync(Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>Todos os devices ordenados por nome (F5-T003: status de ping em massa).</summary>
    Task<IReadOnlyList<Device>> GetAllOrderedByNameAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

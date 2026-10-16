using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Inventory.Devices.Ports;

public interface IDeviceRepository : IRepository<Device>
{
    /// <summary>Busca device por nome global, sem distinção de maiúsculas/minúsculas.</summary>
    Task<Device?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Busca device por IP exato, sem distinção de maiúsculas (F4-T010).</summary>
    Task<Device?> GetByIpAsync(string ip, CancellationToken cancellationToken = default);

    /// <summary>Busca device por MAC sem separador/caixa (F4-T010).</summary>
    Task<Device?> GetByMacAsync(string mac, CancellationToken cancellationToken = default);

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

    /// <summary>F7-T003: bloqueio de exclusão — tasks vinculadas ao device.</summary>
    Task<bool> HasTasksAsync(Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>F30-T005c: bloqueio de exclusão de NVR — mapeamentos câmera-NVR (409).</summary>
    Task<bool> HasCameraNvrMappingsAsync(Guid nvrId, CancellationToken cancellationToken = default);

    /// <summary>Todos os devices ordenados por nome (F5-T003: status de ping em massa).</summary>
    Task<IReadOnlyList<Device>> GetAllOrderedByNameAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

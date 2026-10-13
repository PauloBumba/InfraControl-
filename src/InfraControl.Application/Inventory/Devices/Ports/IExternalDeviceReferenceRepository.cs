using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Inventory.Devices.Ports;

public interface IExternalDeviceReferenceRepository : IRepository<ExternalDeviceReference>
{
    /// <summary>
    /// Device dono do identificador externo estável da fonte (null = sem
    /// referência) — ponte "MAC/Device ID → Device" das integrações (F20).
    /// A resolução do Zabbix por IP/hostname continua no fluxo de ingestão
    /// (regra 6 da revisão F19/F20/F21); este lookup só é usado quando a
    /// fonte manda um identificador estável.
    /// </summary>
    Task<Guid?> GetDeviceIdAsync(
        ExternalDeviceSource source,
        string externalId,
        CancellationToken cancellationToken = default);

    /// <summary>F26 Fase 7.2: todas as referências da fonte (correlação).</summary>
    Task<IReadOnlyList<ExternalDeviceReference>> ListBySourceAsync(
        ExternalDeviceSource source, CancellationToken cancellationToken = default);

    /// <summary>F26 Fase 7.2/7.3: referência específica (exclusão de vínculo).</summary>
    Task<ExternalDeviceReference?> GetBySourceAndExternalIdAsync(
        ExternalDeviceSource source, string externalId,
        CancellationToken cancellationToken = default);

    /// <summary>F1-b: todas as referências de um device (status consolidado).</summary>
    Task<IReadOnlyList<ExternalDeviceReference>> ListByDeviceIdAsync(
        Guid deviceId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

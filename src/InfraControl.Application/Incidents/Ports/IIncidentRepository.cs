using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Incidents.Ports;

public interface IIncidentRepository : IRepository<Incident>
{
    /// <summary>Incident Open do device (auto-abertura anti-storm) ou null.</summary>
    Task<Incident?> GetOpenByDeviceAsync(
        Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>Alert já vinculado ao incidente?</summary>
    Task<bool> IsAlertLinkedAsync(
        Guid incidentId, Guid alertId, CancellationToken cancellationToken = default);

    Task AddLinkAsync(IncidentAlert link, CancellationToken cancellationToken = default);

    /// <summary>Alerts vinculados ao incidente (join).</summary>
    Task<IReadOnlyList<Alert>> GetAlertsAsync(
        Guid incidentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ids de Incident Open vinculados ao alerta (F6-T006 — fechamento
    /// por recovery).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetOpenIdsLinkedToAlertAsync(
        Guid alertId, CancellationToken cancellationToken = default);

    /// <summary>Paginado, mais recente primeiro — filtros device/status.</summary>
    Task<PagedResult<IncidentWithAlertCount>> SearchAsync(
        Guid? deviceId,
        IncidentStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Incident do search com a contagem de alerts vinculados.</summary>
public sealed record IncidentWithAlertCount(Incident Incident, int AlertCount);

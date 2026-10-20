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

    /// <summary>
    /// ADR-027: incident não-Resolved do device (Status != Resolved) — usado
    /// pelo avaliador (F6-T003/F6-T006) para o ciclo com incidente em
    /// Investigating/Identified/Monitoring: sem isto cada falha reabria um
    /// novo incidente e o recovery não fechava o existente.
    /// </summary>
    Task<Incident?> GetUnresolvedByDeviceAsync(
        Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>Alert já vinculado ao incidente?</summary>
    Task<bool> IsAlertLinkedAsync(
        Guid incidentId, Guid alertId, CancellationToken cancellationToken = default);

    Task AddLinkAsync(IncidentAlert link, CancellationToken cancellationToken = default);

    /// <summary>Alerts vinculados ao incidente (join).</summary>
    Task<IReadOnlyList<Alert>> GetAlertsAsync(
        Guid incidentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ids de Incident não-Resolved (Status != Resolved) vinculados ao
    /// alerta (F6-T006/ADR-027 — fechamento por recovery em qualquer estado
    /// não-Resolved, não só Open).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetUnresolvedIdsLinkedToAlertAsync(
        Guid alertId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ids de Incident vinculados ao alerta em qualquer status (contrato de
    /// resposta do webhook Zabbix: devolve o incidente mesmo já resolvido).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetLinkedIdsToAlertAsync(
        Guid alertId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Paginado, mais recente primeiro — filtros device/status.
    /// M7 (caça-bugs): <paramref name="statuses"/> é um IN de estados
    /// (ex.: todos exceto Resolved para "abertos"); null = sem filtro.
    /// </summary>
    Task<PagedResult<IncidentWithAlertCount>> SearchAsync(
        Guid? deviceId,
        IReadOnlyCollection<IncidentStatus>? statuses,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Incident do search com a contagem de alerts vinculados.</summary>
public sealed record IncidentWithAlertCount(Incident Incident, int AlertCount);

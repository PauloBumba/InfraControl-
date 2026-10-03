using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Incidents.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F6-T003: incidentes com join de alerts no search/leitura.</summary>
public sealed class IncidentRepository(AppDbContext dbContext)
    : IIncidentRepository
{
    public Task<Incident?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.Incidents
            .FirstOrDefaultAsync(incident => incident.Id == id, cancellationToken);

    public async Task AddAsync(
        Incident entity, CancellationToken cancellationToken = default)
        => await dbContext.Incidents.AddAsync(entity, cancellationToken);

    public void Remove(Incident entity) => dbContext.Incidents.Remove(entity);

    // Tracking: F6-T005 usa a mesma carga para SetRootCause (causa
    // provável recalculada pelo avaliador).
    public Task<Incident?> GetOpenByDeviceAsync(
        Guid deviceId, CancellationToken cancellationToken = default)
        => dbContext.Incidents
            .FirstOrDefaultAsync(incident =>
                incident.DeviceId == deviceId
                && incident.Status == IncidentStatus.Open,
                cancellationToken);

    // ADR-027: não-Resolved (Open/Investigating/Identified/Monitoring) —
    // usado pelo avaliador para não abrir incidente novo enquanto o atual
    // está em investigação e para fechar o existente no recovery.
    public Task<Incident?> GetUnresolvedByDeviceAsync(
        Guid deviceId, CancellationToken cancellationToken = default)
        => dbContext.Incidents
            .FirstOrDefaultAsync(incident =>
                incident.DeviceId == deviceId
                && incident.Status != IncidentStatus.Resolved,
                cancellationToken);

    public Task<bool> IsAlertLinkedAsync(
        Guid incidentId, Guid alertId, CancellationToken cancellationToken = default)
        => dbContext.IncidentAlerts.AsNoTracking()
            .AnyAsync(link =>
                link.IncidentId == incidentId && link.AlertId == alertId,
                cancellationToken);

    public async Task AddLinkAsync(
        IncidentAlert link, CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.IncidentAlerts.AsNoTracking()
            .AnyAsync(row => row.IncidentId == link.IncidentId
                && row.AlertId == link.AlertId,
                cancellationToken);
        if (!exists)
            await dbContext.IncidentAlerts.AddAsync(link, cancellationToken);
    }

    public async Task<IReadOnlyList<Alert>> GetAlertsAsync(
        Guid incidentId, CancellationToken cancellationToken = default)
        => await dbContext.IncidentAlerts.AsNoTracking()
            .Where(link => link.IncidentId == incidentId)
            .Join(dbContext.Alerts.AsNoTracking(),
                link => link.AlertId,
                alert => alert.Id,
                (_, alert) => alert)
            .OrderByDescending(alert => alert.CreateDateTime)
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<IncidentWithAlertCount>> SearchAsync(
        Guid? deviceId,
        IncidentStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query =
            from incident in dbContext.Incidents.AsNoTracking()
            where (deviceId == null || incident.DeviceId == deviceId)
                && (status == null || incident.Status == status)
            orderby incident.CreateDateTime descending
            select incident;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var ids = items.Select(incident => incident.Id).ToArray();
        var counts = await dbContext.IncidentAlerts.AsNoTracking()
            .Where(link => ids.Contains(link.IncidentId))
            .GroupBy(link => link.IncidentId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Key, row => row.Count, cancellationToken);

        var total = await query.CountAsync(cancellationToken);
        return new PagedResult<IncidentWithAlertCount>(
            items.Select(incident => new IncidentWithAlertCount(
                incident, counts.TryGetValue(incident.Id, out var c) ? c : 0)).ToList(),
            total, page, pageSize);
    }

    public async Task<IReadOnlyList<Guid>> GetUnresolvedIdsLinkedToAlertAsync(
        Guid alertId, CancellationToken cancellationToken = default)
        => await dbContext.IncidentAlerts.AsNoTracking()
            .Where(link => link.AlertId == alertId)
            .Join(dbContext.Incidents.AsNoTracking(),
                link => link.IncidentId,
                incident => incident.Id,
                (link, incident) => incident)
            .Where(incident => incident.Status != IncidentStatus.Resolved)
            .Select(incident => incident.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetLinkedIdsToAlertAsync(
        Guid alertId, CancellationToken cancellationToken = default)
        => await dbContext.IncidentAlerts.AsNoTracking()
            .Where(link => link.AlertId == alertId)
            .Join(dbContext.Incidents.AsNoTracking(),
                link => link.IncidentId,
                incident => incident.Id,
                (link, incident) => incident)
            .OrderByDescending(incident => incident.Status != IncidentStatus.Resolved)
            .ThenByDescending(incident => incident.CreateDateTime)
            .Select(incident => incident.Id)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

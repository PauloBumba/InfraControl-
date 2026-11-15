using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F6-T001: alertas com join do device no search.</summary>
public sealed class AlertRepository(AppDbContext dbContext)
    : IAlertRepository
{
    public Task<Alert?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.Alerts
            .FirstOrDefaultAsync(alert => alert.Id == id, cancellationToken);

    public async Task AddAsync(
        Alert entity, CancellationToken cancellationToken = default)
        => await dbContext.Alerts.AddAsync(entity, cancellationToken);

    public void Remove(Alert entity) => dbContext.Alerts.Remove(entity);

    // Tracking: o avaliador chama Resolve()/Suppress()/Reopen() no
    // retorno. F6-T004: ativo = Open OU Suppressed (não-Resolved).
    public Task<Alert?> GetActiveAsync(
        Guid deviceId,
        CheckKind kind,
        CancellationToken cancellationToken = default)
        => dbContext.Alerts
            .FirstOrDefaultAsync(alert =>
                alert.DeviceId == deviceId
                && alert.Kind == kind
                && alert.Status != AlertStatus.Resolved,
                cancellationToken);

    // ADR-027: alerta externo por (Source, ExternalProblemId), tracking
    // (Resolve no recovery); não-Resolved primeiro.
    public async Task<Alert?> GetByExternalAsync(
        MonitoringSource source,
        string externalProblemId,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Alerts
            .Where(alert =>
                alert.Source == source
                && alert.ExternalProblemId == externalProblemId);

        return await query
            .OrderBy(alert => alert.Status == AlertStatus.Resolved ? 1 : 0)
            .ThenByDescending(alert => alert.CreateDateTime)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<AlertWithDevice>> SearchAsync(
        Guid? deviceId,
        AlertStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Left join: alerta externo sem device mapeado entra sem nome (ADR-027).
        var query =
            from alert in dbContext.Alerts.AsNoTracking()
            join device in dbContext.Devices.AsNoTracking()
                on alert.DeviceId equals device.Id into deviceGroup
            from device in deviceGroup.DefaultIfEmpty()
            where (deviceId == null || alert.DeviceId == deviceId)
                && (status == null || alert.Status == status)
            orderby alert.CreateDateTime descending
            select new AlertWithDevice(alert, device == null ? null : device.Name);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var total = await query.CountAsync(cancellationToken);

        // Responsável do alerta tem prioridade; na falta dele, cai para o
        // responsável do incidente vinculado (comportamento anterior).
        var ownAssignees = items
            .Where(item => item.Alert.AssignedToId is not null)
            .Select(item => item.Alert.AssignedToId!.Value)
            .Distinct()
            .ToArray();
        var ownNames = ownAssignees.Length == 0
            ? new Dictionary<Guid, string>()
            : (await dbContext.DomainUsers.AsNoTracking()
                .Where(user => ownAssignees.Contains(user.Id))
                .Select(user => new { user.Id, user.DisplayName })
                .ToListAsync(cancellationToken))
                .ToDictionary(user => user.Id, user => user.DisplayName);

        var incidentNames = await GetAssignedToNamesAsync(
            items.Where(item => item.Alert.AssignedToId is null)
                .Select(item => item.Alert.Id).ToArray(),
            cancellationToken);

        var linkedIncidents = await GetLinkedIncidentsAsync(
            items.Select(item => item.Alert.Id).ToArray(), cancellationToken);

        var named = items
            .Select(item =>
            {
                var hasIncident = linkedIncidents.TryGetValue(item.Alert.Id, out var linked);
                var withIncident = item with
                {
                    LinkedIncidentId = hasIncident ? linked.Id : (Guid?)null,
                    LinkedIncidentTitle = hasIncident ? linked.Title : null,
                };
                if (item.Alert.AssignedToId is { } ownId
                    && ownNames.TryGetValue(ownId, out var ownName))
                    return withIncident with { AssignedToName = ownName };
                return incidentNames.TryGetValue(item.Alert.Id, out var name)
                    ? withIncident with { AssignedToName = name }
                    : withIncident;
            })
            .ToArray();

        return new PagedResult<AlertWithDevice>(named, total, page, pageSize);
    }

    /// <summary>
    /// Nome do responsável do incidente vinculado a cada alerta da página
    /// (fallback quando o alerta não tem responsável próprio).
    /// Alerta pode ter N incidentes — prefere o não resolvido mais recente.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, string>> GetAssignedToNamesAsync(
        IReadOnlyCollection<Guid> alertIds,
        CancellationToken cancellationToken)
    {
        if (alertIds.Count == 0)
            return new Dictionary<Guid, string>();

        var rows = await (
            from link in dbContext.IncidentAlerts.AsNoTracking()
            join incident in dbContext.Incidents.AsNoTracking()
                on link.IncidentId equals incident.Id
            join user in dbContext.DomainUsers.AsNoTracking()
                on incident.AssignedToId equals user.Id
            where alertIds.Contains(link.AlertId)
            select new
            {
                link.AlertId,
                incident.Status,
                incident.CreateDateTime,
                user.DisplayName,
            }).ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.AlertId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(row => row.Status == IncidentStatus.Resolved ? 0 : 1)
                    .ThenByDescending(row => row.CreateDateTime)
                    .First()
                    .DisplayName);
    }

    /// <summary>
    /// Incidente vinculado a cada alerta (N:N) — prefere o não resolvido
    /// mais recente. Independente de responsável: todo alerta ligado a um
    /// incidente mostra a relação, mesmo sem assignee.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, (Guid Id, string Title)>> GetLinkedIncidentsAsync(
        IReadOnlyCollection<Guid> alertIds,
        CancellationToken cancellationToken)
    {
        if (alertIds.Count == 0)
            return new Dictionary<Guid, (Guid, string)>();

        var rows = await (
            from link in dbContext.IncidentAlerts.AsNoTracking()
            join incident in dbContext.Incidents.AsNoTracking()
                on link.IncidentId equals incident.Id
            where alertIds.Contains(link.AlertId)
            select new
            {
                link.AlertId,
                incident.Id,
                incident.Title,
                incident.Status,
                incident.CreateDateTime,
            }).ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.AlertId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var best = group
                        .OrderByDescending(row => row.Status == IncidentStatus.Resolved ? 0 : 1)
                        .ThenByDescending(row => row.CreateDateTime)
                        .First();
                    return (best.Id, best.Title);
                });
    }

    public async Task<IReadOnlyList<OpenAlertCount>> GetOpenCountsByDeviceAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.Alerts
            .AsNoTracking()
            .Where(alert =>
                alert.Status == AlertStatus.Open && alert.DeviceId != null)
            .GroupBy(alert => new { alert.DeviceId, alert.Severity })
            .Select(group => new
            {
                group.Key.DeviceId,
                group.Key.Severity,
                Count = group.Count(),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new OpenAlertCount(
                row.DeviceId!.Value, row.Severity, row.Count))
            .ToList();
    }

    public Task<bool> UserExistsAsync(
        Guid userId, CancellationToken cancellationToken = default)
        => dbContext.DomainUsers.AnyAsync(user => user.Id == userId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
            return new Dictionary<Guid, string>();

        var users = await dbContext.DomainUsers.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.DisplayName })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(user => user.Id, user => user.DisplayName);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

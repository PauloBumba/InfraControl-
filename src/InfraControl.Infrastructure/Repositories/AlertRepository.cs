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

        return new PagedResult<AlertWithDevice>(items, total, page, pageSize);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

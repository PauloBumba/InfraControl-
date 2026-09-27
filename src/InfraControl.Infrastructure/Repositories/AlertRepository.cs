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

    // Tracking: o avaliador chama Resolve() no retorno (auto-resolve).
    public Task<Alert?> GetOpenAsync(
        Guid deviceId,
        CheckKind kind,
        CancellationToken cancellationToken = default)
        => dbContext.Alerts
            .FirstOrDefaultAsync(alert =>
                alert.DeviceId == deviceId
                && alert.Kind == kind
                && alert.Status == AlertStatus.Open,
                cancellationToken);

    public async Task<PagedResult<AlertWithDevice>> SearchAsync(
        Guid? deviceId,
        AlertStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query =
            from alert in dbContext.Alerts.AsNoTracking()
            join device in dbContext.Devices.AsNoTracking()
                on alert.DeviceId equals device.Id
            where (deviceId == null || alert.DeviceId == deviceId)
                && (status == null || alert.Status == status)
            orderby alert.CreateDateTime descending
            select new AlertWithDevice(alert, device.Name);

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

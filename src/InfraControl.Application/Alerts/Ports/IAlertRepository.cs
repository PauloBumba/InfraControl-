using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Alerts.Ports;

public interface IAlertRepository : IRepository<Alert>
{
    /// <summary>Alerta Open de (device, check) com tracking (para Resolve) ou null.</summary>
    Task<Alert?> GetOpenAsync(
        Guid deviceId,
        CheckKind kind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Paginado com o device resolvido (join), mais recente primeiro;
    /// filtro por device e status (null = todos).
    /// </summary>
    Task<PagedResult<AlertWithDevice>> SearchAsync(
        Guid? deviceId,
        AlertStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Alert com o device já carregado (projeção do search).</summary>
public sealed record AlertWithDevice(Alert Alert, string DeviceName);

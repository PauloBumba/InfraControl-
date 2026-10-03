using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Alerts.Ports;

public interface IAlertRepository : IRepository<Alert>
{
    /// <summary>
    /// Alerta ativo (Open OU Suppressed — F6-T004) de (device, check)
    /// com tracking (para Resolve/Suppress/Reopen) ou null.
    /// </summary>
    Task<Alert?> GetActiveAsync(
        Guid deviceId,
        CheckKind kind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ADR-027: alerta de fonte externa por (Source, ExternalProblemId), com
    /// tracking (Resolve na recuperação); não-Resolved primeiro (replay
    /// pós-resolução acha o row antigo).
    /// </summary>
    Task<Alert?> GetByExternalAsync(
        MonitoringSource source,
        string externalProblemId,
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

/// <summary>
/// Alert com o device já carregado (projeção do search). DeviceName null
/// quando o alerta externo não mapeou device (ADR-027).
/// </summary>
public sealed record AlertWithDevice(Alert Alert, string? DeviceName);

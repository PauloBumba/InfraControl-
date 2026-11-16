using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Monitoring.Results.Ports;

public interface IMonitoringResultRepository : IRepository<MonitoringResult>
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Últimos polls do device, mais recente primeiro (F5-T002).</summary>
    Task<IReadOnlyList<MonitoringResult>> GetRecentByDeviceAsync(
        Guid deviceId, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Últimos polls do device+kind, mais recente primeiro — streak de
    /// falhas consecutivas do avaliador de alertas (F6-T002).
    /// </summary>
    Task<IReadOnlyList<MonitoringResult>> GetRecentByDeviceAndKindAsync(
        Guid deviceId,
        Domain.Enums.CheckKind kind,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Último resultado de Ping por device (um por device) — status
    /// agregado do dashboard sem N+1 (F54-T002 / docs/69).
    /// </summary>
    Task<IReadOnlyList<MonitoringResult>> GetLatestPingByDeviceAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Contagem de polls por MINUTO no período (total e falhas) — série do
    /// dashboard no período de 1h (F54-T003 / docs/69).
    /// </summary>
    Task<IReadOnlyList<PollCountRow>> GetPollCountsByMinuteAsync(
        DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Contagem de polls por HORA no período (total e falhas) — série do
    /// dashboard em 24h/7d/30d (F54-T003 / docs/69).
    /// </summary>
    Task<IReadOnlyList<PollCountRow>> GetPollCountsByHourAsync(
        DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>Linha agregada de polls do dashboard (F54-T003).</summary>
public sealed record PollCountRow(DateTime BucketUtc, int Total, int Failed);

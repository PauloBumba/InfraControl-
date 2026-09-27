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
}

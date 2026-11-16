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
    /// F54-T001: últimos polls de TODOS os devices (mais recente primeiro),
    /// limitado — base do fallback "sem alertas/Zabbix" do status da frota
    /// (o handler agrupa por device e fica com os N mais recentes de cada).
    /// </summary>
    Task<IReadOnlyList<MonitoringResult>> GetRecentAsync(
        int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// F54-T001: contagens de polls agregadas em células de 5 minutos no
    /// banco (GROUP BY) — série de disponibilidade sem varrer linha a
    /// linha. O handler funde as células nos buckets pedidos.
    /// </summary>
    Task<IReadOnlyList<MonitoringAvailabilityCell>> GetAvailabilityCellsAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// ADR-068: polls agregados por (DeviceId, Kind) desde <paramref name="sinceUtc"/>
    /// — base do cálculo de disponibilidade dos SLOs vinculados a checks
    /// (o pareamento check↔par é resolvido em memória pelo worker).
    /// </summary>
    Task<IReadOnlyList<DeviceKindPollCount>> GetSuccessCountsByDeviceKindAsync(
        DateTime sinceUtc, CancellationToken cancellationToken = default);
}

/// <summary>Agregado de polls de um par (DeviceId, Kind) na janela.</summary>
public sealed record DeviceKindPollCount(
    Guid DeviceId,
    Domain.Enums.CheckKind Kind,
    int Total,
    int Succeeded);

/// <summary>Célula agregada de 5 min: polls totais e com sucesso.</summary>
public sealed record MonitoringAvailabilityCell(
    int Year,
    int Month,
    int Day,
    int Hour,
    int MinuteBucket,
    int Total,
    int Succeeded)
{
    /// <summary>Início UTC da célula (MinuteBucket = minuto/5, 0..11).</summary>
    public DateTime StartUtc =>
        new(Year, Month, Day, Hour, MinuteBucket * 5, 0, DateTimeKind.Utc);
}

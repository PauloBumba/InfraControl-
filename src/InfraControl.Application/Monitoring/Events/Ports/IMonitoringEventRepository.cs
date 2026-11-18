using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Monitoring.Events.Ports;

public interface IMonitoringEventRepository : IRepository<MonitoringEvent>
{
    /// <summary>
    /// Evento pelo par (Source, ExternalEventId) — dedup idempotente da
    /// ingestão (unicidade física em IX_monitoring_events_source_external_event_id).
    /// Com tracking: a transição Problem→Recovered usa MarkRecovered.
    /// </summary>
    Task<MonitoringEvent?> GetBySourceAndExternalEventIdAsync(
        MonitoringSource source,
        string externalEventId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Projeção mínima (OccurredAt/Severity/Status) dos eventos da fonte
    /// desde <paramref name="sinceUtc"/> — base dos gráficos de estatística.
    /// </summary>
    Task<IReadOnlyList<MonitoringEventStatRow>> ListStatsSinceAsync(
        MonitoringSource source,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>Linha crua de evento de monitoramento para agregação em stats.</summary>
public sealed record MonitoringEventStatRow(
    DateTime OccurredAtUtc,
    int Severity,
    MonitoringEventStatus Status);

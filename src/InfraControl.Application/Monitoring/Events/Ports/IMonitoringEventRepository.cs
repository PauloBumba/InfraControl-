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
}

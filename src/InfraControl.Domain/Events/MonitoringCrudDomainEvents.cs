using InfraControl.Domain.Common.Events;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Events;

// ADR-030 §5 — eventos CRUD de Monitoring (check + ciclo do discovery).

public sealed class MonitoringCheckCreatedDomainEvent : DomainEvent
{
    public MonitoringCheckCreatedDomainEvent(Guid monitoringCheckId, Guid deviceId, CheckKind kind, bool enabled)
    {
        MonitoringCheckId = monitoringCheckId;
        DeviceId = deviceId;
        Kind = kind;
        Enabled = enabled;
    }

    public Guid MonitoringCheckId { get; }
    public Guid DeviceId { get; }
    public CheckKind Kind { get; }
    public bool Enabled { get; }
    public override string EventType => nameof(MonitoringCheckCreatedDomainEvent);
}

public sealed class MonitoringCheckUpdatedDomainEvent : DomainEvent
{
    public MonitoringCheckUpdatedDomainEvent(Guid monitoringCheckId, Guid deviceId, bool enabled)
    {
        MonitoringCheckId = monitoringCheckId;
        DeviceId = deviceId;
        Enabled = enabled;
    }

    public Guid MonitoringCheckId { get; }
    public Guid DeviceId { get; }
    public bool Enabled { get; }
    public override string EventType => nameof(MonitoringCheckUpdatedDomainEvent);
}

public sealed class MonitoringCheckDeletedDomainEvent : DomainEvent
{
    public MonitoringCheckDeletedDomainEvent(Guid monitoringCheckId, Guid deviceId, CheckKind kind)
    {
        MonitoringCheckId = monitoringCheckId;
        DeviceId = deviceId;
        Kind = kind;
    }

    public Guid MonitoringCheckId { get; }
    public Guid DeviceId { get; }
    public CheckKind Kind { get; }
    public override string EventType => nameof(MonitoringCheckDeletedDomainEvent);
}

public sealed class DiscoveryRunCreatedDomainEvent : DomainEvent
{
    public DiscoveryRunCreatedDomainEvent(Guid discoveryRunId, Guid deviceId)
    {
        DiscoveryRunId = discoveryRunId;
        DeviceId = deviceId;
    }

    public Guid DiscoveryRunId { get; }
    public Guid DeviceId { get; }
    public override string EventType => nameof(DiscoveryRunCreatedDomainEvent);
}

public sealed class DiscoveryRunUpdatedDomainEvent : DomainEvent
{
    public DiscoveryRunUpdatedDomainEvent(Guid discoveryRunId, Guid deviceId, DiscoveryRunStatus status)
    {
        DiscoveryRunId = discoveryRunId;
        DeviceId = deviceId;
        Status = status;
    }

    public Guid DiscoveryRunId { get; }
    public Guid DeviceId { get; }
    public DiscoveryRunStatus Status { get; }
    public override string EventType => nameof(DiscoveryRunUpdatedDomainEvent);
}

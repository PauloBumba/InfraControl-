using InfraControl.Domain.Common.Events;

namespace InfraControl.Domain.Events;

// ADR-030 §5 — eventos CRUD de SLA (service, política, SLO e telemetria).

public sealed class ServiceCreatedDomainEvent : DomainEvent
{
    public ServiceCreatedDomainEvent(Guid serviceId, string name)
    {
        ServiceId = serviceId;
        Name = name;
    }

    public Guid ServiceId { get; }
    public string Name { get; }
    public override string EventType => nameof(ServiceCreatedDomainEvent);
}

public sealed class ServiceUpdatedDomainEvent : DomainEvent
{
    public ServiceUpdatedDomainEvent(Guid serviceId, string name)
    {
        ServiceId = serviceId;
        Name = name;
    }

    public Guid ServiceId { get; }
    public string Name { get; }
    public override string EventType => nameof(ServiceUpdatedDomainEvent);
}

public sealed class ServiceDeletedDomainEvent : DomainEvent
{
    public ServiceDeletedDomainEvent(Guid serviceId, string name)
    {
        ServiceId = serviceId;
        Name = name;
    }

    public Guid ServiceId { get; }
    public string Name { get; }
    public override string EventType => nameof(ServiceDeletedDomainEvent);
}

public sealed class SlaPolicyCreatedDomainEvent : DomainEvent
{
    public SlaPolicyCreatedDomainEvent(Guid slaPolicyId, string name, int responseMinutes, int resolutionMinutes)
    {
        SlaPolicyId = slaPolicyId;
        Name = name;
        ResponseMinutes = responseMinutes;
        ResolutionMinutes = resolutionMinutes;
    }

    public Guid SlaPolicyId { get; }
    public string Name { get; }
    public int ResponseMinutes { get; }
    public int ResolutionMinutes { get; }
    public override string EventType => nameof(SlaPolicyCreatedDomainEvent);
}

public sealed class SlaPolicyUpdatedDomainEvent : DomainEvent
{
    public SlaPolicyUpdatedDomainEvent(Guid slaPolicyId, string name)
    {
        SlaPolicyId = slaPolicyId;
        Name = name;
    }

    public Guid SlaPolicyId { get; }
    public string Name { get; }
    public override string EventType => nameof(SlaPolicyUpdatedDomainEvent);
}

public sealed class SlaPolicyDeletedDomainEvent : DomainEvent
{
    public SlaPolicyDeletedDomainEvent(Guid slaPolicyId, string name)
    {
        SlaPolicyId = slaPolicyId;
        Name = name;
    }

    public Guid SlaPolicyId { get; }
    public string Name { get; }
    public override string EventType => nameof(SlaPolicyDeletedDomainEvent);
}

public sealed class SloCreatedDomainEvent : DomainEvent
{
    public SloCreatedDomainEvent(Guid sloId, string name, decimal targetPercent, Guid serviceId)
    {
        SloId = sloId;
        Name = name;
        TargetPercent = targetPercent;
        ServiceId = serviceId;
    }

    public Guid SloId { get; }
    public string Name { get; }
    public decimal TargetPercent { get; }
    public Guid ServiceId { get; }
    public override string EventType => nameof(SloCreatedDomainEvent);
}

public sealed class SloUpdatedDomainEvent : DomainEvent
{
    public SloUpdatedDomainEvent(Guid sloId, string name, decimal targetPercent)
    {
        SloId = sloId;
        Name = name;
        TargetPercent = targetPercent;
    }

    public Guid SloId { get; }
    public string Name { get; }
    public decimal TargetPercent { get; }
    public override string EventType => nameof(SloUpdatedDomainEvent);
}

public sealed class SloDeletedDomainEvent : DomainEvent
{
    public SloDeletedDomainEvent(Guid sloId, string name)
    {
        SloId = sloId;
        Name = name;
    }

    public Guid SloId { get; }
    public string Name { get; }
    public override string EventType => nameof(SloDeletedDomainEvent);
}

public sealed class SloBudgetCreatedDomainEvent : DomainEvent
{
    public SloBudgetCreatedDomainEvent(Guid sloBudgetId, Guid sloId, decimal consumedPercent)
    {
        SloBudgetId = sloBudgetId;
        SloId = sloId;
        ConsumedPercent = consumedPercent;
    }

    public Guid SloBudgetId { get; }
    public Guid SloId { get; }
    public decimal ConsumedPercent { get; }
    public override string EventType => nameof(SloBudgetCreatedDomainEvent);
}

public sealed class SloBudgetDeletedDomainEvent : DomainEvent
{
    public SloBudgetDeletedDomainEvent(Guid sloBudgetId, Guid sloId)
    {
        SloBudgetId = sloBudgetId;
        SloId = sloId;
    }

    public Guid SloBudgetId { get; }
    public Guid SloId { get; }
    public override string EventType => nameof(SloBudgetDeletedDomainEvent);
}

public sealed class SloMeasurementCreatedDomainEvent : DomainEvent
{
    public SloMeasurementCreatedDomainEvent(Guid sloMeasurementId, Guid sloId, decimal valuePercent)
    {
        SloMeasurementId = sloMeasurementId;
        SloId = sloId;
        ValuePercent = valuePercent;
    }

    public Guid SloMeasurementId { get; }
    public Guid SloId { get; }
    public decimal ValuePercent { get; }
    public override string EventType => nameof(SloMeasurementCreatedDomainEvent);
}

public sealed class SloMeasurementDeletedDomainEvent : DomainEvent
{
    public SloMeasurementDeletedDomainEvent(Guid sloMeasurementId, Guid sloId)
    {
        SloMeasurementId = sloMeasurementId;
        SloId = sloId;
    }

    public Guid SloMeasurementId { get; }
    public Guid SloId { get; }
    public override string EventType => nameof(SloMeasurementDeletedDomainEvent);
}

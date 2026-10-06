using InfraControl.Domain.Common.Events;

namespace InfraControl.Domain.Events;

// ADR-030 §5 — eventos CRUD dos agregados operacionais de Network.

public sealed class VlanCreatedDomainEvent : DomainEvent
{
    public VlanCreatedDomainEvent(Guid vlanId, string name, int vlanNumber)
    {
        VlanId = vlanId;
        Name = name;
        VlanNumber = vlanNumber;
    }

    public Guid VlanId { get; }
    public string Name { get; }
    public int VlanNumber { get; }
    public override string EventType => nameof(VlanCreatedDomainEvent);
}

public sealed class VlanUpdatedDomainEvent : DomainEvent
{
    public VlanUpdatedDomainEvent(Guid vlanId, string name, int vlanNumber)
    {
        VlanId = vlanId;
        Name = name;
        VlanNumber = vlanNumber;
    }

    public Guid VlanId { get; }
    public string Name { get; }
    public int VlanNumber { get; }
    public override string EventType => nameof(VlanUpdatedDomainEvent);
}

public sealed class VlanDeletedDomainEvent : DomainEvent
{
    public VlanDeletedDomainEvent(Guid vlanId, string name, int vlanNumber)
    {
        VlanId = vlanId;
        Name = name;
        VlanNumber = vlanNumber;
    }

    public Guid VlanId { get; }
    public string Name { get; }
    public int VlanNumber { get; }
    public override string EventType => nameof(VlanDeletedDomainEvent);
}

public sealed class SwitchPortCreatedDomainEvent : DomainEvent
{
    public SwitchPortCreatedDomainEvent(Guid switchPortId, Guid deviceId, string name)
    {
        SwitchPortId = switchPortId;
        DeviceId = deviceId;
        Name = name;
    }

    public Guid SwitchPortId { get; }
    public Guid DeviceId { get; }
    public string Name { get; }
    public override string EventType => nameof(SwitchPortCreatedDomainEvent);
}

public sealed class SwitchPortUpdatedDomainEvent : DomainEvent
{
    public SwitchPortUpdatedDomainEvent(Guid switchPortId, Guid deviceId, string name)
    {
        SwitchPortId = switchPortId;
        DeviceId = deviceId;
        Name = name;
    }

    public Guid SwitchPortId { get; }
    public Guid DeviceId { get; }
    public string Name { get; }
    public override string EventType => nameof(SwitchPortUpdatedDomainEvent);
}

public sealed class SwitchPortDeletedDomainEvent : DomainEvent
{
    public SwitchPortDeletedDomainEvent(Guid switchPortId, Guid deviceId, string name)
    {
        SwitchPortId = switchPortId;
        DeviceId = deviceId;
        Name = name;
    }

    public Guid SwitchPortId { get; }
    public Guid DeviceId { get; }
    public string Name { get; }
    public override string EventType => nameof(SwitchPortDeletedDomainEvent);
}

public sealed class SwitchPortVlanCreatedDomainEvent : DomainEvent
{
    public SwitchPortVlanCreatedDomainEvent(Guid switchPortVlanId, Guid switchPortId, Guid vlanId)
    {
        SwitchPortVlanId = switchPortVlanId;
        SwitchPortId = switchPortId;
        VlanId = vlanId;
    }

    public Guid SwitchPortVlanId { get; }
    public Guid SwitchPortId { get; }
    public Guid VlanId { get; }
    public override string EventType => nameof(SwitchPortVlanCreatedDomainEvent);
}

public sealed class SwitchPortVlanDeletedDomainEvent : DomainEvent
{
    public SwitchPortVlanDeletedDomainEvent(Guid switchPortVlanId, Guid switchPortId, Guid vlanId)
    {
        SwitchPortVlanId = switchPortVlanId;
        SwitchPortId = switchPortId;
        VlanId = vlanId;
    }

    public Guid SwitchPortVlanId { get; }
    public Guid SwitchPortId { get; }
    public Guid VlanId { get; }
    public override string EventType => nameof(SwitchPortVlanDeletedDomainEvent);
}

public sealed class ConnectionCreatedDomainEvent : DomainEvent
{
    public ConnectionCreatedDomainEvent(
        Guid connectionId, Guid sourceInterfaceId, Guid targetInterfaceId)
    {
        ConnectionId = connectionId;
        SourceInterfaceId = sourceInterfaceId;
        TargetInterfaceId = targetInterfaceId;
    }

    public Guid ConnectionId { get; }
    public Guid SourceInterfaceId { get; }
    public Guid TargetInterfaceId { get; }
    public override string EventType => nameof(ConnectionCreatedDomainEvent);
}

public sealed class ConnectionUpdatedDomainEvent : DomainEvent
{
    public ConnectionUpdatedDomainEvent(
        Guid connectionId, Guid sourceInterfaceId, Guid targetInterfaceId)
    {
        ConnectionId = connectionId;
        SourceInterfaceId = sourceInterfaceId;
        TargetInterfaceId = targetInterfaceId;
    }

    public Guid ConnectionId { get; }
    public Guid SourceInterfaceId { get; }
    public Guid TargetInterfaceId { get; }
    public override string EventType => nameof(ConnectionUpdatedDomainEvent);
}

public sealed class ConnectionDeletedDomainEvent : DomainEvent
{
    public ConnectionDeletedDomainEvent(
        Guid connectionId, Guid sourceInterfaceId, Guid targetInterfaceId)
    {
        ConnectionId = connectionId;
        SourceInterfaceId = sourceInterfaceId;
        TargetInterfaceId = targetInterfaceId;
    }

    public Guid ConnectionId { get; }
    public Guid SourceInterfaceId { get; }
    public Guid TargetInterfaceId { get; }
    public override string EventType => nameof(ConnectionDeletedDomainEvent);
}

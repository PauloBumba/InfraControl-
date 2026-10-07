using InfraControl.Domain.Common.Events;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Events;

// ADR-030 §5 — eventos CRUD dos agregados operacionais de Inventory.
// Payload 1:1 com as propriedades de identidade; namespace fixo
// (constraint de InfraControl.Domain.Events no OutboxDispatcherWorker).

public sealed class DeviceCreatedDomainEvent : DomainEvent
{
    public DeviceCreatedDomainEvent(Guid deviceId, string name, DeviceType type)
    {
        DeviceId = deviceId;
        Name = name;
        Type = type;
    }

    public Guid DeviceId { get; }
    public string Name { get; }
    public DeviceType Type { get; }
    public override string EventType => nameof(DeviceCreatedDomainEvent);
}

public sealed class DeviceUpdatedDomainEvent : DomainEvent
{
    public DeviceUpdatedDomainEvent(Guid deviceId, string name, DeviceType type)
    {
        DeviceId = deviceId;
        Name = name;
        Type = type;
    }

    public Guid DeviceId { get; }
    public string Name { get; }
    public DeviceType Type { get; }
    public override string EventType => nameof(DeviceUpdatedDomainEvent);
}

public sealed class DeviceDeletedDomainEvent : DomainEvent
{
    public DeviceDeletedDomainEvent(Guid deviceId, string name, DeviceType type)
    {
        DeviceId = deviceId;
        Name = name;
        Type = type;
    }

    public Guid DeviceId { get; }
    public string Name { get; }
    public DeviceType Type { get; }
    public override string EventType => nameof(DeviceDeletedDomainEvent);
}

public sealed class LocationCreatedDomainEvent : DomainEvent
{
    public LocationCreatedDomainEvent(Guid locationId, string name)
    {
        LocationId = locationId;
        Name = name;
    }

    public Guid LocationId { get; }
    public string Name { get; }
    public override string EventType => nameof(LocationCreatedDomainEvent);
}

public sealed class LocationUpdatedDomainEvent : DomainEvent
{
    public LocationUpdatedDomainEvent(Guid locationId, string name)
    {
        LocationId = locationId;
        Name = name;
    }

    public Guid LocationId { get; }
    public string Name { get; }
    public override string EventType => nameof(LocationUpdatedDomainEvent);
}

public sealed class LocationDeletedDomainEvent : DomainEvent
{
    public LocationDeletedDomainEvent(Guid locationId, string name)
    {
        LocationId = locationId;
        Name = name;
    }

    public Guid LocationId { get; }
    public string Name { get; }
    public override string EventType => nameof(LocationDeletedDomainEvent);
}

public sealed class RackCreatedDomainEvent : DomainEvent
{
    public RackCreatedDomainEvent(Guid rackId, Guid locationId, string name)
    {
        RackId = rackId;
        LocationId = locationId;
        Name = name;
    }

    public Guid RackId { get; }
    public Guid LocationId { get; }
    public string Name { get; }
    public override string EventType => nameof(RackCreatedDomainEvent);
}

public sealed class RackUpdatedDomainEvent : DomainEvent
{
    public RackUpdatedDomainEvent(Guid rackId, Guid locationId, string name)
    {
        RackId = rackId;
        LocationId = locationId;
        Name = name;
    }

    public Guid RackId { get; }
    public Guid LocationId { get; }
    public string Name { get; }
    public override string EventType => nameof(RackUpdatedDomainEvent);
}

public sealed class RackDeletedDomainEvent : DomainEvent
{
    public RackDeletedDomainEvent(Guid rackId, Guid locationId, string name)
    {
        RackId = rackId;
        LocationId = locationId;
        Name = name;
    }

    public Guid RackId { get; }
    public Guid LocationId { get; }
    public string Name { get; }
    public override string EventType => nameof(RackDeletedDomainEvent);
}

public sealed class IpAddressCreatedDomainEvent : DomainEvent
{
    public IpAddressCreatedDomainEvent(Guid ipAddressId, Guid interfaceId, string ip)
    {
        IpAddressId = ipAddressId;
        InterfaceId = interfaceId;
        Ip = ip;
    }

    public Guid IpAddressId { get; }
    public Guid InterfaceId { get; }
    public string Ip { get; }
    public override string EventType => nameof(IpAddressCreatedDomainEvent);
}

public sealed class IpAddressUpdatedDomainEvent : DomainEvent
{
    public IpAddressUpdatedDomainEvent(Guid ipAddressId, Guid interfaceId, string ip)
    {
        IpAddressId = ipAddressId;
        InterfaceId = interfaceId;
        Ip = ip;
    }

    public Guid IpAddressId { get; }
    public Guid InterfaceId { get; }
    public string Ip { get; }
    public override string EventType => nameof(IpAddressUpdatedDomainEvent);
}

public sealed class IpAddressDeletedDomainEvent : DomainEvent
{
    public IpAddressDeletedDomainEvent(Guid ipAddressId, Guid interfaceId, string ip)
    {
        IpAddressId = ipAddressId;
        InterfaceId = interfaceId;
        Ip = ip;
    }

    public Guid IpAddressId { get; }
    public Guid InterfaceId { get; }
    public string Ip { get; }
    public override string EventType => nameof(IpAddressDeletedDomainEvent);
}

public sealed class MacAddressCreatedDomainEvent : DomainEvent
{
    public MacAddressCreatedDomainEvent(Guid macAddressId, Guid interfaceId, string mac)
    {
        MacAddressId = macAddressId;
        InterfaceId = interfaceId;
        Mac = mac;
    }

    public Guid MacAddressId { get; }
    public Guid InterfaceId { get; }
    public string Mac { get; }
    public override string EventType => nameof(MacAddressCreatedDomainEvent);
}

public sealed class MacAddressUpdatedDomainEvent : DomainEvent
{
    public MacAddressUpdatedDomainEvent(Guid macAddressId, Guid interfaceId, string mac)
    {
        MacAddressId = macAddressId;
        InterfaceId = interfaceId;
        Mac = mac;
    }

    public Guid MacAddressId { get; }
    public Guid InterfaceId { get; }
    public string Mac { get; }
    public override string EventType => nameof(MacAddressUpdatedDomainEvent);
}

public sealed class MacAddressDeletedDomainEvent : DomainEvent
{
    public MacAddressDeletedDomainEvent(Guid macAddressId, Guid interfaceId, string mac)
    {
        MacAddressId = macAddressId;
        InterfaceId = interfaceId;
        Mac = mac;
    }

    public Guid MacAddressId { get; }
    public Guid InterfaceId { get; }
    public string Mac { get; }
    public override string EventType => nameof(MacAddressDeletedDomainEvent);
}

public sealed class NetworkInterfaceCreatedDomainEvent : DomainEvent
{
    public NetworkInterfaceCreatedDomainEvent(Guid networkInterfaceId, Guid deviceId, string name)
    {
        NetworkInterfaceId = networkInterfaceId;
        DeviceId = deviceId;
        Name = name;
    }

    public Guid NetworkInterfaceId { get; }
    public Guid DeviceId { get; }
    public string Name { get; }
    public override string EventType => nameof(NetworkInterfaceCreatedDomainEvent);
}

public sealed class NetworkInterfaceUpdatedDomainEvent : DomainEvent
{
    public NetworkInterfaceUpdatedDomainEvent(Guid networkInterfaceId, Guid deviceId, string name)
    {
        NetworkInterfaceId = networkInterfaceId;
        DeviceId = deviceId;
        Name = name;
    }

    public Guid NetworkInterfaceId { get; }
    public Guid DeviceId { get; }
    public string Name { get; }
    public override string EventType => nameof(NetworkInterfaceUpdatedDomainEvent);
}

public sealed class NetworkInterfaceDeletedDomainEvent : DomainEvent
{
    public NetworkInterfaceDeletedDomainEvent(Guid networkInterfaceId, Guid deviceId, string name)
    {
        NetworkInterfaceId = networkInterfaceId;
        DeviceId = deviceId;
        Name = name;
    }

    public Guid NetworkInterfaceId { get; }
    public Guid DeviceId { get; }
    public string Name { get; }
    public override string EventType => nameof(NetworkInterfaceDeletedDomainEvent);
}

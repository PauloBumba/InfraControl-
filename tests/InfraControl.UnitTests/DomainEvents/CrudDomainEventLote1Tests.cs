using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.UnitTests.DomainEvents;

/// <summary>
/// ADR-030 §5 — Lote 1 (Inventory + Network): cada agregado operacional
/// levanta Created no Create, Updated no mutador e Deleted no Delete()
/// (namespace InfraControl.Domain.Events — constraint do outbox).
/// </summary>
public sealed class CrudDomainEventLote1Tests
{
    private static readonly Guid SomeLocationId = Guid.NewGuid();
    private static readonly Guid SomeDeviceId = Guid.NewGuid();
    private static readonly Guid SomeInterfaceId = Guid.NewGuid();

    // ── Device ───────────────────────────────────────────────────────────

    [Fact]
    public void Device_Create_ShouldRaiseCreatedOnly()
    {
        var device = Device.Create(SomeLocationId, "sw-core-01", DeviceType.Switch);

        device.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DeviceCreatedDomainEvent>();
        var evt = (DeviceCreatedDomainEvent)device.DomainEvents.Single();
        evt.DeviceId.Should().Be(device.Id);
        evt.Name.Should().Be("sw-core-01");
        evt.Type.Should().Be(DeviceType.Switch);
    }

    [Fact]
    public void Device_Update_ShouldRaiseUpdated()
    {
        var device = Device.Create(SomeLocationId, "sw-core-01", DeviceType.Switch);
        device.ClearDomainEvents();

        device.Update("sw-core-02", DeviceType.Switch, null);

        device.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DeviceUpdatedDomainEvent>()
            .Which.Name.Should().Be("sw-core-02");
    }

    [Fact]
    public void Device_Delete_ShouldRaiseDeleted()
    {
        var device = Device.Create(SomeLocationId, "sw-core-01", DeviceType.Switch);
        device.ClearDomainEvents();

        device.Delete();

        device.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DeviceDeletedDomainEvent>();
    }

    // ── Location ─────────────────────────────────────────────────────────

    [Fact]
    public void Location_ShouldRaiseCreatedUpdatedDeleted()
    {
        var location = Location.Create("Data Center A");
        location.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<LocationCreatedDomainEvent>();

        location.ClearDomainEvents();
        location.Update("Data Center B", null);
        location.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<LocationUpdatedDomainEvent>()
            .Which.Name.Should().Be("Data Center B");

        location.ClearDomainEvents();
        location.Delete();
        location.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<LocationDeletedDomainEvent>();
    }

    // ── Rack ─────────────────────────────────────────────────────────────

    [Fact]
    public void Rack_ShouldRaiseCreatedUpdatedDeleted()
    {
        var rack = Rack.Create(SomeLocationId, "Rack 01");
        rack.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RackCreatedDomainEvent>();

        rack.ClearDomainEvents();
        rack.Update("Rack 02", null);
        rack.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RackUpdatedDomainEvent>();

        rack.ClearDomainEvents();
        rack.Delete();
        rack.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RackDeletedDomainEvent>();
    }

    // ── IpAddress ────────────────────────────────────────────────────────

    [Fact]
    public void IpAddress_ShouldRaiseCreatedUpdatedDeleted()
    {
        var ip = IpAddress.Create(SomeInterfaceId, "10.0.0.1");
        ip.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<IpAddressCreatedDomainEvent>();

        ip.ClearDomainEvents();
        ip.Update("10.0.0.2");
        ip.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<IpAddressUpdatedDomainEvent>()
            .Which.Ip.Should().Be("10.0.0.2");

        ip.ClearDomainEvents();
        ip.Delete();
        ip.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<IpAddressDeletedDomainEvent>();
    }

    // ── MacAddress ───────────────────────────────────────────────────────

    [Fact]
    public void MacAddress_ShouldRaiseCreatedUpdatedDeleted()
    {
        var mac = MacAddress.Create(SomeInterfaceId, "AA:BB:CC:DD:EE:FF");
        mac.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MacAddressCreatedDomainEvent>();

        mac.ClearDomainEvents();
        mac.Update("11:22:33:44:55:66");
        mac.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MacAddressUpdatedDomainEvent>();

        mac.ClearDomainEvents();
        mac.Delete();
        mac.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MacAddressDeletedDomainEvent>();
    }

    // ── NetworkInterface ─────────────────────────────────────────────────

    [Fact]
    public void NetworkInterface_ShouldRaiseCreatedUpdatedDeleted()
    {
        var nic = NetworkInterface.Create(SomeDeviceId, "eth0");
        nic.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<NetworkInterfaceCreatedDomainEvent>();

        nic.ClearDomainEvents();
        nic.Update("eth1", null);
        nic.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<NetworkInterfaceUpdatedDomainEvent>();

        nic.ClearDomainEvents();
        nic.Delete();
        nic.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<NetworkInterfaceDeletedDomainEvent>();
    }

    // ── Vlan ─────────────────────────────────────────────────────────────

    [Fact]
    public void Vlan_ShouldRaiseCreatedUpdatedDeleted()
    {
        var vlan = Vlan.Create("VLAN Produção", 100);
        vlan.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<VlanCreatedDomainEvent>();

        vlan.ClearDomainEvents();
        vlan.Update("VLAN Dados", 200, null);
        vlan.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<VlanUpdatedDomainEvent>()
            .Which.VlanNumber.Should().Be(200);

        vlan.ClearDomainEvents();
        vlan.Delete();
        vlan.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<VlanDeletedDomainEvent>();
    }

    // ── SwitchPort ───────────────────────────────────────────────────────

    [Fact]
    public void SwitchPort_ShouldRaiseCreatedUpdatedDeleted()
    {
        var port = SwitchPort.Create(SomeDeviceId, "Gi1/0/1");
        port.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SwitchPortCreatedDomainEvent>();

        port.ClearDomainEvents();
        port.Update("Gi1/0/2", null, null);
        port.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SwitchPortUpdatedDomainEvent>();

        port.ClearDomainEvents();
        port.Delete();
        port.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SwitchPortDeletedDomainEvent>();
    }

    // ── SwitchPortVlan (sem PUT — Created/Deleted) ───────────────────────

    [Fact]
    public void SwitchPortVlan_ShouldRaiseCreatedAndDeleted()
    {
        var link = SwitchPortVlan.Create(Guid.NewGuid(), Guid.NewGuid());
        link.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SwitchPortVlanCreatedDomainEvent>();

        link.ClearDomainEvents();
        link.Delete();
        link.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SwitchPortVlanDeletedDomainEvent>();
    }

    // ── Connection ───────────────────────────────────────────────────────

    [Fact]
    public void Connection_ShouldRaiseCreatedUpdatedDeleted()
    {
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();
        var connection = Connection.Create(source, target);
        connection.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ConnectionCreatedDomainEvent>();

        connection.ClearDomainEvents();
        var newTarget = Guid.NewGuid();
        connection.Repoint(source, newTarget);
        connection.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ConnectionUpdatedDomainEvent>()
            .Which.TargetInterfaceId.Should().Be(newTarget);

        connection.ClearDomainEvents();
        connection.Delete();
        connection.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ConnectionDeletedDomainEvent>();
    }
}

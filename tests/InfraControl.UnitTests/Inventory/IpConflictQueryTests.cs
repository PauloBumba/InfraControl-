using FluentAssertions;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.IpAddresses.Ports;
using InfraControl.Application.Inventory.IpAddresses.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Moq;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F29-4: detecção de conflito de IP cruza as fontes persistidas (interface
/// de device, IP de gestão e host Zabbix correlacionado via
/// ExternalDeviceReference) e devolve só IPs com 2+ identidades distintas.
/// Read-model — REGRA 8 (nada de telemetria persistida).
/// </summary>
public sealed class IpConflictQueryTests
{
    [Fact]
    public async Task SameDeviceWithInterfaceAndManagementIp_ShouldNotConflict()
    {
        var device = Device.Create(
            Guid.NewGuid(), "sw-core", DeviceType.Switch, ip: "192.0.2.10");
        var handler = BuildHandler(
            interfaceClaims: [new IpInterfaceClaim("192.0.2.10", device.Id)],
            devices: [device]);

        var result = await handler.Handle(new GetIpConflictsQuery(), CancellationToken.None);

        result.Should().BeEmpty("é o mesmo device — mesma identidade, não é conflito");
    }

    [Fact]
    public async Task SameIpOnTwoDifferentDevices_ShouldConflict()
    {
        var deviceA = Device.Create(Guid.NewGuid(), "sw-a", DeviceType.Switch);
        var deviceB = Device.Create(Guid.NewGuid(), "sw-b", DeviceType.Switch, ip: "192.0.2.10");
        var handler = BuildHandler(
            interfaceClaims: [new IpInterfaceClaim("192.0.2.10", deviceA.Id)],
            devices: [deviceA, deviceB]);

        var result = await handler.Handle(new GetIpConflictsQuery(), CancellationToken.None);

        var conflict = result.Should().ContainSingle().Subject;
        conflict.Ip.Should().Be("192.0.2.10");
        conflict.Claims.Should().HaveCount(2);
        conflict.Claims.Select(c => c.Source).Should().BeEquivalentTo(
            ["Interface", "Management"]);
    }

    [Fact]
    public async Task ZabbixHostUncorrelatedToIpOwner_ShouldConflict()
    {
        var device = Device.Create(Guid.NewGuid(), "sw-core", DeviceType.Switch);
        var host = ZabbixHost.Create(
            "10084", "sw-core-zbx", "SW ZBX", "192.0.2.10", 10050,
            true, false, [], [], DateTime.UtcNow);
        var handler = BuildHandler(
            interfaceClaims: [new IpInterfaceClaim("192.0.2.10", device.Id)],
            devices: [device],
            zabbixHosts: [host],
            zabbixLinks: []); // sem referência → identidade zabbix:10084

        var result = await handler.Handle(new GetIpConflictsQuery(), CancellationToken.None);

        var conflict = result.Should().ContainSingle().Subject;
        conflict.Claims.Should().HaveCount(2);
        conflict.Claims.Single(c => c.Source == "ZabbixHost").DeviceId.Should().BeNull();
    }

    [Fact]
    public async Task ZabbixHostCorrelatedToIpOwner_ShouldNotConflict()
    {
        var device = Device.Create(Guid.NewGuid(), "sw-core", DeviceType.Switch);
        var host = ZabbixHost.Create(
            "10084", "sw-core", "SW Core", "192.0.2.10", 10050,
            true, false, [], [], DateTime.UtcNow);
        var link = ExternalDeviceReference.Create(
            ExternalDeviceSource.Zabbix, "10084", device.Id);
        var handler = BuildHandler(
            interfaceClaims: [new IpInterfaceClaim("192.0.2.10", device.Id)],
            devices: [device],
            zabbixHosts: [host],
            zabbixLinks: [link]);

        var result = await handler.Handle(new GetIpConflictsQuery(), CancellationToken.None);

        result.Should().BeEmpty(
            "o host Zabbix está correlacionado ao device dono do IP — mesma identidade");
    }

    [Fact]
    public async Task ZabbixHostCorrelatedToDifferentDevice_ShouldConflict()
    {
        var owner = Device.Create(Guid.NewGuid(), "sw-core", DeviceType.Switch);
        var other = Device.Create(Guid.NewGuid(), "sw-outro", DeviceType.Switch);
        var host = ZabbixHost.Create(
            "10084", "sw-outro", "SW Outro", "192.0.2.10", 10050,
            true, false, [], [], DateTime.UtcNow);
        var link = ExternalDeviceReference.Create(
            ExternalDeviceSource.Zabbix, "10084", other.Id);
        var handler = BuildHandler(
            interfaceClaims: [new IpInterfaceClaim("192.0.2.10", owner.Id)],
            devices: [owner, other],
            zabbixHosts: [host],
            zabbixLinks: [link]);

        var result = await handler.Handle(new GetIpConflictsQuery(), CancellationToken.None);

        var conflict = result.Should().ContainSingle().Subject;
        // identidades: owner (interface) + other (zabbix correlacionado) → 2 devices
        conflict.Claims.Should().HaveCount(2);
        conflict.Claims.Select(c => c.DeviceId).Should().Contain(owner.Id)
            .And.Contain(other.Id);
    }

    [Fact]
    public async Task HostWithoutIp_ShouldBeIgnored()
    {
        var device = Device.Create(Guid.NewGuid(), "sw-core", DeviceType.Switch);
        var host = ZabbixHost.Create(
            "10084", "sem-ip", "Sem IP", null, null,
            true, false, [], [], DateTime.UtcNow);
        var handler = BuildHandler(
            interfaceClaims: [],
            devices: [device],
            zabbixHosts: [host],
            zabbixLinks: []);

        var result = await handler.Handle(new GetIpConflictsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ── Infra ───────────────────────────────────────────────────────

    private static GetIpConflictsQueryHandler BuildHandler(
        IReadOnlyList<IpInterfaceClaim> interfaceClaims,
        IReadOnlyList<Device> devices,
        IReadOnlyList<ZabbixHost>? zabbixHosts = null,
        IReadOnlyList<ExternalDeviceReference>? zabbixLinks = null)
    {
        var ipAddresses = new Mock<IIpAddressRepository>();
        ipAddresses.Setup(r => r.ListInterfaceClaimsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(interfaceClaims);

        var deviceRepository = new Mock<IDeviceRepository>();
        deviceRepository.Setup(r => r.GetAllOrderedByNameAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(devices);

        var zabbixHostsRepository = new Mock<IZabbixHostsRepository>();
        zabbixHostsRepository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(zabbixHosts ?? []);

        var references = new Mock<IExternalDeviceReferenceRepository>();
        references.Setup(r => r.ListBySourceAsync(
                ExternalDeviceSource.Zabbix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(zabbixLinks ?? []);

        return new GetIpConflictsQueryHandler(
            ipAddresses.Object, deviceRepository.Object,
            zabbixHostsRepository.Object, references.Object);
    }
}

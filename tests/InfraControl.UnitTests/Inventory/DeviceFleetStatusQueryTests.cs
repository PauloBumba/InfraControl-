using FluentAssertions;
using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Devices.Queries;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F54-T001: overview agregado da frota — mesma regra do status
/// consolidado por device, computada em lote (alertas Open agrupados,
/// referência/host/problemas Zabbix, fallback de polls recentes).
/// </summary>
public sealed class DeviceFleetStatusQueryTests
{
    private static Device NewDevice(string name, DeviceType type)
        => Device.Create(Guid.NewGuid(), name, type, ip: "10.0.0.1");

    [Fact]
    public async Task EmptyFleet_ReturnsZeros()
    {
        var result = await Handle(
            devices: [],
            openCounts: [],
            references: [],
            zabbixHosts: [],
            problems: [],
            recentResults: []);

        result.TotalDevices.Should().Be(0);
        result.ByType.Should().BeEmpty();
        result.ByStatus.Should().OnlyContain(s => s.Count == 0);
        result.OpenAlerts.Should().Be(0);
    }

    [Fact]
    public async Task CountsDevicesByTypeAndStatus()
    {
        // A: alerta crítico → Unavailable; B: host Zabbix ok → Available;
        // C: sem sinal → Unknown.
        var deviceA = NewDevice("sw-a", DeviceType.Switch);
        var deviceB = NewDevice("srv-b", DeviceType.Server);
        var deviceC = NewDevice("ws-c", DeviceType.Workstation);

        var result = await Handle(
            devices: [deviceA, deviceB, deviceC],
            openCounts: [new OpenAlertCount(deviceA.Id, AlertSeverity.Critical, 2)],
            references: [Reference(deviceB.Id, "200")],
            zabbixHosts: [ZabbixHost.Create("200", "srv-b", "srv-b", "10.0.0.1", 10051, true, false, [], [], DateTime.UtcNow)],
            problems: [],
            recentResults: []);

        result.TotalDevices.Should().Be(3);
        result.ByType.Should().Contain(t => t.Type == "Switch" && t.Count == 1);
        result.ByType.Should().Contain(t => t.Type == "Server" && t.Count == 1);
        result.ByStatus.Should().Contain(s => s.Status == "Unavailable" && s.Count == 1);
        result.ByStatus.Should().Contain(s => s.Status == "Available" && s.Count == 1);
        result.ByStatus.Should().Contain(s => s.Status == "Unknown" && s.Count == 1);
        result.OpenAlerts.Should().Be(2);
        result.OpenCriticalAlerts.Should().Be(2);
        result.OpenWarningAlerts.Should().Be(0);
    }

    [Fact]
    public async Task WarningAlert_DegradesDevice()
    {
        var device = NewDevice("sw-a", DeviceType.Switch);

        var result = await Handle(
            devices: [device],
            openCounts: [new OpenAlertCount(device.Id, AlertSeverity.Warning, 1)],
            references: [],
            zabbixHosts: [],
            problems: [],
            recentResults: []);

        result.ByStatus.Should().Contain(s => s.Status == "Degraded" && s.Count == 1);
        result.OpenWarningAlerts.Should().Be(1);
    }

    [Fact]
    public async Task ZabbixHostMissingOrDisabled_IsUnavailable()
    {
        var device = NewDevice("srv-b", DeviceType.Server);
        var missing = ZabbixHost.Create("200", "srv-b", "srv-b", "10.0.0.1", 10051, true, false, [], [], DateTime.UtcNow);
        missing.MarkMissing();

        var result = await Handle(
            devices: [device],
            openCounts: [],
            references: [Reference(device.Id, "200")],
            zabbixHosts: [missing],
            problems: [],
            recentResults: []);

        result.ByStatus.Should().Contain(s => s.Status == "Unavailable" && s.Count == 1);
    }

    [Fact]
    public async Task ActiveZabbixProblem_HighSeverityUnavailable_RecoveredIgnored()
    {
        var device = NewDevice("srv-b", DeviceType.Server);
        var host = ZabbixHost.Create("200", "srv-b", "srv-b", "10.0.0.1", 10051, true, false, [], [], DateTime.UtcNow);
        var problems = new[]
        {
            new ZabbixProblemDto("1", "CPU alta", 2, DateTime.UtcNow, null, "t1", ["srv-b"]),
            new ZabbixProblemDto("2", "Resolvido", 5, DateTime.UtcNow, "9", "t2", ["srv-b"]),
        };

        var result = await Handle(
            devices: [device],
            openCounts: [],
            references: [Reference(device.Id, "200")],
            zabbixHosts: [host],
            problems: problems,
            recentResults: []);

        // Problema ativo sev 2 → Degraded; o recuperado (sev 5) é ignorado.
        result.ByStatus.Should().Contain(s => s.Status == "Degraded" && s.Count == 1);
    }

    [Fact]
    public async Task RecentResultsFallback_AppliesOnlyWithoutAlertsAndZabbix()
    {
        // Device sem alertas/Zabbix com polls recentes todos ok → Available.
        // Device COM referência Zabbix não usa o fallback: o host 999 não
        // está no read-model → Unavailable (mesma regra do consolidado).
        var deviceA = NewDevice("sw-a", DeviceType.Switch);
        var deviceB = NewDevice("srv-b", DeviceType.Server);

        var result = await Handle(
            devices: [deviceA, deviceB],
            openCounts: [],
            references: [Reference(deviceB.Id, "999")], // host 999 não sincronizado
            zabbixHosts: [],
            problems: [],
            recentResults:
            [
                MonitoringResult.Create(deviceA.Id, CheckKind.Ping, true, 5),
                MonitoringResult.Create(deviceA.Id, CheckKind.Ping, true, 5),
                MonitoringResult.Create(deviceB.Id, CheckKind.Ping, true, 5),
            ],
            zabbixConfigured: true);

        result.ByStatus.Should().Contain(s => s.Status == "Available" && s.Count == 1);
        result.ByStatus.Should().Contain(s => s.Status == "Unavailable" && s.Count == 1);
    }

    [Fact]
    public async Task RecentFailures_DegradeDevice()
    {
        var device = NewDevice("sw-a", DeviceType.Switch);

        var result = await Handle(
            devices: [device],
            openCounts: [],
            references: [],
            zabbixHosts: [],
            problems: [],
            recentResults:
            [
                MonitoringResult.Create(device.Id, CheckKind.Ping, false, 5),
                MonitoringResult.Create(device.Id, CheckKind.Ping, true, 5),
            ]);

        result.ByStatus.Should().Contain(s => s.Status == "Degraded" && s.Count == 1);
    }

    private static async Task<DeviceFleetStatusDto> Handle(
        IReadOnlyList<Device> devices,
        IReadOnlyList<OpenAlertCount> openCounts,
        IReadOnlyList<ExternalDeviceReference> references,
        IReadOnlyList<ZabbixHost> zabbixHosts,
        IReadOnlyList<ZabbixProblemDto> problems,
        IReadOnlyList<MonitoringResult> recentResults,
        bool zabbixConfigured = true)
    {
        var deviceRepository = new Mock<IDeviceRepository>();
        deviceRepository.Setup(r => r.GetAllOrderedByNameAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(devices);

        var alertRepository = new Mock<IAlertRepository>();
        alertRepository.Setup(r => r.GetOpenCountsByDeviceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(openCounts);

        var resultRepository = new Mock<IMonitoringResultRepository>();
        resultRepository.Setup(r => r.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentResults);

        var referenceRepository = new Mock<IExternalDeviceReferenceRepository>();
        referenceRepository.Setup(r => r.ListBySourceAsync(
                ExternalDeviceSource.Zabbix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(references);

        var hostsRepository = new Mock<IZabbixHostsRepository>();
        hostsRepository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(zabbixHosts);

        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.IsConfigured).Returns(zabbixConfigured);
        client.Setup(c => c.ListProblemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(problems);

        var handler = new DeviceFleetStatusQueryHandler(
            deviceRepository.Object,
            alertRepository.Object,
            resultRepository.Object,
            referenceRepository.Object,
            hostsRepository.Object,
            client.Object,
            NullLogger<DeviceFleetStatusQueryHandler>.Instance);

        return await handler.Handle(new GetDeviceFleetStatusQuery(), CancellationToken.None);
    }

    private static ExternalDeviceReference Reference(Guid deviceId, string externalId)
        => ExternalDeviceReference.Create(ExternalDeviceSource.Zabbix, externalId, deviceId);
}

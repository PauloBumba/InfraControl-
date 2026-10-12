using FluentAssertions;
using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Devices.Queries;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F1-b (Plano Mestre §8.1): estado operacional consolidado de um device.
/// Combina: alertas abertos, host Zabbix vinculado (referência estável),
/// problemas ativos do Zabbix e resultados recentes de monitoramento próprio.
/// Regra: pior sinal vence (Unavailable > Degraded > Available); sem
/// qualquer sinal → Unknown.
/// </summary>
public sealed class DeviceConsolidatedStatusQueryTests
{
    private static readonly Guid DeviceId = Guid.NewGuid();

    [Fact]
    public async Task Unknown_WhenNoSignalsExist()
    {
        var result = await Handle(
            openAlerts: [],
            reference: null,
            zabbixHost: null,
            problems: [],
            recentResults: []);

        result.Status.Should().Be("Unknown");
        result.Sources.Should().BeEmpty();
    }

    [Fact]
    public async Task Available_WhenZabbixHostLinkedEnabledAndNoProblems()
    {
        var result = await Handle(
            openAlerts: [],
            reference: Reference(ExternalDeviceSource.Zabbix, "10084"),
            zabbixHost: CreateZabbixHost("10084", enabled: true),
            problems: [],
            recentResults: []);

        result.Status.Should().Be("Available");
        result.Sources.Should().Contain("Zabbix");
        result.Reasons.Should().Contain(r => r.Contains("10084"));
    }

    [Fact]
    public async Task Unavailable_WhenOpenCriticalAlert()
    {
        var result = await Handle(
            openAlerts: [CreateAlert(AlertSeverity.Critical)],
            reference: null,
            zabbixHost: null,
            problems: [],
            recentResults: []);

        result.Status.Should().Be("Unavailable");
        result.Sources.Should().Contain("Monitoring");
        result.Reasons.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Degraded_WhenOpenWarningAlert()
    {
        var result = await Handle(
            openAlerts: [CreateAlert(AlertSeverity.Warning)],
            reference: null,
            zabbixHost: null,
            problems: [],
            recentResults: []);

        result.Status.Should().Be("Degraded");
        result.Sources.Should().Contain("Monitoring");
    }

    [Fact]
    public async Task Unavailable_WhenLinkedZabbixHostDisabled()
    {
        var result = await Handle(
            openAlerts: [],
            reference: Reference(ExternalDeviceSource.Zabbix, "10084"),
            zabbixHost: CreateZabbixHost("10084", enabled: false),
            problems: [],
            recentResults: []);

        result.Status.Should().Be("Unavailable");
        result.Reasons.Should().Contain(r => r.Contains("desabilitado"));
    }

    [Fact]
    public async Task Unavailable_WhenLinkedZabbixHostMissing()
    {
        var host = CreateZabbixHost("10084", enabled: true);
        host.MarkMissing();

        var result = await Handle(
            openAlerts: [],
            reference: Reference(ExternalDeviceSource.Zabbix, "10084"),
            zabbixHost: host,
            problems: [],
            recentResults: []);

        result.Status.Should().Be("Unavailable");
        result.Reasons.Should().Contain(r => r.Contains("ausente"));
    }

    [Fact]
    public async Task Unavailable_WhenZabbixProblemHighSeverity()
    {
        var result = await Handle(
            openAlerts: [],
            reference: Reference(ExternalDeviceSource.Zabbix, "10084"),
            zabbixHost: CreateZabbixHost("10084", enabled: true),
            problems:
            [
                new ZabbixProblemDto("1", "ICMP ping fail", 4,
                    DateTime.UtcNow, null, "900", ["sw-core"])
            ],
            recentResults: []);

        result.Status.Should().Be("Unavailable");
        result.Sources.Should().Contain("Zabbix");
        result.Reasons.Should().Contain(r => r.Contains("ICMP ping fail"));
    }

    [Fact]
    public async Task Degraded_WhenZabbixProblemMediumSeverity()
    {
        var result = await Handle(
            openAlerts: [],
            reference: Reference(ExternalDeviceSource.Zabbix, "10084"),
            zabbixHost: CreateZabbixHost("10084", enabled: true),
            problems:
            [
                new ZabbixProblemDto("1", "High CPU", 2,
                    DateTime.UtcNow, null, "901", ["sw-core"])
            ],
            recentResults: []);

        result.Status.Should().Be("Degraded");
        result.Reasons.Should().Contain(r => r.Contains("High CPU"));
    }

    [Fact]
    public async Task ProblemsForOtherHostsAreIgnored()
    {
        var result = await Handle(
            openAlerts: [],
            reference: Reference(ExternalDeviceSource.Zabbix, "10084"),
            zabbixHost: CreateZabbixHost("10084", enabled: true, host: "sw-core"),
            problems:
            [
                new ZabbixProblemDto("1", "Other host down", 5,
                    DateTime.UtcNow, null, "902", ["sw-other"])
            ],
            recentResults: []);

        result.Status.Should().Be("Available");
        result.Reasons.Should().NotContain(r => r.Contains("Other host down"));
    }

    [Fact]
    public async Task Degraded_WhenRecentMonitoringFailures()
    {
        var result = await Handle(
            openAlerts: [],
            reference: null,
            zabbixHost: null,
            problems: [],
            recentResults:
            [
                CreateMonitoringResult(false), CreateMonitoringResult(false), CreateMonitoringResult(true)
            ]);

        result.Status.Should().Be("Degraded");
        result.Sources.Should().Contain("Monitoring");
        result.Reasons.Should().Contain(r => r.Contains("falha"));
    }

    [Fact]
    public async Task Available_WhenRecentMonitoringAllSucceed()
    {
        var result = await Handle(
            openAlerts: [],
            reference: null,
            zabbixHost: null,
            problems: [],
            recentResults: [CreateMonitoringResult(true), CreateMonitoringResult(true)]);

        result.Status.Should().Be("Available");
        result.Sources.Should().Contain("Monitoring");
    }

    [Fact]
    public async Task CriticalAlertBeatsEverythingElse()
    {
        var result = await Handle(
            openAlerts: [CreateAlert(AlertSeverity.Critical)],
            reference: Reference(ExternalDeviceSource.Zabbix, "10084"),
            zabbixHost: CreateZabbixHost("10084", enabled: true),
            problems:
            [
                new ZabbixProblemDto("1", "High CPU", 2,
                    DateTime.UtcNow, null, "901", ["sw-core"])
            ],
            recentResults: [CreateMonitoringResult(true)]);

        result.Status.Should().Be("Unavailable");
        result.Sources.Should().Contain("Monitoring");
    }

    [Fact]
    public async Task DeviceNotFound_ShouldThrow()
    {
        var deviceRepository = new Mock<IDeviceRepository>();
        deviceRepository.Setup(r => r.GetByIdAsync(DeviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Device?)null);

        var handler = CreateHandler(
            deviceRepository.Object, Mock.Of<IAlertRepository>(),
            Mock.Of<IExternalDeviceReferenceRepository>(),
            Mock.Of<IZabbixHostsRepository>(),
            Mock.Of<IZabbixClient>(),
            Mock.Of<IMonitoringResultRepository>());

        var act = async () => await handler.Handle(
            new GetDeviceConsolidatedStatusQuery(DeviceId), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    private static async Task<DeviceConsolidatedStatusDto> Handle(
        IReadOnlyList<Alert> openAlerts,
        ExternalDeviceReference? reference,
        ZabbixHost? zabbixHost,
        IReadOnlyList<ZabbixProblemDto> problems,
        IReadOnlyList<MonitoringResult> recentResults,
        bool zabbixConfigured = true)
    {
        var deviceRepository = new Mock<IDeviceRepository>();
        deviceRepository.Setup(r => r.GetByIdAsync(DeviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Device.Create(DeviceId, "sw-core", DeviceType.Switch, ip: "192.168.127.1"));

        var alertRepository = new Mock<IAlertRepository>();
        alertRepository.Setup(r => r.SearchAsync(
                DeviceId, AlertStatus.Open, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<AlertWithDevice>(
                openAlerts.Select(a => new AlertWithDevice(a, "sw-core")).ToArray(),
                openAlerts.Count, 1, 100));

        var referenceRepository = new Mock<IExternalDeviceReferenceRepository>();
        referenceRepository.Setup(r => r.ListByDeviceIdAsync(
                DeviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference is null ? [] : [reference]);

        var hostsRepository = new Mock<IZabbixHostsRepository>();
        if (zabbixHost is not null)
            hostsRepository.Setup(r => r.GetByHostIdAsync(
                    zabbixHost.HostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(zabbixHost);

        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.IsConfigured).Returns(zabbixConfigured);
        client.Setup(c => c.ListProblemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(problems);

        var resultRepository = new Mock<IMonitoringResultRepository>();
        resultRepository.Setup(r => r.GetRecentByDeviceAsync(
                DeviceId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentResults);

        var handler = CreateHandler(
            deviceRepository.Object, alertRepository.Object,
            referenceRepository.Object, hostsRepository.Object,
            client.Object, resultRepository.Object);

        return await handler.Handle(
            new GetDeviceConsolidatedStatusQuery(DeviceId), CancellationToken.None);
    }

    private static DeviceConsolidatedStatusQueryHandler CreateHandler(
        IDeviceRepository devices,
        IAlertRepository alerts,
        IExternalDeviceReferenceRepository references,
        IZabbixHostsRepository hosts,
        IZabbixClient client,
        IMonitoringResultRepository results)
        => new(devices, alerts, references, hosts, client, results,
            NullLogger<DeviceConsolidatedStatusQueryHandler>.Instance);

    private static Alert CreateAlert(AlertSeverity severity)
        => Alert.Create(DeviceId, CheckKind.Ping, severity,
            "host sem resposta");

    private static ExternalDeviceReference Reference(
        ExternalDeviceSource source, string externalId)
        => ExternalDeviceReference.Create(source, externalId, DeviceId);

    private static ZabbixHost CreateZabbixHost(
        string hostId, bool enabled, string host = "sw-core")
        => ZabbixHost.Create(hostId, host, host, "192.168.127.1",
            10051, enabled, false, [], [], DateTime.UtcNow);

    private static MonitoringResult CreateMonitoringResult(bool succeeded)
        => MonitoringResult.Create(DeviceId, CheckKind.Ping, succeeded, 12);
}

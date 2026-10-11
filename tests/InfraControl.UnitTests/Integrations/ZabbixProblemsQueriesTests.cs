using FluentAssertions;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Application.Inventory.Devices.Ports;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F27 Fase 10.1/10.2/10.3: consultas de problemas ativos (filtro/
/// ordenação/severidades), histórico sob demanda (limites) e resumo do
/// dashboard (contagens, match com Asset, falha de problemas segregada).
/// </summary>
public sealed class ZabbixProblemsQueriesTests
{
    private static ZabbixProblemDto Problem(
        string eventId, string name, int severity,
        DateTime? clock = null, string? recoveryId = null,
        string? objectId = null, IReadOnlyList<string>? hosts = null)
        => new(eventId, name, severity,
            clock ?? DateTime.UtcNow.AddMinutes(-severity),
            recoveryId, objectId, hosts);

    [Fact]
    public async Task Problems_ShouldFilterRecoveredOrderAndMapSeverityLabel()
    {
        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.ListProblemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Problem("1", "Aviso baixo", 2),
                Problem("2", "Desastre", 5),
                Problem("3", "Recuperado", 4, recoveryId: "99"),
                Problem("4", "Médio", 3, clock: DateTime.UtcNow.AddHours(-1)),
                Problem("5", "Outro alto", 4),
            });

        var handler = new GetZabbixProblemsQueryHandler(client.Object);

        var result = await handler.Handle(
            new GetZabbixProblemsQuery(), CancellationToken.None);

        result.Total.Should().Be(4, "problemas recuperados são ocultos por default");
        result.Items.Select(i => i.EventId).Should().Equal("2", "5", "4", "1");
        result.Items[0].SeverityLabel.Should().Be("Desastre");
        result.Items[2].SeverityLabel.Should().Be("Médio");
        result.Items.All(i => !i.Recovered).Should().BeTrue();
    }

    [Fact]
    public async Task Problems_WithMinSeverity_ShouldFilterAndKeepHostNames()
    {
        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.ListProblemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Problem("1", "Informativo", 1, hosts: ["sw-01"]),
                Problem("2", "Alto", 4, hosts: ["sw-02", "sw-03"]),
            });

        var handler = new GetZabbixProblemsQueryHandler(client.Object);

        var result = await handler.Handle(
            new GetZabbixProblemsQuery(MinSeverity: 3), CancellationToken.None);

        result.Total.Should().Be(1);
        result.Items.Single().HostNames.Should().Equal("sw-02", "sw-03");
    }

    [Fact]
    public async Task Problems_MinSeverityOutOfRange_ShouldThrow()
    {
        var handler = new GetZabbixProblemsQueryHandler(Mock.Of<IZabbixClient>());

        var act = async () => await handler.Handle(
            new GetZabbixProblemsQuery(MinSeverity: 6), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*entre 0*5*");
    }

    [Theory]
    [InlineData("", "2026-01-01", "2026-01-02", 10)]
    [InlineData("50001", "2026-01-02", "2026-01-01", 10)]
    [InlineData("50001", "2026-01-01", "2026-06-01", 10)]
    [InlineData("50001", "2026-01-01", "2026-01-02", 0)]
    [InlineData("50001", "2026-01-01", "2026-01-02", 100001)]
    public async Task History_InvalidArguments_ShouldThrow(
        string itemId, string from, string to, int limit)
    {
        var handler = new GetZabbixHistoryQueryHandler(Mock.Of<IZabbixClient>());

        var act = async () => await handler.Handle(
            new GetZabbixHistoryQuery(
                itemId, DateTime.Parse(from), DateTime.Parse(to), limit),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task History_ShouldMapValuesAndRespectLimits()
    {
        var client = new Mock<IZabbixClient>();
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(1);
        client.Setup(c => c.GetHistoryAsync(
                "50001", from, to, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ZabbixHistoryValueDto("50001", from.AddHours(1), "42"),
                new ZabbixHistoryValueDto("50001", from.AddHours(2), "43.5"),
            });

        var handler = new GetZabbixHistoryQueryHandler(client.Object);

        var result = await handler.Handle(
            new GetZabbixHistoryQuery("50001", from, to, 100),
            CancellationToken.None);

        result.Should().HaveCount(2);
        result[1].Value.Should().Be("43.5");
        result[0].ClockUtc.Should().Be(from.AddHours(1));
    }

    [Fact]
    public async Task Summary_ShouldAggregateCountsAssetsAndProblemsBySeverity()
    {
        var hostsRepository = new Mock<IZabbixHostsRepository>();
        hostsRepository.Setup(r => r.GetCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZabbixHostCounts(Total: 5, Enabled: 4, Missing: 1));
        hostsRepository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Host("sw-1", ip: "10.0.0.1"),
                Host("sw-2", ip: "10.0.0.2"),
                Host("sw-3", ip: null),
                Host("Zabbix server", ip: "127.0.0.1"),
                Host("sem-asset", ip: null),
            });

        var devices = new[]
        {
            Asset("sw-1", "10.0.0.1"),
            Asset("Zabbix server", null),
        };
        var deviceRepository = new Mock<IDeviceRepository>();
        deviceRepository.Setup(r => r.GetAllOrderedByNameAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(devices);

        var settingsRepository = new Mock<IZabbixSettingsRepository>();
        settingsRepository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ZabbixIntegrationSetting.Create(
                "http://zbx:8080", "admin", "enc"));

        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.IsConfigured).Returns(true);
        client.Setup(c => c.ListProblemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Problem("1", "A", 5, hosts: ["sw-1"]),
                Problem("2", "B", 4, hosts: ["sw-1", "sw-2"]),
                Problem("3", "C", 4, hosts: ["sw-2"]),
            });

        var handler = new GetZabbixSummaryQueryHandler(
            client.Object, hostsRepository.Object, settingsRepository.Object,
            deviceRepository.Object, NullLogger<GetZabbixSummaryQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetZabbixSummaryQuery(), CancellationToken.None);

        result.TotalHosts.Should().Be(5);
        result.EnabledHosts.Should().Be(4);
        result.MissingHosts.Should().Be(1);
        result.HostsWithAsset.Should().Be(2, "IP sw-1 + hostname Zabbix server");
        result.HostsWithoutAsset.Should().Be(3);
        result.ProblemsTotal.Should().Be(3);
        result.ProblemsBySeverity[5].Should().Be(1);
        result.ProblemsBySeverity[4].Should().Be(2);
        result.HostsWithProblems.Should().Be(2, "sw-1 e sw-2 (distintos)");
        result.ProblemsAvailable.Should().BeTrue();
        result.IsConfigured.Should().BeTrue();
        result.DetectedVersion.Should().BeNull();
    }

    [Fact]
    public async Task Summary_WhenProblemsFail_ShouldKeepCountsAndMarkUnavailable()
    {
        var hostsRepository = new Mock<IZabbixHostsRepository>();
        hostsRepository.Setup(r => r.GetCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZabbixHostCounts(2, 2, 0));
        hostsRepository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ZabbixHost>());

        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.IsConfigured).Returns(true);
        client.Setup(c => c.ListProblemsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("HTTP 500 do Zabbix"));

        var handler = new GetZabbixSummaryQueryHandler(
            client.Object, hostsRepository.Object,
            Mock.Of<IZabbixSettingsRepository>(),
            EmptyDevices(),
            NullLogger<GetZabbixSummaryQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetZabbixSummaryQuery(), CancellationToken.None);

        result.TotalHosts.Should().Be(2, "contagens locais sobrevivem à falha");
        result.ProblemsAvailable.Should().BeFalse();
        result.ProblemsError.Should().Be("Não foi possível consultar os problemas no Zabbix.");
        result.ProblemsTotal.Should().Be(0);
    }

    [Fact]
    public async Task Summary_WhenNotConfigured_ShouldReportNotConfigured()
    {
        var hostsRepository = new Mock<IZabbixHostsRepository>();
        hostsRepository.Setup(r => r.GetCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZabbixHostCounts(0, 0, 0));
        hostsRepository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ZabbixHost>());

        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.IsConfigured).Returns(false);

        var handler = new GetZabbixSummaryQueryHandler(
            client.Object, hostsRepository.Object,
            Mock.Of<IZabbixSettingsRepository>(),
            EmptyDevices(),
            NullLogger<GetZabbixSummaryQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetZabbixSummaryQuery(), CancellationToken.None);

        result.IsConfigured.Should().BeFalse();
        result.ProblemsAvailable.Should().BeFalse();
        result.ProblemsError.Should().Be("Integração com Zabbix não configurada.");
    }

    private static ZabbixHost Host(string host, string? ip) => ZabbixHost.Create(
        hostId: Guid.NewGuid().ToString("N")[..8],
        host, name: host, ip, port: 10051,
        enabled: true, discovered: false, [], [], DateTime.UtcNow);

    private static Device Asset(string name, string? ip)
        => Device.Create(
            Guid.NewGuid(), name, DeviceType.Switch, ip: ip);

    private static IDeviceRepository EmptyDevices()
    {
        var repository = new Mock<IDeviceRepository>();
        repository.Setup(r => r.GetAllOrderedByNameAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Device>());
        return repository.Object;
    }
}

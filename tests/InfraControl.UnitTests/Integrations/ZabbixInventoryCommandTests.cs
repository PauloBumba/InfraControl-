using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F27 Fase 3.1: sync de hosts Zabbix → read-model (idempotente por
/// hostid, ausente = marcado nunca apagado) e query da página Hosts
/// monitorados (busca + flag HasAsset por IP/hostname).
/// </summary>
public sealed class ZabbixInventoryCommandTests
{
    [Fact]
    public async Task Sync_ShouldCreateUpdateAndMarkMissingIdempotently()
    {
        var existing = ZabbixHost.Create(
            "10084", "zbx-velho", "Velho", "10.0.0.9", 10050,
            enabled: true, discovered: false, [], [], DateTime.UtcNow.AddDays(-1));
        var repository = new Mock<IZabbixHostsRepository>();
        var local = new List<ZabbixHost> { existing };
        repository.Setup(r => r.GetByHostIdAsync("10084", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repository.Setup(r => r.GetByHostIdAsync("20001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ZabbixHost?)null);
        repository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => local);
        repository.Setup(r => r.AddAsync(It.IsAny<ZabbixHost>(), It.IsAny<CancellationToken>()))
            .Callback<ZabbixHost, CancellationToken>((h, _) => local.Add(h))
            .Returns(Task.CompletedTask);

        var settings = ZabbixIntegrationSetting.Create("http://zbx:8080", "admin", "enc");
        var settingsRepository = new Mock<IZabbixSettingsRepository>();
        settingsRepository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("8.0.0");
        client.Setup(c => c.ListHostsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Host("10084", "zbx-novo", ip: "10.0.0.9", enabled: true,
                    groupIds: ["4"], templateIds: ["10001"]),
                Host("20001", "zbx-novo-02", ip: "10.0.0.10", enabled: false)
            });
        client.Setup(c => c.ListHostGroupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ZabbixHostGroupDto("4", "Linux servers") });
        client.Setup(c => c.ListTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ZabbixTemplateDto("10001", "Linux by Zabbix agent") });

        var handler = new SyncZabbixInventoryCommandHandler(
            client.Object, repository.Object, settingsRepository.Object,
            NullLogger<SyncZabbixInventoryCommandHandler>.Instance);

        var result = await handler.Handle(
            new SyncZabbixInventoryCommand(), CancellationToken.None);

        result.Created.Should().Be(1);
        result.Updated.Should().Be(1);
        result.Missing.Should().Be(0);
        result.Errors.Should().Be(0);
        result.Version.Should().Be("8.0.0");

        existing.Host.Should().Be("zbx-novo");
        existing.Enabled.Should().BeTrue();
        existing.GroupsJson.Should().Contain("Linux servers");
        existing.TemplatesJson.Should().Contain("Linux by Zabbix agent");
        settings.LastInventorySyncAtUtc.Should().NotBeNull();
        settings.LastCommunicationAtUtc.Should().NotBeNull();
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sync_HostsAbsentFromZabbix_ShouldMarkMissingNeverDelete()
    {
        var orphan = ZabbixHost.Create(
            "99999", "sumiu", "Sumiu", null, null,
            true, false, [], [], DateTime.UtcNow.AddDays(-2));
        orphan.MarkMissing();
        var repository = new Mock<IZabbixHostsRepository>();
        repository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { orphan });

        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("8.0.0");
        client.Setup(c => c.ListHostsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ZabbixHostDto>());
        client.Setup(c => c.ListHostGroupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ZabbixHostGroupDto>());
        client.Setup(c => c.ListTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ZabbixTemplateDto>());

        var handler = new SyncZabbixInventoryCommandHandler(
            client.Object, repository.Object,
            Mock.Of<IZabbixSettingsRepository>(),
            NullLogger<SyncZabbixInventoryCommandHandler>.Instance);

        var result = await handler.Handle(
            new SyncZabbixInventoryCommand(), CancellationToken.None);

        result.Missing.Should().Be(1);
        orphan.MissingSinceUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Sync_PerHostError_ShouldCountAndContinue()
    {
        var repository = new Mock<IZabbixHostsRepository>();
        repository.Setup(r => r.GetByHostIdAsync("bad", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("hostid inválido"));
        repository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ZabbixHost>());

        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("8.0.0");
        client.Setup(c => c.ListHostsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Host("bad", "host-ruim"),
                Host("good", "host-bom")
            });
        client.Setup(c => c.ListHostGroupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ZabbixHostGroupDto>());
        client.Setup(c => c.ListTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ZabbixTemplateDto>());

        var handler = new SyncZabbixInventoryCommandHandler(
            client.Object, repository.Object,
            Mock.Of<IZabbixSettingsRepository>(),
            NullLogger<SyncZabbixInventoryCommandHandler>.Instance);

        var result = await handler.Handle(
            new SyncZabbixInventoryCommand(), CancellationToken.None);

        result.Errors.Should().Be(1);
        result.Created.Should().Be(1, "o sync deve continuar após erro de um host");
    }

    [Fact]
    public async Task GetHosts_ShouldFlagHasAssetByIp()
    {
        var host = ZabbixHost.Create(
            "10084", "sw-core", "Switch Core", "192.0.2.10", 10050,
            true, false, ["Switches"], ["Cisco IOS"], DateTime.UtcNow);
        var repository = new Mock<IZabbixHostsRepository>();
        repository.Setup(r => r.SearchAsync(
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<bool?>(),
                1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ZabbixHost>(
                [host], 1, 1, 20));
        var devices = new Mock<IDeviceRepository>();
        devices.Setup(d => d.GetByIpAsync("192.0.2.10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeviceStub.Create("sw-core", "192.0.2.10"));

        var handler = new GetZabbixHostsQueryHandler(repository.Object, devices.Object);
        var page = await handler.Handle(new GetZabbixHostsQuery(), CancellationToken.None);

        page.TotalCount.Should().Be(1);
        var view = page.Items.Single();
        view.HasAsset.Should().BeTrue();
        view.Groups.Should().ContainSingle().Which.Should().Be("Switches");
        view.Templates.Should().ContainSingle().Which.Should().Be("Cisco IOS");
        view.Missing.Should().BeFalse();
    }

    [Fact]
    public async Task GetHosts_ShouldFallbackAssetMatchByHostname()
    {
        var host = ZabbixHost.Create(
            "10084", "srv-web-01", "Servidor Web", null, null,
            true, false, [], [], DateTime.UtcNow);
        var repository = new Mock<IZabbixHostsRepository>();
        repository.Setup(r => r.SearchAsync(
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<bool?>(),
                1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ZabbixHost>([host], 1, 1, 20));
        var devices = new Mock<IDeviceRepository>();
        devices.Setup(d => d.GetByNameAsync("srv-web-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeviceStub.Create("srv-web-01", null));

        var handler = new GetZabbixHostsQueryHandler(repository.Object, devices.Object);
        var page = await handler.Handle(new GetZabbixHostsQuery(), CancellationToken.None);

        page.Items.Single().HasAsset.Should().BeTrue();
    }

    [Fact]
    public async Task GetHosts_WithoutAsset_ShouldFlagFalse()
    {
        var host = ZabbixHost.Create(
            "10084", "huerfano", "Sem Asset", "192.0.2.99", null,
            true, false, [], [], DateTime.UtcNow);
        var repository = new Mock<IZabbixHostsRepository>();
        repository.Setup(r => r.SearchAsync(
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<bool?>(),
                1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ZabbixHost>([host], 1, 1, 20));

        var handler = new GetZabbixHostsQueryHandler(
            repository.Object, Mock.Of<IDeviceRepository>());
        var page = await handler.Handle(new GetZabbixHostsQuery(), CancellationToken.None);

        page.Items.Single().HasAsset.Should().BeFalse();
    }

    // ── Infra ───────────────────────────────────────────────────────

    private static ZabbixHostDto Host(
        string hostId, string host, string? ip = null, bool enabled = true,
        IReadOnlyList<string>? groupIds = null,
        IReadOnlyList<string>? templateIds = null) =>
        new(
            hostId, host, host, enabled, Discovered: false,
            Interfaces: ip is null
                ? []
                : [new ZabbixInterfaceDto(ip, 10050, Type: 1, Main: true)],
            GroupIds: groupIds ?? [], TemplateIds: templateIds ?? []);

    /// <summary>Stub mínimo de Device para o mock do IDeviceRepository.</summary>
    private static class DeviceStub
    {
        public static Device Create(string name, string? ip) =>
            Device.Create(
                Guid.NewGuid(), name, Domain.Enums.DeviceType.Server, ip: ip);
    }
}

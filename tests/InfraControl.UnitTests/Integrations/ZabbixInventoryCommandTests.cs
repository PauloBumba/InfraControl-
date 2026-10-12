using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
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
            Mock.Of<IDeviceRepository>(),
            Mock.Of<IExternalDeviceReferenceRepository>(),
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
    public async Task Sync_WhenAnotherSyncInProgress_ShouldThrowConflictAndReleaseGateAfter()
    {
        // F27 Fase 11.1: um sync por processo — a segunda execução
        // simultânea recebe InvalidOperationException (409 no API) e o
        // gate é liberado no finally (um 3º sync volta a funcionar).
        var gateVersion = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.GetVersionAsync(It.IsAny<CancellationToken>()))
            .Returns(() => gateVersion.Task);
        client.Setup(c => c.ListHostsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ZabbixHostDto>());
        client.Setup(c => c.ListHostGroupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ZabbixHostGroupDto>());
        client.Setup(c => c.ListTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ZabbixTemplateDto>());

        var repository = new Mock<IZabbixHostsRepository>();
        repository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ZabbixHost>());
        var settingsRepository = new Mock<IZabbixSettingsRepository>();
        settingsRepository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ZabbixIntegrationSetting?)null);

        var handler = new SyncZabbixInventoryCommandHandler(
            client.Object, repository.Object, settingsRepository.Object,
            Mock.Of<IDeviceRepository>(),
            Mock.Of<IExternalDeviceReferenceRepository>(),
            NullLogger<SyncZabbixInventoryCommandHandler>.Instance);

        var first = handler.Handle(
            new SyncZabbixInventoryCommand(), CancellationToken.None);

        var second = async () => await handler.Handle(
            new SyncZabbixInventoryCommand(), CancellationToken.None);
        await second.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já está em andamento*");

        gateVersion.SetResult("8.0.0");
        var result = await first;
        result.Version.Should().Be("8.0.0");

        // gate liberado — novo sync passa normalmente
        var third = await handler.Handle(
            new SyncZabbixInventoryCommand(), CancellationToken.None);
        third.Version.Should().Be("8.0.0");
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
            Mock.Of<IDeviceRepository>(),
            Mock.Of<IExternalDeviceReferenceRepository>(),
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
            Mock.Of<IDeviceRepository>(),
            Mock.Of<IExternalDeviceReferenceRepository>(),
            NullLogger<SyncZabbixInventoryCommandHandler>.Instance);

        var result = await handler.Handle(
            new SyncZabbixInventoryCommand(), CancellationToken.None);

        result.Errors.Should().Be(1);
        result.Created.Should().Be(1, "o sync deve continuar após erro de um host");
    }

    // ── F29-1: correlação host↔device → ExternalDeviceReference ──────

    [Fact]
    public async Task Sync_WhenHostIpMatchesDevice_ShouldWriteStableReference()
    {
        var device = Device.Create(
            Guid.NewGuid(), "sw-core", Domain.Enums.DeviceType.Switch, ip: "192.0.2.10");
        var devices = new Mock<IDeviceRepository>();
        devices.Setup(d => d.GetByIpAsync("192.0.2.10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        var references = new Mock<IExternalDeviceReferenceRepository>();
        references.Setup(r => r.GetBySourceAndExternalIdAsync(
                ExternalDeviceSource.Zabbix, "10084", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalDeviceReference?)null);

        var handler = BuildSyncHandler(
            hosts:
            [
                Host("10084", "sw-core", ip: "192.0.2.10")
            ],
            devices: devices, references: references);

        await handler.Handle(new SyncZabbixInventoryCommand(), CancellationToken.None);

        references.Verify(r => r.AddAsync(
            It.Is<ExternalDeviceReference>(x =>
                x.Source == ExternalDeviceSource.Zabbix
                && x.ExternalId == "10084"
                && x.DeviceId == device.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sync_WhenReferenceAlreadyPointsToSameDevice_ShouldNotDuplicate()
    {
        var device = Device.Create(
            Guid.NewGuid(), "sw-core", Domain.Enums.DeviceType.Switch, ip: "192.0.2.10");
        var existingRef = ExternalDeviceReference.Create(
            ExternalDeviceSource.Zabbix, "10084", device.Id);
        var references = new Mock<IExternalDeviceReferenceRepository>();
        references.Setup(r => r.GetBySourceAndExternalIdAsync(
                ExternalDeviceSource.Zabbix, "10084", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRef);
        var devices = new Mock<IDeviceRepository>();
        devices.Setup(d => d.GetByIpAsync("192.0.2.10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        var handler = BuildSyncHandler(
            hosts: [Host("10084", "sw-core", ip: "192.0.2.10")],
            devices: devices, references: references);

        await handler.Handle(new SyncZabbixInventoryCommand(), CancellationToken.None);

        references.Verify(r => r.AddAsync(
            It.IsAny<ExternalDeviceReference>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Sync_WhenReferencePointsToDifferentDevice_ShouldKeepExisting()
    {
        var otherDeviceId = Guid.NewGuid();
        var matchedDevice = Device.Create(
            Guid.NewGuid(), "sw-core", Domain.Enums.DeviceType.Switch, ip: "192.0.2.10");
        var existingRef = ExternalDeviceReference.Create(
            ExternalDeviceSource.Zabbix, "10084", otherDeviceId);
        var references = new Mock<IExternalDeviceReferenceRepository>();
        references.Setup(r => r.GetBySourceAndExternalIdAsync(
                ExternalDeviceSource.Zabbix, "10084", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRef);
        var devices = new Mock<IDeviceRepository>();
        devices.Setup(d => d.GetByIpAsync("192.0.2.10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(matchedDevice);

        var handler = BuildSyncHandler(
            hosts: [Host("10084", "sw-core", ip: "192.0.2.10")],
            devices: devices, references: references);

        var result = await handler.Handle(
            new SyncZabbixInventoryCommand(), CancellationToken.None);

        // mantém a existente (não reassigna em silêncio) e o sync não falha
        references.Verify(r => r.AddAsync(
            It.IsAny<ExternalDeviceReference>(), It.IsAny<CancellationToken>()),
            Times.Never);
        result.Errors.Should().Be(0);
    }

    [Fact]
    public async Task Sync_WithoutMatchingDevice_ShouldNotWriteReference()
    {
        var devices = new Mock<IDeviceRepository>();
        devices.Setup(d => d.GetByIpAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Device?)null);
        devices.Setup(d => d.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Device?)null);
        var references = new Mock<IExternalDeviceReferenceRepository>();

        var handler = BuildSyncHandler(
            hosts: [Host("10084", "huerfano", ip: "192.0.2.99")],
            devices: devices, references: references);

        var result = await handler.Handle(
            new SyncZabbixInventoryCommand(), CancellationToken.None);

        references.Verify(r => r.AddAsync(
            It.IsAny<ExternalDeviceReference>(), It.IsAny<CancellationToken>()),
            Times.Never);
        result.Errors.Should().Be(0);
    }

    [Fact]
    public async Task Sync_ShouldFallbackReferenceMatchByHostnameWhenNoIp()
    {
        var device = Device.Create(
            Guid.NewGuid(), "srv-web-01", Domain.Enums.DeviceType.Server, ip: null);
        var devices = new Mock<IDeviceRepository>();
        devices.Setup(d => d.GetByNameAsync("srv-web-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);
        var references = new Mock<IExternalDeviceReferenceRepository>();
        references.Setup(r => r.GetBySourceAndExternalIdAsync(
                ExternalDeviceSource.Zabbix, "10084", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalDeviceReference?)null);

        var handler = BuildSyncHandler(
            hosts: [Host("10084", "srv-web-01", ip: null)],
            devices: devices, references: references);

        await handler.Handle(new SyncZabbixInventoryCommand(), CancellationToken.None);

        references.Verify(r => r.AddAsync(
            It.Is<ExternalDeviceReference>(x =>
                x.DeviceId == device.Id), It.IsAny<CancellationToken>()), Times.Once);
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

        var handler = new GetZabbixHostsQueryHandler(
            repository.Object, devices.Object,
            Mock.Of<IExternalDeviceReferenceRepository>());
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

        var handler = new GetZabbixHostsQueryHandler(
            repository.Object, devices.Object,
            Mock.Of<IExternalDeviceReferenceRepository>());
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
            repository.Object, Mock.Of<IDeviceRepository>(),
            Mock.Of<IExternalDeviceReferenceRepository>());
        var page = await handler.Handle(new GetZabbixHostsQuery(), CancellationToken.None);

        page.Items.Single().HasAsset.Should().BeFalse();
    }

    [Fact]
    public async Task GetHosts_ShouldPreferStableReferenceOverIpHeuristic()
    {
        // F29-1: a referência hostid→device tem precedência — HasAsset é
        // true mesmo quando o IP não bate com nenhum device.
        var host = ZabbixHost.Create(
            "10084", "sw-core", "Switch Core", "192.0.2.10", 10050,
            true, false, [], [], DateTime.UtcNow);
        var repository = new Mock<IZabbixHostsRepository>();
        repository.Setup(r => r.SearchAsync(
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<bool?>(),
                1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ZabbixHost>([host], 1, 1, 20));

        var references = new Mock<IExternalDeviceReferenceRepository>();
        references.Setup(r => r.GetDeviceIdAsync(
                ExternalDeviceSource.Zabbix, "10084", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        // deviceRepository NÃO bate por IP nem hostname — só a referência resolve
        var handler = new GetZabbixHostsQueryHandler(
            repository.Object, Mock.Of<IDeviceRepository>(), references.Object);
        var page = await handler.Handle(new GetZabbixHostsQuery(), CancellationToken.None);

        page.Items.Single().HasAsset.Should().BeTrue();
    }

    // ── Infra ───────────────────────────────────────────────────────

    /// <summary>
    /// Monta o SyncZabbixInventoryCommandHandler com cliente devolvendo os
    /// hosts dados e repositórios de device/referência injetáveis (F29-1).
    /// </summary>
    private static SyncZabbixInventoryCommandHandler BuildSyncHandler(
        IReadOnlyList<ZabbixHostDto> hosts,
        Mock<IDeviceRepository>? devices = null,
        Mock<IExternalDeviceReferenceRepository>? references = null)
    {
        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("8.0.0");
        client.Setup(c => c.ListHostsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(hosts);
        client.Setup(c => c.ListHostGroupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ZabbixHostGroupDto>());
        client.Setup(c => c.ListTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ZabbixTemplateDto>());

        var repository = new Mock<IZabbixHostsRepository>();
        repository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ZabbixHost>());
        repository.Setup(r => r.GetByHostIdAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ZabbixHost?)null);

        return new SyncZabbixInventoryCommandHandler(
            client.Object, repository.Object,
            Mock.Of<IZabbixSettingsRepository>(),
            (devices ?? new Mock<IDeviceRepository>()).Object,
            (references ?? new Mock<IExternalDeviceReferenceRepository>()).Object,
            NullLogger<SyncZabbixInventoryCommandHandler>.Instance);
    }

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

using FluentAssertions;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F20-T001/ADR-028 §4: sync idempotente do cadastro UniFi — upsert de
/// Device + ExternalDeviceReference (Source=UniFi, ExternalId = MAC lowercase
/// SEM separadores, decisão §7), location "UniFi" auto-criada, gateway →
/// DeviceType.Firewall e ZERO telemetria persistida (regra 8).
/// </summary>
public sealed class SyncUnifiInventoryCommandTests
{
    private readonly Mock<IUnifiApiClient> _client = new();
    private readonly Mock<IUniFiSitesRepository> _sites = new();
    private readonly Mock<IDeviceRepository> _devices = new();
    private readonly Mock<IExternalDeviceReferenceRepository> _references = new();
    private readonly Mock<InfraControl.Application.Integrations.Unifi.Ports.IUniFiSyncRunsRepository> _syncRuns = new();
    private readonly Mock<ILocationRepository> _locations = new();

    private Device? _addedDevice;
    private readonly List<Device> _addedDevices = [];
    private Location? _addedLocation;

    [Fact]
    public async Task Handle_DeviceNew_ShouldCreateDeviceAndReferenceInAutoCreatedUniFiLocation()
    {
        ArrangeClient(new UnifiDeviceDto(
            "AA:BB:CC:DD:EE:01", "gw-01", "UDM-Pro", "udm", "192.168.1.1"));
        _locations.Setup(GetUniFiLocation).ReturnsAsync((Location?)null);
        _devices.Setup(GetByMac).ReturnsAsync((Device?)null);
        _references.Setup(GetReference).ReturnsAsync((Guid?)null);

        var result = await Handler().Handle(new SyncUnifiInventoryCommand(), default);

        result.Should().Be(new UnifiSyncResultDto(Created: 1, Updated: 0, References: 1, Errors: 0));
        _addedLocation.Should().NotBeNull("a location UniFi deve ser criada automaticamente");
        _addedLocation!.Name.Should().Be("UniFi");
        _addedDevice.Should().NotBeNull();
        _addedDevice!.Type.Should().Be(DeviceType.Firewall, "udm = gateway → Firewall");
        _addedDevice.LocationId.Should().Be(_addedLocation.Id);
        _addedDevice.Name.Should().Be("gw-01");
        _addedDevice.Ip.Should().Be("192.168.1.1");
        _addedDevice.Model.Should().Be("UDM-Pro");

        _references.Verify(r => r.AddAsync(
            It.Is<ExternalDeviceReference>(x =>
                x.Source == ExternalDeviceSource.UniFi &&
                x.ExternalId == "aabbccddee01" &&
                x.DeviceId == _addedDevice.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SecondSync_ShouldUpdateInsteadOfCreatingAgain()
    {
        var dto = new UnifiDeviceDto("AA:BB:CC:DD:EE:01", "gw-01-v2", "UDM-Pro", "udm", "192.168.1.10");
        ArrangeClient(dto);
        _locations.Setup(GetUniFiLocation).ReturnsAsync(new LocationFixture().Location);
        var device = Device.Create(Guid.NewGuid(), "gw-01", DeviceType.Firewall, ip: "192.168.1.1");
        _devices.Setup(GetByMac).ReturnsAsync(device);
        _references.Setup(GetReference).ReturnsAsync(device.Id);

        var result = await Handler().Handle(new SyncUnifiInventoryCommand(), default);

        result.Should().Be(new UnifiSyncResultDto(0, 1, 0, 0));
        device.Name.Should().Be("gw-01-v2", "cadastro do Controller vence no sync");
        device.Ip.Should().Be("192.168.1.10");
        _devices.Verify(d => d.AddAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()), Times.Never);
        _references.Verify(r => r.AddAsync(It.IsAny<ExternalDeviceReference>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldMapUniFiTypeToDeviceType()
    {
        ArrangeClient(
            new UnifiDeviceDto("aa:bb:cc:dd:ee:01", "gw", "UXG", "uxg", "10.0.0.1"),
            new UnifiDeviceDto("aa:bb:cc:dd:ee:02", "sw", "USW", "usw", "10.0.0.2"),
            new UnifiDeviceDto("aa:bb:cc:dd:ee:03", "ap", "U6", "uap", "10.0.0.3"),
            new UnifiDeviceDto("aa:bb:cc:dd:ee:04", "x", "X", "unknown-model", "10.0.0.4"));
        _locations.Setup(GetUniFiLocation).ReturnsAsync(new LocationFixture().Location);
        _devices.Setup(GetByMac).ReturnsAsync((Device?)null);
        _references.Setup(GetReference).ReturnsAsync((Guid?)null);

        await Handler().Handle(new SyncUnifiInventoryCommand(), default);

        var types = CaptureAddedDevices().Select(d => d.Type).ToArray();
        types.Should().Equal(
            DeviceType.Firewall, DeviceType.Switch, DeviceType.AccessPoint, DeviceType.Other);
    }

    [Fact]
    public async Task Handle_MacWithSeparators_ShouldStoreReferenceWithoutSeparators()
    {
        ArrangeClient(new UnifiDeviceDto(
            "AA-BB-CC-DD-EE-02", "sw-01", "USW", "usw", "192.168.1.2"));
        _locations.Setup(GetUniFiLocation).ReturnsAsync(new LocationFixture().Location);
        _devices.Setup(GetByMac).ReturnsAsync((Device?)null);
        _references.Setup(GetReference).ReturnsAsync((Guid?)null);

        await Handler().Handle(new SyncUnifiInventoryCommand(), default);

        // decisão ADR-028 §7: ExternalId = lowercase SEM separadores
        _references.Verify(r => r.AddAsync(
            It.Is<ExternalDeviceReference>(x => x.ExternalId == "aabbccddee02"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidMac_ShouldCountErrorAndStillProcessTheNext()
    {
        ArrangeClient(
            new UnifiDeviceDto("sem-mac", "bad", "X", "usw", "10.0.0.9"),
            new UnifiDeviceDto("aa:bb:cc:dd:ee:05", "ok", "USW", "usw", "10.0.0.5"));
        _locations.Setup(GetUniFiLocation).ReturnsAsync(new LocationFixture().Location);
        _devices.Setup(GetByMac).ReturnsAsync((Device?)null);
        _references.Setup(GetReference).ReturnsAsync((Guid?)null);

        var result = await Handler().Handle(new SyncUnifiInventoryCommand(), default);

        result.Should().Be(new UnifiSyncResultDto(Created: 1, Updated: 0, References: 1, Errors: 1));
        CaptureAddedDevices().Should().ContainSingle(d => d.Name == "ok");
    }

    [Fact]
    public async Task Handle_ReferenceOwnedByAnotherDevice_ShouldCountErrorWithoutRelinking()
    {
        ArrangeClient(new UnifiDeviceDto("aa:bb:cc:dd:ee:06", "gw", "UDM", "udm", "10.0.0.6"));
        _locations.Setup(GetUniFiLocation).ReturnsAsync(new LocationFixture().Location);
        _devices.Setup(GetByMac).ReturnsAsync((Device?)null);
        _references.Setup(GetReference).ReturnsAsync(Guid.NewGuid());

        var result = await Handler().Handle(new SyncUnifiInventoryCommand(), default);

        result.Errors.Should().Be(1, "divergência de dono não é realocada automaticamente");
        result.References.Should().Be(0);
    }

    [Fact]
    public async Task Handle_EmptyInventory_ShouldNotCreateLocationAndReturnZeros()
    {
        ArrangeClient();
        _locations.Setup(GetUniFiLocation).ReturnsAsync((Location?)null);

        var result = await Handler().Handle(new SyncUnifiInventoryCommand(), default);

        result.Should().Be(new UnifiSyncResultDto(0, 0, 0, 0));
        _locations.Verify(l => l.AddAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenClientNotConfigured_ShouldPropagatePtBr()
    {
        _client.Setup(c => c.ListDevicesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Integração UniFi não configurada."));

        var send = () => Handler().Handle(new SyncUnifiInventoryCommand(), default);

        (await send.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*não configurada*");
        _devices.Verify(d => d.AddAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Infra ───────────────────────────────────────────────────────

    private SyncUnifiInventoryCommandHandler Handler()
    {
        _devices.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _references.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _locations.Setup(l => l.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _devices.Setup(d => d.AddAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()))
            .Callback<Device, CancellationToken>((d, _) =>
            {
                _addedDevice ??= d;
                _addedDevices.Add(d);
            });
        _locations.Setup(l => l.AddAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .Callback<Location, CancellationToken>((l, _) => _addedLocation = l);

        return new SyncUnifiInventoryCommandHandler(
            _client.Object,
            _devices.Object,
            _references.Object,
            _locations.Object,
            _sites.Object,
            _syncRuns.Object,
            NullLogger<SyncUnifiInventoryCommandHandler>.Instance);
    }


    [Fact]
    public async Task Sync_sites_cria_novos_e_conta_no_resultado()
    {
        ArrangeClient();
        _client.Setup(c => c.ListSitesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UnifiSiteDto("abc123", "default", "Site principal", false)
            });

        var handler = Handler();

        var result = await handler.Handle(new SyncUnifiInventoryCommand(), CancellationToken.None);

        result.Sites.Should().Be(1);
        _sites.Verify(r => r.AddAsync(
            It.Is<UniFiSite>(s => s.ShortId == "abc123" && s.Name == "default"),
            It.IsAny<CancellationToken>()), Times.Once);
        _sites.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sync_sites_marca_como_missing_o_site_que_sumiu_do_controller()
    {
        ArrangeClient();
        var local = UniFiSite.Create("velho", "Antigo", string.Empty, false);
        _sites.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { local });

        var handler = Handler();

        var result = await handler.Handle(new SyncUnifiInventoryCommand(), CancellationToken.None);

        result.Sites.Should().Be(0);
        local.MissingSinceUtc.Should().NotBeNull();
        _sites.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private void ArrangeClient(params UnifiDeviceDto[] devices)
    {
        _client.Setup(c => c.ListDevicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(devices);
        _client.Setup(c => c.ListSitesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UnifiSiteDto>());
        _sites.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UniFiSite>());
        _sites.Setup(r => r.GetByShortIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UniFiSite?)null);
        _sites.Setup(r => r.AddAsync(It.IsAny<UniFiSite>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sites.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private List<Device> CaptureAddedDevices() => _addedDevices;

    private static System.Linq.Expressions.Expression<
        Func<ILocationRepository, Task<Location?>>> GetUniFiLocation
        => l => l.GetByNameAsync("UniFi", It.IsAny<CancellationToken>());

    private static System.Linq.Expressions.Expression<
        Func<IDeviceRepository, Task<Device?>>> GetByMac
        => d => d.GetByMacAsync(It.IsAny<string>(), It.IsAny<CancellationToken>());

    private static System.Linq.Expressions.Expression<
        Func<IExternalDeviceReferenceRepository, Task<Guid?>>> GetReference
        => r => r.GetDeviceIdAsync(
            ExternalDeviceSource.UniFi, It.IsAny<string>(), It.IsAny<CancellationToken>());

    private sealed class LocationFixture
    {
        public Location Location { get; } = Location.Create("UniFi");
    }
}

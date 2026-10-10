using FluentAssertions;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Moq;
using Xunit;

namespace InfraControl.UnitTests.Integrations;

/// <summary>F26 Fase 7: correlação UniFi↔inventário (views + vínculo manual).</summary>
public sealed class UniFiCorrelationCommandsTests
{
    private readonly Mock<IExternalDeviceReferenceRepository> _references = new();
    private readonly Mock<IDeviceRepository> _devices = new();
    private readonly Mock<IUnifiApiClient> _unifi = new();

    [Fact]
    public async Task GetUniFiCorrelation_Separa_Matched_Unlinked_E_Conflitos()
    {
        var deviceOk = Device.Create(Guid.NewGuid(), "sw-01", DeviceType.Switch,
            ip: "10.0.0.2", mac: "aa:bb:cc:00:00:02");
        var deviceConflict = Device.Create(Guid.NewGuid(), "ap-antigo", DeviceType.AccessPoint,
            ip: "10.0.0.3", mac: "de:ad:be:ef:00:01");

        _references.Setup(r => r.ListBySourceAsync(ExternalDeviceSource.UniFi, default))
            .ReturnsAsync(new List<ExternalDeviceReference>
            {
                ExternalDeviceReference.Create(ExternalDeviceSource.UniFi, "aabbcc000002", deviceOk.Id),
                ExternalDeviceReference.Create(ExternalDeviceSource.UniFi, "d021f93c5950", deviceConflict.Id),
            });
        _devices.Setup(d => d.GetAllOrderedByNameAsync(default))
            .ReturnsAsync(new List<Device> { deviceOk, deviceConflict });
        _unifi.Setup(u => u.ListDevicesAsync(default)).ReturnsAsync(new List<UnifiDeviceDto>
        {
            new("aa:bb:cc:00:00:02", "sw-01", "USW", "usw", "10.0.0.2"),
            new("d0:21:f9:3c:59:50", "ac-pro", "U7PG2", "uap", "10.0.0.9"),
            new("aa:bb:cc:00:00:77", "orfaos", "U7PG2", "uap", "10.0.0.77"),
        });

        var handler = new GetUniFiCorrelationQueryHandler(
            _references.Object, _devices.Object, _unifi.Object);
        var result = await handler.Handle(new GetUniFiCorrelationQuery(), default);

        result.Matched.Should().ContainSingle(m => m.Mac == "aabbcc000002" && m.ControllerPresent);
        result.Conflicts.Should().ContainSingle(c => c.Mac == "d021f93c5950");
        result.Unlinked.Should().ContainSingle(u => u.Mac == "aa:bb:cc:00:00:77" || u.Mac == "aabbcc000077");
    }

    [Fact]
    public async Task CreateUniFiAssetLink_CriaVinculo_QuandoMacLivreEDeviceSemMac()
    {
        var device = Device.Create(Guid.NewGuid(), "ap-novo", DeviceType.AccessPoint);
        _references.Setup(r => r.GetDeviceIdAsync(ExternalDeviceSource.UniFi, "d021f93c5950", default))
            .ReturnsAsync((Guid?)null);
        _devices.Setup(d => d.GetByIdAsync(device.Id, default)).ReturnsAsync(device);
        _devices.Setup(d => d.GetByMacAsync("d0:21:f9:3c:59:50", default))
            .ReturnsAsync((Device?)null);

        var handler = new CreateUniFiAssetLinkCommandHandler(_references.Object, _devices.Object);
        var link = await handler.Handle(
            new CreateUniFiAssetLinkCommand("D0:21:F9:3C:59:50", device.Id), default);

        link.Mac.Should().Be("d021f93c5950");
        link.DeviceId.Should().Be(device.Id);
        _references.Verify(r => r.AddAsync(
            It.Is<ExternalDeviceReference>(x => x.ExternalId == "d021f93c5950"), default), Times.Once);
        _references.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task CreateUniFiAssetLink_MacJaVinculado_LancaInvalidOperation()
    {
        _references.Setup(r => r.GetDeviceIdAsync(ExternalDeviceSource.UniFi, "d021f93c5950", default))
            .ReturnsAsync(Guid.NewGuid());

        var handler = new CreateUniFiAssetLinkCommandHandler(_references.Object, _devices.Object);
        var act = () => handler.Handle(
            new CreateUniFiAssetLinkCommand("d0:21:f9:3c:59:50", Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _references.Verify(r => r.AddAsync(It.IsAny<ExternalDeviceReference>(), default), Times.Never);
    }

    [Fact]
    public async Task CreateUniFiAssetLink_DeviceComOutroMac_LancaInvalidOperation()
    {
        var device = Device.Create(Guid.NewGuid(), "ap", DeviceType.AccessPoint,
            mac: "de:ad:be:ef:00:01");
        _references.Setup(r => r.GetDeviceIdAsync(ExternalDeviceSource.UniFi, "d021f93c5950", default))
            .ReturnsAsync((Guid?)null);
        _devices.Setup(d => d.GetByIdAsync(device.Id, default)).ReturnsAsync(device);

        var handler = new CreateUniFiAssetLinkCommandHandler(_references.Object, _devices.Object);
        var act = () => handler.Handle(
            new CreateUniFiAssetLinkCommand("d0:21:f9:3c:59:50", device.Id), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*corrija o cadastro*");
    }

    [Fact]
    public async Task DeleteUniFiAssetLink_RemoveApenasAReferencia()
    {
        var reference = ExternalDeviceReference.Create(
            ExternalDeviceSource.UniFi, "d021f93c5950", Guid.NewGuid());
        _references.Setup(r => r.GetBySourceAndExternalIdAsync(
                ExternalDeviceSource.UniFi, "D0:21:F9:3C:59:50", default))
            .ReturnsAsync(reference);

        var handler = new DeleteUniFiAssetLinkCommandHandler(_references.Object);
        await handler.Handle(new DeleteUniFiAssetLinkCommand("D0:21:F9:3C:59:50"), default);

        _references.Verify(r => r.Remove(reference), Times.Once);
        _references.Verify(r => r.SaveChangesAsync(default), Times.Once);
        _devices.Verify(
            d => d.Remove(It.IsAny<Device>()), Times.Never);
    }

    [Fact]
    public async Task DeleteUniFiAssetLink_SemVinculo_LancaKeyNotFound()
    {
        _references.Setup(r => r.GetBySourceAndExternalIdAsync(
                ExternalDeviceSource.UniFi, "d021f93c5950", default))
            .ReturnsAsync((ExternalDeviceReference?)null);

        var handler = new DeleteUniFiAssetLinkCommandHandler(_references.Object);
        var act = () => handler.Handle(new DeleteUniFiAssetLinkCommand("d021f93c5950"), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}

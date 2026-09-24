using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F2-T003: invariantes da entidade Device — mensagens nascem em pt-BR no domínio.
/// </summary>
public sealed class DeviceTests
{
    private static readonly Guid SomeLocationId = Guid.NewGuid();
    private static readonly Guid SomeRackId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldSetFieldsTrimAndNormalize()
    {
        var device = Device.Create(
            SomeLocationId,
            "  sw-core-01  ",
            DeviceType.Switch,
            rackId: SomeRackId,
            ip: " 192.0.2.10 ",
            mac: "AA-BB-CC-DD-EE-FF",
            serial: "  SN-001  ",
            model: "  S5560X  ",
            vendor: "  H3C  ",
            vlanId: null,
            description: "  Núcleo  ");

        device.LocationId.Should().Be(SomeLocationId);
        device.RackId.Should().Be(SomeRackId);
        device.Type.Should().Be(DeviceType.Switch);
        device.Name.Should().Be("sw-core-01");
        device.Ip.Should().Be("192.0.2.10");
        device.Mac.Should().Be("aa:bb:cc:dd:ee:ff");
        device.Serial.Should().Be("SN-001");
        device.Model.Should().Be("S5560X");
        device.Vendor.Should().Be("H3C");
        device.Description.Should().Be("Núcleo");
    }

    [Fact]
    public void Create_ShouldGenerateIdAndTimestamps()
    {
        var device = Device.Create(SomeLocationId, "pc-01", DeviceType.Workstation);

        device.Id.Should().NotBe(Guid.Empty);
        device.CreateDateTime.Should().NotBe(default(DateTime));
        device.UpdateDateTime.Should().NotBe(default(DateTime));
    }

    [Fact]
    public void Create_WithoutLocationId_ShouldThrowPtBrMessage()
    {
        var action = () => Device.Create(Guid.Empty, "sw-01", DeviceType.Switch);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id do local é obrigatório.*");
    }

    [Fact]
    public void Create_WithoutName_ShouldThrowPtBrMessage()
    {
        var action = () => Device.Create(SomeLocationId, "   ", DeviceType.Switch);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O nome do device é obrigatório.*");
    }

    [Fact]
    public void Create_WithTooLongName_ShouldThrowPtBrMessage()
    {
        var action = () => Device.Create(
            SomeLocationId, new string('a', Device.NameMaxLength + 1), DeviceType.Server);

        action.Should().Throw<ArgumentException>()
            .WithMessage($"O nome do device pode ter no máximo {Device.NameMaxLength} caracteres.*");
    }

    [Fact]
    public void Create_WithInvalidIp_ShouldThrowPtBrMessage()
    {
        var action = () => Device.Create(
            SomeLocationId, "sw-01", DeviceType.Switch, ip: "nao-e-ip");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O IP informado é inválido.*");
    }

    [Fact]
    public void Create_WithInvalidMac_ShouldThrowPtBrMessage()
    {
        var action = () => Device.Create(
            SomeLocationId, "sw-01", DeviceType.Switch, mac: "not-a-mac");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O MAC informado é inválido.*");
    }

    [Fact]
    public void Create_WithOptionalBlanks_ShouldStoreNull()
    {
        var device = Device.Create(
            SomeLocationId, "cam-01", DeviceType.Camera,
            ip: "  ", mac: null, serial: "   ", model: "", vendor: null, description: "  ");

        device.Ip.Should().BeNull();
        device.Mac.Should().BeNull();
        device.Serial.Should().BeNull();
        device.Model.Should().BeNull();
        device.Vendor.Should().BeNull();
        device.Description.Should().BeNull();
        device.RackId.Should().BeNull();
    }

    [Fact]
    public void Create_WithTooLongSerial_ShouldThrowPtBrMessage()
    {
        var action = () => Device.Create(
            SomeLocationId, "ups-01", DeviceType.UPS,
            serial: new string('s', Device.SerialMaxLength + 1));

        action.Should().Throw<ArgumentException>()
            .WithMessage($"O serial do device pode ter no máximo {Device.SerialMaxLength} caracteres.*");
    }

    [Fact]
    public void Update_ShouldApplyNewFields()
    {
        var device = Device.Create(SomeLocationId, "prn-01", DeviceType.Printer);

        device.Update(
            "  prn-02  ",
            DeviceType.Printer,
            rackId: SomeRackId,
            ip: "192.0.2.20",
            mac: "aa:bb:cc:dd:ee:01",
            serial: "SN-9",
            model: "LJ-M404",
            vendor: "HP",
            vlanId: Guid.NewGuid(),
            description: "Recepção");

        device.Name.Should().Be("prn-02");
        device.RackId.Should().Be(SomeRackId);
        device.Ip.Should().Be("192.0.2.20");
        device.VlanId.Should().NotBeNull();
        device.Description.Should().Be("Recepção");
    }

    [Fact]
    public void Update_WithoutName_ShouldThrowAndKeepState()
    {
        var device = Device.Create(SomeLocationId, "sw-01", DeviceType.Switch);

        var action = () => device.Update("  ", DeviceType.Switch, null);

        action.Should().Throw<ArgumentException>();
        device.Name.Should().Be("sw-01", "falha de validação não pode alterar o agregado");
    }

    [Fact]
    public void Update_ShouldAllowChangeType()
    {
        var device = Device.Create(SomeLocationId, "gw-01", DeviceType.Other);

        device.Update("gw-01", DeviceType.Router, null);

        device.Type.Should().Be(DeviceType.Router);
    }
}

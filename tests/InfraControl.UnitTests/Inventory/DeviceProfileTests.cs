using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Profiles;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F2-T008 / ADR-014: defaults por tipo e materialização/override no Device.
/// </summary>
public sealed class DeviceProfileTests
{
    private static readonly Guid SomeLocationId = Guid.NewGuid();

    [Theory]
    [InlineData(DeviceType.Printer, CheckKind.Ping, CheckKind.SnmpToner, CheckKind.SnmpDrum)]
    [InlineData(DeviceType.AccessPoint, CheckKind.Ping, CheckKind.HttpWan)]
    [InlineData(DeviceType.Switch, CheckKind.Ping, CheckKind.SnmpSystem)]
    [InlineData(DeviceType.Server, CheckKind.Ping, CheckKind.SnmpSystem)]
    [InlineData(DeviceType.Camera, CheckKind.Ping, CheckKind.Tcp)]
    [InlineData(DeviceType.Other, CheckKind.Ping)]
    // F31-T001 / docs/53 D3: VM e vCenter entram na categoria de servidores —
    // perfil default idêntico ao de Server (Ping + SnmpSystem).
    [InlineData(DeviceType.VirtualMachine, CheckKind.Ping, CheckKind.SnmpSystem)]
    [InlineData(DeviceType.VCenter, CheckKind.Ping, CheckKind.SnmpSystem)]
    public void Defaults_ShouldMatchAdr014(DeviceType type, params CheckKind[] expected)
    {
        DeviceProfileDefaults.For(type).Should().Equal(expected);
    }

    [Fact]
    public void Create_ShouldMaterializeDefaultProfile()
    {
        var device = Device.Create(SomeLocationId, "prn-01", DeviceType.Printer);

        device.MonitoringChecks.Should().Equal(
            CheckKind.Ping, CheckKind.SnmpToner, CheckKind.SnmpDrum);
        device.ProfileIsCustom.Should().BeFalse(
            "device recém-criado usa o default do tipo (ADR-014)");
    }

    [Fact]
    public void Create_Camera_ShouldMaterializePingAndTcp()
    {
        var device = Device.Create(SomeLocationId, "cam-01", DeviceType.Camera);

        device.MonitoringChecks.Should().Equal(CheckKind.Ping, CheckKind.Tcp);
        device.ProfileIsCustom.Should().BeFalse();
    }

    [Fact]
    public void ApplyCustomProfile_ShouldReplaceChecksAndMarkCustom()
    {
        var device = Device.Create(SomeLocationId, "sw-01", DeviceType.Switch);

        device.ApplyCustomProfile([CheckKind.Ping]);

        device.MonitoringChecks.Should().Equal(CheckKind.Ping);
        device.ProfileIsCustom.Should().BeTrue();
    }

    [Fact]
    public void ApplyCustomProfile_WithEmptyList_ShouldThrowPtBrMessage()
    {
        var device = Device.Create(SomeLocationId, "sw-01", DeviceType.Switch);

        var action = () => device.ApplyCustomProfile([]);

        action.Should().Throw<ArgumentException>()
            .WithMessage("Informe ao menos um check.*");
        device.ProfileIsCustom.Should().BeFalse();
    }

    [Fact]
    public void ApplyCustomProfile_WithDuplicateChecks_ShouldThrowPtBrMessage()
    {
        var device = Device.Create(SomeLocationId, "sw-01", DeviceType.Switch);

        var action = () => device.ApplyCustomProfile(
            [CheckKind.Ping, CheckKind.Ping]);

        action.Should().Throw<ArgumentException>()
            .WithMessage("Checks duplicados não são permitidos.*");
    }

    [Fact]
    public void ResetProfileToDefault_ShouldRestoreTypeDefaults()
    {
        var device = Device.Create(SomeLocationId, "prn-01", DeviceType.Printer);
        device.ApplyCustomProfile([CheckKind.Ping]);

        device.ResetProfileToDefault();

        device.MonitoringChecks.Should().Equal(
            CheckKind.Ping, CheckKind.SnmpToner, CheckKind.SnmpDrum);
        device.ProfileIsCustom.Should().BeFalse();
    }

    [Fact]
    public void Update_TypeChange_ShouldKeepCustomProfile()
    {
        var device = Device.Create(SomeLocationId, "gw-01", DeviceType.Other);
        device.ApplyCustomProfile([CheckKind.Ping, CheckKind.HttpWan]);

        device.Update("gw-01", DeviceType.Router, null);

        device.Type.Should().Be(DeviceType.Router);
        device.ProfileIsCustom.Should().BeTrue(
            "mudança de tipo não sobrescreve override explícito do operador");
        device.MonitoringChecks.Should().Equal(CheckKind.Ping, CheckKind.HttpWan);
    }

    /// <summary>
    /// F30-T004 S2 (docs/52 §S2): SnmpRadio é opt-in por device — NENHUM
    /// tipo o tem como default (DeviceProfileDefaults intocado). Este teste
    /// é guarda de regressão: nasce verde e protege contra "esquecer de
    /// opt-in".
    /// </summary>
    [Fact]
    public void Defaults_ShouldNeverIncludeSnmpRadio_OptInOnly()
    {
        foreach (var type in Enum.GetValues<DeviceType>())
            DeviceProfileDefaults.For(type).Should().NotContain(
                CheckKind.SnmpRadio,
                "SnmpRadio é opt-in por device (docs/52 §S2)");
    }
}

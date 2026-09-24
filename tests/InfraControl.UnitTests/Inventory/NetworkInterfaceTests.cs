using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F2-T004: invariantes da entidade NetworkInterface — mensagens nascem em
/// pt-BR no domínio. Device 1:N NetworkInterface; MAC/IP ficam para F2-T005/F2-T006.
/// </summary>
public sealed class NetworkInterfaceTests
{
    private static readonly Guid SomeDeviceId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldSetDeviceAndTrimNameAndDescription()
    {
        var nic = NetworkInterface.Create(SomeDeviceId, "  eth0  ", "  Uplink core  ");

        nic.DeviceId.Should().Be(SomeDeviceId);
        nic.Name.Should().Be("eth0");
        nic.Description.Should().Be("Uplink core");
    }

    [Fact]
    public void Create_WithBlankDescription_ShouldStoreNull()
    {
        var nic = NetworkInterface.Create(SomeDeviceId, "eth0", "   ");

        nic.Description.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldGenerateIdAndTimestamps()
    {
        var nic = NetworkInterface.Create(SomeDeviceId, "eth0");

        nic.Id.Should().NotBe(Guid.Empty);
        nic.CreateDateTime.Should().NotBe(default(DateTime));
        nic.UpdateDateTime.Should().NotBe(default(DateTime));
    }

    [Fact]
    public void Create_WithoutDeviceId_ShouldThrowPtBrMessage()
    {
        var action = () => NetworkInterface.Create(Guid.Empty, "eth0");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id do device é obrigatório.*");
    }

    [Fact]
    public void Create_WithoutName_ShouldThrowPtBrMessage()
    {
        var action = () => NetworkInterface.Create(SomeDeviceId, "   ");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O nome da interface é obrigatório.*");
    }

    [Fact]
    public void Create_WithTooLongName_ShouldThrowPtBrMessage()
    {
        var action = () => NetworkInterface.Create(
            SomeDeviceId, new string('a', NetworkInterface.NameMaxLength + 1));

        action.Should().Throw<ArgumentException>()
            .WithMessage("O nome da interface pode ter no máximo 60 caracteres.*");
    }

    [Fact]
    public void Create_WithTooLongDescription_ShouldThrowPtBrMessage()
    {
        var action = () => NetworkInterface.Create(
            SomeDeviceId, "eth0", new string('a', NetworkInterface.DescriptionMaxLength + 1));

        action.Should().Throw<ArgumentException>()
            .WithMessage("A descrição da interface pode ter no máximo 500 caracteres.*");
    }

    [Fact]
    public void Update_ShouldApplyNewNameAndDescription()
    {
        var nic = NetworkInterface.Create(SomeDeviceId, "eth0");

        nic.Update("  eth1  ", "  Downlink  ");

        nic.Name.Should().Be("eth1");
        nic.Description.Should().Be("Downlink");
    }

    [Fact]
    public void Update_WithoutName_ShouldThrowAndKeepState()
    {
        var nic = NetworkInterface.Create(SomeDeviceId, "eth0");

        var action = () => nic.Update("  ", null);

        action.Should().Throw<ArgumentException>();
        nic.Name.Should().Be("eth0", "falha de validação não pode alterar o agregado");
    }
}

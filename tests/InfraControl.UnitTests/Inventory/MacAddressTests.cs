using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F2-T005: invariantes da entidade MacAddress — NetworkInterface 1:N MacAddress,
/// MAC único global (case-insensitive), normalização aa:bb:cc:dd:ee:ff em pt-BR.
/// </summary>
public sealed class MacAddressTests
{
    private static readonly Guid SomeInterfaceId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldSetInterfaceAndNormalizeMac()
    {
        var mac = MacAddress.Create(SomeInterfaceId, "  AA-BB-CC-DD-EE-FF  ");

        mac.InterfaceId.Should().Be(SomeInterfaceId);
        mac.Mac.Should().Be("aa:bb:cc:dd:ee:ff");
    }

    [Fact]
    public void Create_WithCiscoDotNotation_ShouldNormalize()
    {
        var mac = MacAddress.Create(SomeInterfaceId, "aabb.ccdd.eeff");

        mac.Mac.Should().Be("aa:bb:cc:dd:ee:ff");
    }

    [Fact]
    public void Create_ShouldGenerateIdAndTimestamps()
    {
        var mac = MacAddress.Create(SomeInterfaceId, "aa:bb:cc:dd:ee:ff");

        mac.Id.Should().NotBe(Guid.Empty);
        mac.CreateDateTime.Should().NotBe(default(DateTime));
        mac.UpdateDateTime.Should().NotBe(default(DateTime));
    }

    [Fact]
    public void Create_WithoutInterfaceId_ShouldThrowPtBrMessage()
    {
        var action = () => MacAddress.Create(Guid.Empty, "aa:bb:cc:dd:ee:ff");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id da interface é obrigatório.*");
    }

    [Fact]
    public void Create_WithoutMac_ShouldThrowPtBrMessage()
    {
        var action = () => MacAddress.Create(SomeInterfaceId, "   ");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O MAC é obrigatório.*");
    }

    [Fact]
    public void Create_WithInvalidMac_ShouldThrowPtBrMessage()
    {
        var action = () => MacAddress.Create(SomeInterfaceId, "not-a-mac");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O MAC informado é inválido.*");
    }

    [Fact]
    public void Create_WithTooLongMac_ShouldThrowPtBrMessage()
    {
        var action = () => MacAddress.Create(SomeInterfaceId, new string('a', MacAddress.MacMaxLength + 1));

        action.Should().Throw<ArgumentException>()
            .WithMessage("O MAC pode ter no máximo 17 caracteres.*");
    }

    [Fact]
    public void Update_ShouldApplyNormalizedMac()
    {
        var mac = MacAddress.Create(SomeInterfaceId, "aa:bb:cc:dd:ee:ff");

        mac.Update("11-22-33-44-55-66");

        mac.Mac.Should().Be("11:22:33:44:55:66");
    }

    [Fact]
    public void Update_WithInvalidMac_ShouldThrowAndKeepState()
    {
        var mac = MacAddress.Create(SomeInterfaceId, "aa:bb:cc:dd:ee:ff");

        var action = () => mac.Update("banana");

        action.Should().Throw<ArgumentException>();
        mac.Mac.Should().Be("aa:bb:cc:dd:ee:ff", "falha de validação não pode alterar o agregado");
    }
}

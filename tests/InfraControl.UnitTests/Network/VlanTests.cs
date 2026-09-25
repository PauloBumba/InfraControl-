using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Network;

// F3-T003: especificação de domínio da Vlan (docs/03: SwitchPort N:N Vlan —
// a vinculação N:N é F3-T004). Número = tag 802.1Q (1..4094), nome único
// global case-insensitive. Mensagens de validação nascem em pt-BR aqui.
public sealed class VlanTests
{
    [Fact]
    public void Create_Valid_ShouldTrimAndSetValues()
    {
        var vlan = Vlan.Create("  Radiologia  ", 10, "Setor de imagem");

        vlan.Name.Should().Be("Radiologia");
        vlan.VlanNumber.Should().Be(10);
        vlan.Description.Should().Be("Setor de imagem");
        vlan.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_WithoutDescription_ShouldDefaultToNull()
    {
        var vlan = Vlan.Create("Telefonia", 20);

        vlan.Description.Should().BeNull();
    }

    [Fact]
    public void Create_EmptyName_ShouldThrowPtBrMessage()
    {
        var act = () => Vlan.Create("   ", 10);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O nome da VLAN é obrigatório.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4095)]
    public void Create_NumberOutOfRange_ShouldThrowPtBrMessage(int vlanNumber)
    {
        var act = () => Vlan.Create("Radiologia", vlanNumber);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O número da VLAN deve estar entre 1 e 4094.*");
    }

    [Fact]
    public void Create_NameTooLong_ShouldThrowPtBrMessage()
    {
        var act = () => Vlan.Create(new string('a', Vlan.NameMaxLength + 1), 10);

        act.Should().Throw<ArgumentException>()
            .WithMessage($"O nome da VLAN pode ter no máximo {Vlan.NameMaxLength} caracteres.*");
    }

    [Fact]
    public void Create_DescriptionTooLong_ShouldThrowPtBrMessage()
    {
        var act = () => Vlan.Create("Radiologia", 10, new string('a', Vlan.DescriptionMaxLength + 1));

        act.Should().Throw<ArgumentException>()
            .WithMessage($"A descrição da VLAN pode ter no máximo {Vlan.DescriptionMaxLength} caracteres.*");
    }

    [Fact]
    public void Update_Valid_ShouldApplyAllValues()
    {
        var vlan = Vlan.Create("Radiologia", 10, "Setor de imagem");

        vlan.Update("Radiologia Norte", 15, null);

        vlan.Name.Should().Be("Radiologia Norte");
        vlan.VlanNumber.Should().Be(15);
        vlan.Description.Should().BeNull();
    }

    [Fact]
    public void Update_EmptyName_ShouldThrowPtBrMessage()
    {
        var vlan = Vlan.Create("Radiologia", 10);

        var act = () => vlan.Update("  ", 10, null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O nome da VLAN é obrigatório.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4095)]
    public void Update_NumberOutOfRange_ShouldThrowPtBrMessage(int vlanNumber)
    {
        var vlan = Vlan.Create("Radiologia", 10);

        var act = () => vlan.Update("Radiologia", vlanNumber, null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O número da VLAN deve estar entre 1 e 4094.*");
    }
}

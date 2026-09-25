using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// F3-T002: invariantes da entidade SwitchPort — Switch(Device) 1:N SwitchPort
/// (docs/03), nome único POR SWITCH (case-insensitive) e link up/down com
/// null = ainda não observado (F4 preenche via discovery/SNMP).
/// </summary>
public sealed class SwitchPortTests
{
    private static readonly Guid SomeSwitchId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldSetSwitchTrimmedNameAndDescription()
    {
        var port = SwitchPort.Create(SomeSwitchId, "  Gi1/0/1  ", "  Uplink p/ core  ");

        port.DeviceId.Should().Be(SomeSwitchId);
        port.Name.Should().Be("Gi1/0/1");
        port.Description.Should().Be("Uplink p/ core");
    }

    [Fact]
    public void Create_ShouldDefaultLinkUpToNull()
    {
        var port = SwitchPort.Create(SomeSwitchId, "Gi1/0/1");

        port.LinkUp.Should().BeNull("link up/down só passa a existir quando observado (F4)");
        port.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_WithoutSwitchId_ShouldThrowPtBrMessage()
    {
        var action = () => SwitchPort.Create(Guid.Empty, "Gi1/0/1");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id do switch é obrigatório.*");
    }

    [Fact]
    public void Create_WithoutName_ShouldThrowPtBrMessage()
    {
        var action = () => SwitchPort.Create(SomeSwitchId, "   ");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O nome da porta é obrigatório.*");
    }

    [Fact]
    public void Create_WithTooLongName_ShouldThrowPtBrMessage()
    {
        var action = () => SwitchPort.Create(SomeSwitchId, new string('P', 61));

        action.Should().Throw<ArgumentException>()
            .WithMessage("O nome da porta pode ter no máximo 60 caracteres.*");
    }

    [Fact]
    public void Create_WithTooLongDescription_ShouldThrowPtBrMessage()
    {
        var action = () => SwitchPort.Create(SomeSwitchId, "Gi1/0/1", new string('d', 501));

        action.Should().Throw<ArgumentException>()
            .WithMessage("A descrição da porta pode ter no máximo 500 caracteres.*");
    }

    [Fact]
    public void Update_ShouldApplyNameDescriptionAndLinkUp()
    {
        var port = SwitchPort.Create(SomeSwitchId, "Gi1/0/1");

        port.Update("Gi1/0/2", "Porta de acesso", linkUp: true);

        port.Name.Should().Be("Gi1/0/2");
        port.Description.Should().Be("Porta de acesso");
        port.LinkUp.Should().BeTrue();
    }

    [Fact]
    public void Update_WithEmptyName_ShouldThrowAndKeepState()
    {
        var port = SwitchPort.Create(SomeSwitchId, "Gi1/0/1");

        var action = () => port.Update("  ", null, linkUp: null);

        action.Should().Throw<ArgumentException>();
        port.Name.Should().Be("Gi1/0/1", "falha de validação não pode alterar o agregado");
    }
}

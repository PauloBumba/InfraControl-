using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Network;

// F3-T004: junção pura SwitchPort N:N Vlan (docs/03; decisão do usuário:
// sem flag tagged/untagged nesta fase).
public sealed class SwitchPortVlanTests
{
    [Fact]
    public void Create_Valid_ShouldSetPair()
    {
        var portId = Guid.NewGuid();
        var vlanId = Guid.NewGuid();

        var link = SwitchPortVlan.Create(portId, vlanId);

        link.SwitchPortId.Should().Be(portId);
        link.VlanId.Should().Be(vlanId);
        link.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_EmptySwitchPortId_ShouldThrowPtBrMessage()
    {
        var act = () => SwitchPortVlan.Create(Guid.Empty, Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id da porta é obrigatório.*");
    }

    [Fact]
    public void Create_EmptyVlanId_ShouldThrowPtBrMessage()
    {
        var act = () => SwitchPortVlan.Create(Guid.NewGuid(), Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id da VLAN é obrigatório.*");
    }
}

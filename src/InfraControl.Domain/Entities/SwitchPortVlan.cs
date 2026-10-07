using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Junção pura SwitchPort N:N Vlan (docs/03, F3-T004 — decisão do usuário:
/// sem flag tagged/untagged por enquanto; modelar acesso/trunk fica para
/// quando F4 (SNMP Q-BRIDGE-MIB) ou F11 trouxerem essa distinção).
/// Par (porta, vlan) é único — índice composto na migração.
/// </summary>
public sealed class SwitchPortVlan : AggregateRoot
{
    public Guid SwitchPortId { get; private set; }
    public Guid VlanId { get; private set; }

    private SwitchPortVlan() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new SwitchPortVlanDeletedDomainEvent(Id, SwitchPortId, VlanId));

    public static SwitchPortVlan Create(Guid switchPortId, Guid vlanId)
    {
        if (switchPortId == Guid.Empty)
            throw new ArgumentException("O id da porta é obrigatório.", nameof(switchPortId));

        if (vlanId == Guid.Empty)
            throw new ArgumentException("O id da VLAN é obrigatório.", nameof(vlanId));

        var link = new SwitchPortVlan
        {
            SwitchPortId = switchPortId,
            VlanId = vlanId
        };
        link.RaiseDomainEvent(new SwitchPortVlanCreatedDomainEvent(
            link.Id, link.SwitchPortId, link.VlanId));
        return link;
    }
}

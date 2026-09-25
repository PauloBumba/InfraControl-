using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Network.SwitchPortVlans.Ports;

public interface ISwitchPortVlanRepository : IRepository<SwitchPortVlan>
{
    /// <summary>Busca o par (porta, vlan) — é a unicidade natural da junção.</summary>
    Task<SwitchPortVlan?> GetPairAsync(
        Guid switchPortId,
        Guid vlanId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetVlanIdsByPortAsync(
        Guid switchPortId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetPortIdsByVlanAsync(
        Guid vlanId,
        CancellationToken cancellationToken = default);

    /// <summary>Usado pelo DeleteSwitchPort para bloquear exclusão com VLANs (→ 409).</summary>
    Task<bool> HasVlanLinksAsync(Guid switchPortId, CancellationToken cancellationToken = default);

    /// <summary>Usado pelo DeleteVlan para bloquear exclusão com portas (→ 409).</summary>
    Task<bool> HasPortLinksAsync(Guid vlanId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

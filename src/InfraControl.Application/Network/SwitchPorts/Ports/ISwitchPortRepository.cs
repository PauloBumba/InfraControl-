using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Network.SwitchPorts.Ports;

public interface ISwitchPortRepository : IRepository<SwitchPort>
{
    /// <summary>Busca porta por nome DENTRO de um switch, sem distinção de maiúsculas.</summary>
    Task<SwitchPort?> GetByNameAsync(Guid deviceId, string name, CancellationToken cancellationToken = default);

    /// <summary>Carrega várias portas de uma vez (listas da junção F3-T004).</summary>
    Task<IReadOnlyList<SwitchPort>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    Task<PagedResult<SwitchPort>> SearchAsync(
        Guid deviceId,
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

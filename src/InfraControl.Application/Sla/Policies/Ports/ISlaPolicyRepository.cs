using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Sla.Policies.Ports;

public interface ISlaPolicyRepository : IRepository<SlaPolicy>
{
    Task<PagedResult<SlaPolicy>> SearchAsync(
        string? term,
        Guid? serviceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Usado pelo DeleteService para bloquear exclusão com policies (F8-T002 → 409).</summary>
    Task<bool> HasServicePoliciesAsync(Guid serviceId, CancellationToken cancellationToken = default);

    /// <summary>Política padrão global (IsDefault, sem ServiceId) — auto-aplicada a tasks novas.</summary>
    Task<SlaPolicy?> GetDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>Desmarca IsDefault de todas as políticas exceto <paramref name="exceptId"/>.</summary>
    Task ClearDefaultsExceptAsync(Guid exceptId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

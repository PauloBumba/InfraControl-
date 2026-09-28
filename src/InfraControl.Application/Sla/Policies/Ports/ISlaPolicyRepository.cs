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

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

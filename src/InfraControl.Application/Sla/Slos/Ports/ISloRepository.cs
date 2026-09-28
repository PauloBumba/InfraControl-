using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Sla.Slos.Ports;

public interface ISloRepository : IRepository<Slo>
{
    Task<PagedResult<Slo>> SearchAsync(
        string? term,
        Guid? serviceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Usado pelo DeleteService para bloquear exclusão com SLOs (F8-T005 → 409).</summary>
    Task<bool> HasServiceSlosAsync(Guid serviceId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

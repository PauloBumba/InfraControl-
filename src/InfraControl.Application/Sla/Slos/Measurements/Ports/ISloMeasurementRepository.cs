using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Sla.Slos.Measurements.Ports;

public interface ISloMeasurementRepository : IRepository<SloMeasurement>
{
    Task<PagedResult<SloMeasurement>> SearchBySloIdAsync(
        Guid sloId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Usado pelo DeleteSlo para bloquear exclusão com medições (F8-T006 → 409).</summary>
    Task<bool> HasSloMeasurementsAsync(Guid sloId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

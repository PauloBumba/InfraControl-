using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Sla.Slos.Measurements.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class SloMeasurementRepository(AppDbContext dbContext)
    : ISloMeasurementRepository
{
    public async Task AddAsync(
        SloMeasurement measurement, CancellationToken cancellationToken = default)
    {
        await dbContext.SloMeasurements.AddAsync(measurement, cancellationToken);
    }

    public Task<SloMeasurement?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.SloMeasurements
            .SingleOrDefaultAsync(m => m.Id == id, cancellationToken);

    public void Remove(SloMeasurement measurement)
        => dbContext.SloMeasurements.Remove(measurement);

    public async Task<PagedResult<SloMeasurement>> SearchBySloIdAsync(
        Guid sloId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.SloMeasurements
            .AsNoTracking()
            .Where(m => m.SloId == sloId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(m => m.MeasuredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SloMeasurement>(items, totalCount, page, pageSize);
    }

    public Task<bool> HasSloMeasurementsAsync(
        Guid sloId, CancellationToken cancellationToken = default)
        => dbContext.SloMeasurements.AnyAsync(m => m.SloId == sloId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

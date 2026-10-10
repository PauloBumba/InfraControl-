using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F19-T006: lookup do identificador externo estável (unicidade física em
/// source+external_id) → DeviceId, no sentido de leitura das integrações.
/// </summary>
public sealed class ExternalDeviceReferenceRepository(AppDbContext dbContext)
    : IExternalDeviceReferenceRepository
{
    public Task<ExternalDeviceReference?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.ExternalDeviceReferences
            .FirstOrDefaultAsync(reference => reference.Id == id, cancellationToken);

    public async Task AddAsync(
        ExternalDeviceReference entity, CancellationToken cancellationToken = default)
        => await dbContext.ExternalDeviceReferences.AddAsync(entity, cancellationToken);

    public void Remove(ExternalDeviceReference entity)
        => dbContext.ExternalDeviceReferences.Remove(entity);

    public async Task<Guid?> GetDeviceIdAsync(
        ExternalDeviceSource source,
        string externalId,
        CancellationToken cancellationToken = default)
    {
        var normalized = ExternalDeviceReference.NormalizeExternalId(externalId);

        return await dbContext.ExternalDeviceReferences
            .Where(reference =>
                reference.Source == source
                && reference.ExternalId == normalized)
            .Select(reference => (Guid?)reference.DeviceId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExternalDeviceReference>> ListBySourceAsync(
        ExternalDeviceSource source, CancellationToken cancellationToken = default)
        => await dbContext.ExternalDeviceReferences
            .Where(reference => reference.Source == source)
            .OrderBy(reference => reference.ExternalId)
            .ToListAsync(cancellationToken);

    public async Task<ExternalDeviceReference?> GetBySourceAndExternalIdAsync(
        ExternalDeviceSource source, string externalId,
        CancellationToken cancellationToken = default)
    {
        var normalized = ExternalDeviceReference.NormalizeExternalId(externalId);
        return await dbContext.ExternalDeviceReferences
            .FirstOrDefaultAsync(reference =>
                reference.Source == source && reference.ExternalId == normalized,
                cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

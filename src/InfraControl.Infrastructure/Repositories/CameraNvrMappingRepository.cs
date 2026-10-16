using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.CameraNvrMappings.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class CameraNvrMappingRepository(AppDbContext dbContext)
    : ICameraNvrMappingRepository
{
    public async Task AddAsync(
        CameraNvrMapping mapping, CancellationToken cancellationToken = default)
    {
        await dbContext.CameraNvrMappings.AddAsync(mapping, cancellationToken);
    }

    public Task<CameraNvrMapping?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.CameraNvrMappings
            .SingleOrDefaultAsync(mapping => mapping.Id == id, cancellationToken);

    public void Remove(CameraNvrMapping mapping)
        => dbContext.CameraNvrMappings.Remove(mapping);

    public Task<CameraNvrMapping?> GetByNvrAndChannelAsync(
        Guid nvrId, int channelNumber, CancellationToken cancellationToken = default)
        => dbContext.CameraNvrMappings
            .FirstOrDefaultAsync(
                mapping => mapping.NvrId == nvrId && mapping.ChannelNumber == channelNumber,
                cancellationToken);

    public async Task<PagedResult<CameraNvrMapping>> SearchAsync(
        Guid? nvrId,
        Guid? cameraId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.CameraNvrMappings.AsNoTracking().AsQueryable();

        if (nvrId.HasValue)
            query = query.Where(mapping => mapping.NvrId == nvrId.Value);

        if (cameraId.HasValue)
            query = query.Where(mapping => mapping.CameraId == cameraId.Value);

        return await EfSearch.PageAsync(
            query
                .OrderBy(mapping => mapping.NvrId)
                .ThenBy(mapping => mapping.ChannelNumber),
            page,
            pageSize,
            cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class LocationRepository(AppDbContext dbContext) : ILocationRepository
{
    public async Task AddAsync(Location location, CancellationToken cancellationToken = default)
    {
        await dbContext.Locations.AddAsync(location, cancellationToken);
    }

    public Task<Location?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.Locations
            .SingleOrDefaultAsync(location => location.Id == id, cancellationToken);

    public void Remove(Location location)
        => dbContext.Locations.Remove(location);

    public Task<Location?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return dbContext.Locations
            .FirstOrDefaultAsync(
                location => location.Name.ToLower() == normalized,
                cancellationToken);
    }

    public async Task<PagedResult<Location>> SearchAsync(
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Locations.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = term.Trim().ToLowerInvariant();
            query = query.Where(location =>
                location.Name.ToLower().Contains(normalized)
                || (location.Description != null && location.Description.ToLower().Contains(normalized)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(location => location.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Location>(items, totalCount, page, pageSize);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

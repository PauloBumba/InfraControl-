using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Racks.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class RackRepository(AppDbContext dbContext) : IRackRepository
{
    public async Task AddAsync(Rack rack, CancellationToken cancellationToken = default)
    {
        await dbContext.Racks.AddAsync(rack, cancellationToken);
    }

    public Task<Rack?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.Racks
            .SingleOrDefaultAsync(rack => rack.Id == id, cancellationToken);

    public void Remove(Rack rack)
        => dbContext.Racks.Remove(rack);

    public Task<Rack?> GetByNameAsync(Guid locationId, string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return dbContext.Racks
            .FirstOrDefaultAsync(
                rack => rack.LocationId == locationId && rack.Name.ToLower() == normalized,
                cancellationToken);
    }

    public async Task<PagedResult<Rack>> SearchAsync(
        Guid? locationId,
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Racks.AsNoTracking().AsQueryable();

        if (locationId.HasValue)
            query = query.Where(rack => rack.LocationId == locationId.Value);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = term.Trim().ToLowerInvariant();
            query = query.Where(rack =>
                rack.Name.ToLower().Contains(normalized)
                || (rack.Description != null && rack.Description.ToLower().Contains(normalized)));
        }

        // F29-T001A — contar/saltar/pegar era o mesmo corpo em ~25 repositórios.
        // O que varia (filtro e ordenação) fica aqui; o que é igual sai de
        // cena. PageAsync também saneia a página, então este porto não depende
        // de o chamador lembrar de normalizar.
        return await EfSearch.PageAsync(
            query
                .OrderBy(rack => rack.LocationId)
                .ThenBy(rack => rack.Name),
            page,
            pageSize,
            cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

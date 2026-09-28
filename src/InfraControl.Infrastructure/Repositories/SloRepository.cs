using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Sla.Slos.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class SloRepository(AppDbContext dbContext) : ISloRepository
{
    public async Task AddAsync(Slo slo, CancellationToken cancellationToken = default)
    {
        await dbContext.Slos.AddAsync(slo, cancellationToken);
    }

    public Task<Slo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.Slos.SingleOrDefaultAsync(slo => slo.Id == id, cancellationToken);

    public void Remove(Slo slo)
        => dbContext.Slos.Remove(slo);

    public async Task<PagedResult<Slo>> SearchAsync(
        string? term,
        Guid? serviceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Slos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = term.Trim().ToLowerInvariant();
            query = query.Where(slo => slo.Name.ToLower().Contains(normalized));
        }

        if (serviceId is not null)
            query = query.Where(slo => slo.ServiceId == serviceId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(slo => slo.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Slo>(items, totalCount, page, pageSize);
    }

    public Task<bool> HasServiceSlosAsync(
        Guid serviceId, CancellationToken cancellationToken = default)
        => dbContext.Slos.AnyAsync(slo => slo.ServiceId == serviceId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

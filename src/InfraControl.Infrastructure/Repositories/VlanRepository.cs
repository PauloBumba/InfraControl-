using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Network.Vlans.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class VlanRepository(AppDbContext dbContext) : IVlanRepository
{
    public async Task AddAsync(Vlan vlan, CancellationToken cancellationToken = default)
    {
        await dbContext.Vlans.AddAsync(vlan, cancellationToken);
    }

    public Task<Vlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.Vlans
            .SingleOrDefaultAsync(vlan => vlan.Id == id, cancellationToken);

    public void Remove(Vlan vlan)
        => dbContext.Vlans.Remove(vlan);

    public Task<Vlan?> GetByNumberAsync(int vlanNumber, CancellationToken cancellationToken = default)
        => dbContext.Vlans
            .SingleOrDefaultAsync(vlan => vlan.VlanNumber == vlanNumber, cancellationToken);

    public Task<Vlan?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return dbContext.Vlans
            .FirstOrDefaultAsync(vlan => vlan.Name.ToLower() == normalized, cancellationToken);
    }

    public async Task<PagedResult<Vlan>> SearchAsync(
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Vlans.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = term.Trim().ToLowerInvariant();
            query = query.Where(vlan =>
                vlan.Name.ToLower().Contains(normalized)
                || (vlan.Description != null && vlan.Description.ToLower().Contains(normalized)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(vlan => vlan.VlanNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Vlan>(items, totalCount, page, pageSize);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

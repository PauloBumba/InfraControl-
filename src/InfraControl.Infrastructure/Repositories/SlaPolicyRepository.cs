using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Sla.Policies.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class SlaPolicyRepository(AppDbContext dbContext) : ISlaPolicyRepository
{
    public async Task AddAsync(SlaPolicy policy, CancellationToken cancellationToken = default)
    {
        await dbContext.SlaPolicies.AddAsync(policy, cancellationToken);
    }

    public Task<SlaPolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.SlaPolicies
            .SingleOrDefaultAsync(policy => policy.Id == id, cancellationToken);

    public void Remove(SlaPolicy policy)
        => dbContext.SlaPolicies.Remove(policy);

    public async Task<PagedResult<SlaPolicy>> SearchAsync(
        string? term,
        Guid? serviceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.SlaPolicies.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = term.Trim().ToLowerInvariant();
            query = query.Where(policy => policy.Name.ToLower().Contains(normalized));
        }

        if (serviceId is not null)
            query = query.Where(policy => policy.ServiceId == serviceId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(policy => policy.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SlaPolicy>(items, totalCount, page, pageSize);
    }

    public Task<bool> HasServicePoliciesAsync(
        Guid serviceId, CancellationToken cancellationToken = default)
        => dbContext.SlaPolicies.AnyAsync(
            policy => policy.ServiceId == serviceId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

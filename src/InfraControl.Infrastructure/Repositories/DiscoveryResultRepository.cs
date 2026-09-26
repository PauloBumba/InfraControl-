using InfraControl.BuildingBlocks.Domain;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Discovery.Results.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class DiscoveryResultRepository(AppDbContext dbContext) : IDiscoveryResultRepository
{
    public async Task AddAsync(DiscoveryResult result, CancellationToken cancellationToken = default)
    {
        await dbContext.DiscoveryResults.AddAsync(result, cancellationToken);
    }

    public Task<DiscoveryResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.DiscoveryResults
            .SingleOrDefaultAsync(result => result.Id == id, cancellationToken);

    public void Remove(DiscoveryResult result)
        => dbContext.DiscoveryResults.Remove(result);

    public async Task<PagedResult<DiscoveryResult>> SearchByRunAsync(
        Guid discoveryRunId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.DiscoveryResults
            .AsNoTracking()
            .Where(result => result.DiscoveryRunId == discoveryRunId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(result => result.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<DiscoveryResult>(items, totalCount, page, pageSize);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

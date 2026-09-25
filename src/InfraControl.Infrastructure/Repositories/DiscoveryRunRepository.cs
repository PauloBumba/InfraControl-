using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Discovery.Runs.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class DiscoveryRunRepository(AppDbContext dbContext) : IDiscoveryRunRepository
{
    public async Task AddAsync(DiscoveryRun run, CancellationToken cancellationToken = default)
    {
        await dbContext.DiscoveryRuns.AddAsync(run, cancellationToken);
    }

    public Task<DiscoveryRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.DiscoveryRuns
            .SingleOrDefaultAsync(run => run.Id == id, cancellationToken);

    public void Remove(DiscoveryRun run)
        => dbContext.DiscoveryRuns.Remove(run);

    public async Task<PagedResult<DiscoveryRun>> SearchAsync(
        Guid? deviceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.DiscoveryRuns.AsNoTracking();

        if (deviceId is not null)
            query = query.Where(run => run.DeviceId == deviceId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(run => run.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<DiscoveryRun>(items, totalCount, page, pageSize);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F26 Fase 9.1: execuções de sincronização UniFi.</summary>
public sealed class UniFiSyncRunsRepository(AppDbContext dbContext)
    : IUniFiSyncRunsRepository
{
    public Task<UniFiSyncRun?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.UniFiSyncRuns
            .FirstOrDefaultAsync(run => run.Id == id, cancellationToken);

    public async Task AddAsync(UniFiSyncRun entity, CancellationToken cancellationToken = default)
        => await dbContext.UniFiSyncRuns.AddAsync(entity, cancellationToken);

    public void Remove(UniFiSyncRun entity)
        => dbContext.UniFiSyncRuns.Remove(entity);

    public async Task<IReadOnlyList<UniFiSyncRun>> ListRecentAsync(
        int limit, bool? succeeded, DateTime? fromUtc, DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.UniFiSyncRuns.AsNoTracking().AsQueryable();
        if (succeeded is not null)
            query = query.Where(run => run.Succeeded == succeeded);
        if (fromUtc is not null)
            query = query.Where(run => run.StartedAtUtc >= fromUtc);
        if (toUtc is not null)
            query = query.Where(run => run.StartedAtUtc <= toUtc);

        return await query
            .OrderByDescending(run => run.StartedAtUtc)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F26 Fase 3.1: sites UniFi persistidos.</summary>
public sealed class UniFiSitesRepository(AppDbContext dbContext)
    : IUniFiSitesRepository
{
    public async Task<IReadOnlyList<UniFiSite>> ListAsync(CancellationToken cancellationToken)
        => await dbContext.UniFiSites
            .OrderBy(site => site.Name)
            .ToListAsync(cancellationToken);

    public async Task<UniFiSite?> GetByShortIdAsync(
        string shortId, CancellationToken cancellationToken)
        => await dbContext.UniFiSites
            .FirstOrDefaultAsync(site => site.ShortId == shortId, cancellationToken);

    public async Task AddAsync(UniFiSite site, CancellationToken cancellationToken)
        => await dbContext.UniFiSites.AddAsync(site, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}

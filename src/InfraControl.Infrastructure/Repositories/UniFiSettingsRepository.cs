using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F26 Fase 2.2: linha única de configuração UniFi no banco.
/// </summary>
public sealed class UniFiSettingsRepository(AppDbContext dbContext)
    : IUniFiSettingsRepository
{
    public async Task<UniFiIntegrationSetting?> GetAsync(
        CancellationToken cancellationToken)
        => await dbContext.UniFiIntegrationSettings
            .FirstOrDefaultAsync(cancellationToken);

    public async Task SaveAsync(
        UniFiIntegrationSetting setting, CancellationToken cancellationToken)
    {
        var existing = await dbContext.UniFiIntegrationSettings
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
            await dbContext.UniFiIntegrationSettings.AddAsync(setting, cancellationToken);
        else if (!ReferenceEquals(existing, setting))
            dbContext.Entry(existing).CurrentValues.SetValues(setting);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

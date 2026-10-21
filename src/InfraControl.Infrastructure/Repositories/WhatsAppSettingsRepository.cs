using InfraControl.Application.Integrations.WhatsApp.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// ADR-062: linha única de configuração WhatsApp no banco.
/// </summary>
public sealed class WhatsAppSettingsRepository(AppDbContext dbContext)
    : IWhatsAppSettingsRepository
{
    public async Task<WhatsAppIntegrationSetting?> GetAsync(
        CancellationToken cancellationToken)
        => await dbContext.WhatsAppIntegrationSettings
            .FirstOrDefaultAsync(cancellationToken);

    public async Task SaveAsync(
        WhatsAppIntegrationSetting setting, CancellationToken cancellationToken)
    {
        var existing = await dbContext.WhatsAppIntegrationSettings
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
            await dbContext.WhatsAppIntegrationSettings.AddAsync(setting, cancellationToken);
        else if (!ReferenceEquals(existing, setting))
            dbContext.Entry(existing).CurrentValues.SetValues(setting);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

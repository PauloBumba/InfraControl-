using InfraControl.Application.Integrations.Email.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F41-T001: linha única de configuração SMTP no banco.
/// </summary>
public sealed class EmailSettingsRepository(AppDbContext dbContext)
    : IEmailSettingsRepository
{
    public async Task<EmailIntegrationSetting?> GetAsync(
        CancellationToken cancellationToken)
        => await dbContext.EmailIntegrationSettings
            .FirstOrDefaultAsync(cancellationToken);

    public async Task SaveAsync(
        EmailIntegrationSetting setting, CancellationToken cancellationToken)
    {
        var existing = await dbContext.EmailIntegrationSettings
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
            await dbContext.EmailIntegrationSettings.AddAsync(setting, cancellationToken);
        else if (!ReferenceEquals(existing, setting))
            dbContext.Entry(existing).CurrentValues.SetValues(setting);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

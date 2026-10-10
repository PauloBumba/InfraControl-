using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F27 Fase 2.2: linha única de configuração Zabbix no banco.
/// </summary>
public sealed class ZabbixSettingsRepository(AppDbContext dbContext)
    : IZabbixSettingsRepository
{
    public async Task<ZabbixIntegrationSetting?> GetAsync(
        CancellationToken cancellationToken)
        => await dbContext.ZabbixIntegrationSettings
            .FirstOrDefaultAsync(cancellationToken);

    public async Task SaveAsync(
        ZabbixIntegrationSetting setting, CancellationToken cancellationToken)
    {
        var existing = await dbContext.ZabbixIntegrationSettings
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
            await dbContext.ZabbixIntegrationSettings.AddAsync(setting, cancellationToken);
        else if (!ReferenceEquals(existing, setting))
            dbContext.Entry(existing).CurrentValues.SetValues(setting);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

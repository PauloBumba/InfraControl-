using InfraControl.Domain.Entities;

namespace InfraControl.Application.Integrations.Zabbix.Ports;

/// <summary>
/// F27 Fase 2.2: persistência da configuração Zabbix (linha única).
/// </summary>
public interface IZabbixSettingsRepository
{
    Task<ZabbixIntegrationSetting?> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(
        ZabbixIntegrationSetting setting, CancellationToken cancellationToken);
}

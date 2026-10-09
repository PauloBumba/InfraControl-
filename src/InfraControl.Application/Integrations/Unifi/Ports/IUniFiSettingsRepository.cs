using InfraControl.Domain.Entities;

namespace InfraControl.Application.Integrations.Unifi.Ports;

/// <summary>
/// F26 Fase 2.2: persistência da configuração UniFi (linha única).
/// </summary>
public interface IUniFiSettingsRepository
{
    Task<UniFiIntegrationSetting?> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(
        UniFiIntegrationSetting setting, CancellationToken cancellationToken);
}

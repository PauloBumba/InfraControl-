using InfraControl.Domain.Entities;

namespace InfraControl.Application.Integrations.WhatsApp.Ports;

/// <summary>
/// ADR-062: persistência da configuração do canal WhatsApp (linha única).
/// </summary>
public interface IWhatsAppSettingsRepository
{
    Task<WhatsAppIntegrationSetting?> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(
        WhatsAppIntegrationSetting setting, CancellationToken cancellationToken);
}

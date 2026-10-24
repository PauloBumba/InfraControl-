using InfraControl.Domain.Entities;

namespace InfraControl.Application.Integrations.Email.Ports;

/// <summary>
/// F41-T001: persistência da configuração SMTP (linha única).
/// </summary>
public interface IEmailSettingsRepository
{
    Task<EmailIntegrationSetting?> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(
        EmailIntegrationSetting setting, CancellationToken cancellationToken);
}

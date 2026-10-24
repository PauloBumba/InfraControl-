namespace InfraControl.Application.Integrations.Email.Ports;

/// <summary>
/// F41-T001: opções SMTP de env/config (fallback) expostas para o
/// Application via porta.
/// </summary>
public interface IEmailEnvOptionsAccessor
{
    EmailIntegrationOptions Current { get; }
}

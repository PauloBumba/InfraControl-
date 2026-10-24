using InfraControl.Application.Integrations.Email;
using InfraControl.Application.Integrations.Email.Ports;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Integrations.Email;

/// <summary>
/// F41-T001: opções efetivas do canal Email (banco → env). Falha ao
/// decifrar a senha persistida vira string vazia + log de erro (a chamada
/// seguinte ao SMTP falha com erro de credencial identificável, nunca com
/// exception opaca de criptografia) — mesmo padrão do Zabbix/WhatsApp.
/// </summary>
public sealed class EmailOptionsResolver(
    IEmailSettingsRepository repository,
    IEmailEnvOptionsAccessor envAccessor,
    ICredentialProtector protector,
    ILogger<EmailOptionsResolver> logger)
    : IEmailOptionsResolver
{
    public async Task<EmailIntegrationOptions> ResolveAsync(
        CancellationToken cancellationToken)
    {
        var setting = await repository.GetAsync(cancellationToken);
        if (setting is null)
            return envAccessor.Current;

        return new EmailIntegrationOptions
        {
            Host = setting.Host,
            Port = setting.Port,
            Username = setting.Username,
            Password = UnprotectQuietly(setting.PasswordEncrypted),
            From = setting.From,
            FromName = setting.FromName,
            UseSsl = setting.UseSsl
        };
    }

    private string UnprotectQuietly(string passwordEncrypted)
    {
        try
        {
            return protector.Unprotect(passwordEncrypted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao decifrar a senha SMTP persistida");
            return string.Empty;
        }
    }
}

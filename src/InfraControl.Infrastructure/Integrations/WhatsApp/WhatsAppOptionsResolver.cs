using InfraControl.Application.Integrations.WhatsApp.Ports;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Integrations.WhatsApp;

/// <summary>
/// ADR-062: opções efetivas do canal WhatsApp (banco → env). Falha ao
/// decifrar a ApiKey persistida vira string vazia + log de erro (a chamada
/// seguinte à Evolution falha com erro de credencial identificável, nunca
/// com exception opaca de criptografia). Grupo/Phone vêm do banco quando
/// há linha (escolha na UI); sem linha, caem no env.
/// </summary>
public sealed class WhatsAppOptionsResolver(
    IWhatsAppSettingsRepository repository,
    IWhatsAppEnvOptionsAccessor envAccessor,
    ICredentialProtector protector,
    ILogger<WhatsAppOptionsResolver> logger)
    : IWhatsAppOptionsResolver
{
    public async Task<WhatsAppChannelOptions> ResolveAsync(
        CancellationToken cancellationToken)
    {
        var setting = await repository.GetAsync(cancellationToken);
        if (setting is null)
            return envAccessor.Current;

        return new WhatsAppChannelOptions
        {
            BaseUrl = setting.BaseUrl,
            ApiKey = UnprotectQuietly(setting.ApiKeyEncrypted),
            InstanceId = setting.InstanceId,
            GroupId = setting.GroupId ?? string.Empty,
            Phone = setting.Phone ?? string.Empty,
            NotificationsEnabled = setting.NotificationsEnabled
        };
    }

    private string UnprotectQuietly(string apiKeyEncrypted)
    {
        if (string.IsNullOrEmpty(apiKeyEncrypted))
            return string.Empty;

        try
        {
            return protector.Unprotect(apiKeyEncrypted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao decifrar a ApiKey WhatsApp persistida");
            return string.Empty;
        }
    }
}

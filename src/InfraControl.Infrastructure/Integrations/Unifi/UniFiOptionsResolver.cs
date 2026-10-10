using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Integrations.Unifi;

/// <summary>
/// F26 Fase 2.2: opções efetivas (banco → env). Falha ao decifrar a senha
/// persistida vira string vazia + log de erro (a chamada seguinte ao
/// controlador falha com erro de credencial identificável, nunca com
/// exception opaca de criptografia).
/// </summary>
public sealed class UniFiOptionsResolver(
    IUniFiSettingsRepository repository,
    IUnifiEnvOptionsAccessor envAccessor,
    ICredentialProtector protector,
    ILogger<UniFiOptionsResolver> logger)
    : IUniFiOptionsResolver
{
    public async Task<UnifiIntegrationOptions> ResolveAsync(
        CancellationToken cancellationToken)
    {
        var setting = await repository.GetAsync(cancellationToken);
        if (setting is null)
            return envAccessor.Current;

        return new UnifiIntegrationOptions
        {
            BaseUrl = setting.BaseUrl,
            Username = setting.Username,
            Password = UnprotectQuietly(setting.PasswordEncrypted),
            Site = setting.Site,
            AllowInvalidTlsCertificate = setting.AllowInvalidTlsCertificate
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
            logger.LogError(ex, "Falha ao decifrar a senha UniFi persistida");
            return string.Empty;
        }
    }
}

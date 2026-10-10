using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Integrations.Zabbix;

/// <summary>
/// F27 Fase 2.2: opções efetivas (banco → env). Falha ao decifrar a senha
/// persistida vira string vazia + log de erro (a chamada seguinte à API
/// falha com erro de credencial identificável, nunca com exception opaca
/// de criptografia).
/// </summary>
public sealed class ZabbixOptionsResolver(
    IZabbixSettingsRepository repository,
    IZabbixApiOptionsAccessor envAccessor,
    ICredentialProtector protector,
    ILogger<ZabbixOptionsResolver> logger)
    : IZabbixOptionsResolver
{
    public async Task<ZabbixIntegrationOptions> ResolveAsync(
        CancellationToken cancellationToken)
    {
        var setting = await repository.GetAsync(cancellationToken);
        if (setting is null)
            return envAccessor.Current;

        return new ZabbixIntegrationOptions
        {
            ApiUrl = setting.ApiUrl,
            Username = setting.Username,
            Password = UnprotectQuietly(setting.PasswordEncrypted)
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
            logger.LogError(ex, "Falha ao decifrar a senha da API Zabbix persistida");
            return string.Empty;
        }
    }
}

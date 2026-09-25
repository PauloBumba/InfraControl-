using InfraControl.Application.RemoteAccess.Sessions.Ports;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.RemoteAccess;

/// <summary>
/// Fonte provisória de credencial SSH a partir de RemoteAccess:Ssh (F11-T002).
/// Sem credencial configurada, falha com mensagem pt-BR — nunca retorna
/// credencial vazia. F11-T006 substitui esta implementação.
/// </summary>
public sealed class ConfigurationRemoteCredentialSource(
    IOptions<RemoteSshOptions> options)
    : IRemoteCredentialSource
{
    public Task<RemoteCredential> GetCredentialAsync(
        Guid deviceId, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.Username)
            || string.IsNullOrWhiteSpace(settings.Password))
            throw new InvalidOperationException(
                "Credencial SSH não configurada. Defina RemoteAccess:Ssh:Username e RemoteAccess:Ssh:Password.");

        return Task.FromResult(
            new RemoteCredential(settings.Username, settings.Password));
    }
}

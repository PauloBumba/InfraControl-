using InfraControl.Application.RemoteAccess.Sessions.Ports;
using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace InfraControl.Infrastructure.RemoteAccess;

/// <summary>
/// Provider SSH real via SSH.NET (F11-T002, ADR-013): abre sessão sem
/// terminal interativo — só conexão autenticada, que fica registrada na
/// sessão até o encerramento. Erros de conexão viram RemoteConnectResult
/// com mensagem pt-BR; credencial nunca aparece no erro.
/// </summary>
public sealed class SshRemoteProvider(IOptions<RemoteSshOptions> options)
    : IRemoteProvider
{
    public string Name => "SSH";

    public bool CanHandle(string providerKey) =>
        string.Equals(providerKey, "ssh", StringComparison.OrdinalIgnoreCase);

    public async Task<RemoteConnectResult> ConnectAsync(
        string host,
        RemoteCredential credential,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var client = new SshClient(
            host, settings.Port, credential.Username, credential.Password);
        client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);

        try
        {
            // SSH.NET 2026.0.0 expõe apenas Connect() síncrono — roda fora
            // do thread do request e respeita cancelamento antes/depois.
            await Task.Run(() => client.Connect(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return new RemoteConnectResult(true, client, null);
        }
        catch (OperationCanceledException)
        {
            client.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            client.Dispose();
            return new RemoteConnectResult(
                false, null, $"Falha ao conectar via SSH: {Sanitize(exception.Message, credential)}");
        }
    }

    private static string Sanitize(string message, RemoteCredential credential)
    {
        var sanitized = message;
        if (!string.IsNullOrEmpty(credential.Password))
            sanitized = sanitized.Replace(credential.Password, "***", StringComparison.Ordinal);
        return sanitized;
    }
}

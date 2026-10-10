using InfraControl.Infrastructure.NetworkServices.Ports;
using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace InfraControl.Infrastructure.NetworkServices;

/// <summary>
/// Execução SSH real via SSH.NET (F18-T005 · ADR-042 §3) — MESMA
/// biblioteca do `SshRemoteProvider` (ADR-013). Só conexão autenticada +
/// execução de UM comando de leitura por chamada, na porta/timeout da
/// config. Qualquer falha de conexão/execução vira
/// `InvalidOperationException` com motivo pt-BR (→ 503 na API, nunca
/// 500 silencioso) e a senha jamais aparece no erro.
/// </summary>
public sealed class SshNetCommandRunner(
    IOptions<NetworkServicesOptions> options) : ISshCommandRunner
{
    public async Task<string> ExecuteAsync(
        string command, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        if (!config.IsConfigured)
            throw new InvalidOperationException(
                "Serviços de rede não configurados (NetworkServices:Target).");

        try
        {
            // SSH.NET é síncrono por natureza — bloqueio isolado fora do
            // thread da requisição (mesmo cuidado dos demais clients).
            return await Task.Run(() =>
            {
                using var client = new SshClient(
                    config.Target, config.SshPort,
                    config.SshUsername, config.SshPassword);
                client.ConnectionInfo.Timeout =
                    TimeSpan.FromSeconds(config.TimeoutSeconds);
                client.Connect();

                using var sshCommand = client.CreateCommand(command);
                return sshCommand.Execute() ?? string.Empty;
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Inclui a mensagem original do SSH.NET (ex.: "Permission
            // denied") — ela nunca contém a senha, que só existe na config.
            throw new InvalidOperationException(
                $"Equipamento {config.Target} indisponível ou inacessível via SSH " +
                $"(porta {config.SshPort}): {exception.Message}",
                exception);
        }
    }
}

using System.Net.Sockets;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Implementação real do probe TCP (F5-T006) via TcpClient: conecta na
/// porta e reporta sucesso só quando o handshake completa. Timeout por
/// CancellationToken; erro de rede vira TcpProbeReply Success=false.
/// </summary>
public sealed class SystemNetTcpProbeClient : ITcpProbeClient
{
    public async Task<TcpProbeReply> SendAsync(
        string target,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(target, port, timeoutSource.Token);
            return new TcpProbeReply(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new TcpProbeReply(false, "Tempo esgotado.");
        }
        catch (Exception exception)
        {
            return new TcpProbeReply(
                false,
                exception.Message.Length <= 500
                    ? exception.Message
                    : exception.Message[..500]);
        }
    }
}

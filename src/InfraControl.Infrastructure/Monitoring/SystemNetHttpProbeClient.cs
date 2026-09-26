namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Implementação real do probe HTTP (F5-T005) via HttpClient estático.
/// Valida o certificado TLS (decisão aprovada: self-signed = falha);
/// qualquer resposta HTTP recebida conta como sucesso, com ou sem corpo
/// (ResponseHeadersRead — só o status interessa). Timeout por chamada via
/// CancellationToken; erro de rede vira HttpProbeReply com Success=false.
/// </summary>
public sealed class SystemNetHttpProbeClient : IHttpProbeClient
{
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    public async Task<HttpProbeReply> SendAsync(
        string url,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            using var response = await Client.GetAsync(
                url, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);
            return new HttpProbeReply(true, (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new HttpProbeReply(false, null, "Tempo esgotado.");
        }
        catch (Exception exception)
        {
            return new HttpProbeReply(
                false, null, Truncate(exception.Message));
        }
    }

    private static string Truncate(string message)
        => message.Length <= 500 ? message : message[..500];
}

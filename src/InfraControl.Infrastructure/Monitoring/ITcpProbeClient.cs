namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Seam de TCP para o TcpMonitoringProvider (F5-T006) — permite testar
/// o provider sem rede real.
/// </summary>
public interface ITcpProbeClient
{
    Task<TcpProbeReply> SendAsync(
        string target,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resposta bruta de um probe TCP. Success = handshake TCP completou
/// (porta aberta); Error detalha a falha (timeout/recusa) quando não.
/// </summary>
public sealed record TcpProbeReply(bool Success, string? Error = null);

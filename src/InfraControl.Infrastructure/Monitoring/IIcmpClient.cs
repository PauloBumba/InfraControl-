namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Seam de ICMP para o PingMonitoringProvider (F5-T003) — permite testar o
/// provider sem rede real.
/// </summary>
public interface IIcmpClient
{
    Task<IcmpReply> SendAsync(
        string target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resposta bruta de um ping ICMP. RoundtripMs só é confiável quando Success.
/// </summary>
public sealed record IcmpReply(bool Success, long RoundtripMs, string? Error = null);

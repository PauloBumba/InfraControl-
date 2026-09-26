namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Seam de HTTP para o HttpWanMonitoringProvider (F5-T005) — permite
/// testar o provider sem rede real.
/// </summary>
public interface IHttpProbeClient
{
    Task<HttpProbeReply> SendAsync(
        string url,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resposta bruta de um probe HTTP. Success = qualquer resposta HTTP
/// recebida (a validação TLS é do client real: cert inválido = falha);
/// StatusCode só é confiável quando Success.
/// </summary>
public sealed record HttpProbeReply(bool Success, int? StatusCode, string? Error = null);

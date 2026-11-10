using InfraControl.Application.Operations.Cluster.Ports;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Probe de health das instâncias do cluster sobre o
/// <see cref="IHttpProbeClient"/> existente (TLS validado, timeout por
/// chamada). Saudável = resposta HTTP 2xx/3xx — endpoints de health
/// devolvem 503 quando degradado.
/// </summary>
public sealed class ServiceHealthProbe(IHttpProbeClient probeClient)
    : IServiceHealthProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);

    public async Task<bool> IsHealthyAsync(
        string healthUrl, CancellationToken cancellationToken = default)
    {
        var reply = await probeClient.SendAsync(healthUrl, Timeout, cancellationToken);
        return reply.Success && reply.StatusCode is >= 200 and < 400;
    }
}

using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.IntegrationTests.Common;

/// <summary>
/// Probe HTTP falso (F5-T005): sempre responde 200 — nenhum teste de
/// integração deve abrir conexão de rede real.
/// </summary>
public sealed class FakeHttpProbeClient : IHttpProbeClient
{
    public Task<HttpProbeReply> SendAsync(
        string url, TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.FromResult(new HttpProbeReply(Success: true, StatusCode: 200));
}

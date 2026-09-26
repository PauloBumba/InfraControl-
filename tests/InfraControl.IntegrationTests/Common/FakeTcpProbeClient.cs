using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.IntegrationTests.Common;

/// <summary>
/// Probe TCP falso (F5-T006): sempre reporta porta aberta — nenhum
/// teste de integração deve abrir conexão de rede real.
/// </summary>
public sealed class FakeTcpProbeClient : ITcpProbeClient
{
    public Task<TcpProbeReply> SendAsync(
        string target,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new TcpProbeReply(Success: true));
}

using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.IntegrationTests.Common;

/// <summary>
/// ICMP determinístico para integration tests — nenhum teste usa rede real.
/// </summary>
public sealed class FakeIcmpClient : IIcmpClient
{
    public Task<IcmpReply> SendAsync(
        string target, TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.FromResult(new IcmpReply(Success: true, RoundtripMs: 5));
}

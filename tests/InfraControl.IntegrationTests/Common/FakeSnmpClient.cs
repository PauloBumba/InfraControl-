using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.IntegrationTests.Common;

/// <summary>
/// Seam SNMP falsa para os testes — system group de amostra (F4-T004),
/// sem rede real.
/// </summary>
public sealed class FakeSnmpClient : ISnmpClient
{
    public IReadOnlyList<SnmpVariable> Variables { get; set; } =
    [
        new("1.3.6.1.2.1.1.1.0", "Cisco IOS XE Software"),
        new("1.3.6.1.2.1.1.3.0", "123456"),
        new("1.3.6.1.2.1.1.5.0", "sw-core-01"),
        new("1.3.6.1.2.1.1.6.0", "Data Center A"),
        new("1.3.6.1.2.1.1.4.0", "noc")
    ];

    public Task<IReadOnlyList<SnmpVariable>> WalkAsync(
        string target,
        string oid,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Variables);
}

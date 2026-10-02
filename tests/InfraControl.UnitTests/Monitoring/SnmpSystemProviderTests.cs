using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// Fase 0: SnmpSystemMonitoringProvider (MIB-II system group 1.3.6.1.2.1.1)
/// — walk de amostra via seam ISnmpClient, sem rede real.
/// </summary>
public sealed class SnmpSystemProviderTests
{
    private static IReadOnlyList<SnmpVariable> SampleSystemGroup() =>
    [
        new("1.3.6.1.2.1.1.1.0", "Cisco IOS XE Software"),
        new("1.3.6.1.2.1.1.3.0", "123456"),
        new("1.3.6.1.2.1.1.5.0", "sw-core-01")
    ];

    private sealed class FakeSnmpClient(Func<string, IReadOnlyList<SnmpVariable>> walkFactory) : ISnmpClient
    {
        public string? LastOid { get; private set; }

        public Task<IReadOnlyList<SnmpVariable>> WalkAsync(
            string target,
            string oid,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            LastOid = oid;
            return Task.FromResult(walkFactory(oid));
        }
    }

    private sealed class ThrowingSnmpClient : ISnmpClient
    {
        public Task<IReadOnlyList<SnmpVariable>> WalkAsync(
            string target,
            string oid,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
            => throw new TimeoutException("SNMP timeout simulado.");
    }

    [Fact]
    public void Provider_NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new SnmpSystemMonitoringProvider(new FakeSnmpClient(_ => SampleSystemGroup()));

        provider.Name.Should().Be("SnmpSystem");
        provider.CanHandle("SnmpSystem").Should().BeTrue();
        provider.CanHandle("snmpsystem").Should().BeTrue();
        provider.CanHandle("SnmpToner").Should().BeFalse();
    }

    [Fact]
    public async Task Success_ShouldWalkSystemGroupAndReturnValues()
    {
        var client = new FakeSnmpClient(_ => SampleSystemGroup());
        var provider = new SnmpSystemMonitoringProvider(client);

        var result = await provider.CheckAsync("192.168.1.10");

        client.LastOid.Should().Be(SnmpSystemMonitoringProvider.SystemGroupOid);
        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Values.Should().ContainKey("sysDescr").WhoseValue.Should().Be("Cisco IOS XE Software");
        result.Values.Should().ContainKey("sysName").WhoseValue.Should().Be("sw-core-01");
        result.Values.Should().ContainKey("sysUpTime").WhoseValue.Should().Be("123456");
        // 123456 centésimos de segundo → 1234 s
        result.Values.Should().ContainKey("uptimeSeconds").WhoseValue.Should().Be("1234");
    }

    [Fact]
    public async Task PartialResponse_ShouldStillSucceed_WithAvailableKeys()
    {
        var provider = new SnmpSystemMonitoringProvider(
            new FakeSnmpClient(_ => [new SnmpVariable("1.3.6.1.2.1.1.5.0", "fw-edge-01")]));

        var result = await provider.CheckAsync("10.0.0.1");

        result.Succeeded.Should().BeTrue();
        result.Values.Should().ContainKey("sysName").WhoseValue.Should().Be("fw-edge-01");
        result.Values.Should().NotContainKey("sysDescr");
    }

    [Fact]
    public async Task EmptyWalk_ShouldFail_WithGroupMessage()
    {
        var provider = new SnmpSystemMonitoringProvider(new FakeSnmpClient(_ => []));

        var result = await provider.CheckAsync("192.168.1.10");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("grupo system");
    }

    [Fact]
    public async Task SnmpFailure_ShouldFail_WithFriendlyMessage()
    {
        var provider = new SnmpSystemMonitoringProvider(new ThrowingSnmpClient());

        var result = await provider.CheckAsync("192.168.1.10");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Falha ao consultar SNMP:");
    }

    [Fact]
    public async Task EmptyTarget_ShouldFail_WithoutCallingSnmp()
    {
        var client = new FakeSnmpClient(_ => SampleSystemGroup());
        var provider = new SnmpSystemMonitoringProvider(client);

        var result = await provider.CheckAsync("  ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("Destino");
        client.LastOid.Should().BeNull();
    }
}

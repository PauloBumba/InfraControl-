using FluentAssertions;
using InfraControl.Infrastructure.Discovery;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Discovery;

/// <summary>
/// F4-T003: provider ICMP de discovery — sonda o IP do device com a seam
/// de ICMP já existente (mesma do PingMonitoringProvider, sem rede real).
/// </summary>
public sealed class IcmpDiscoveryProviderTests
{
    private sealed class FakeIcmpClient(IcmpReply reply) : IIcmpClient
    {
        public int Calls { get; private set; }

        public Task<IcmpReply> SendAsync(
            string target,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(reply);
        }
    }

    [Fact]
    public void Name_ShouldBeIcmp()
        => new IcmpDiscoveryProvider(new FakeIcmpClient(new IcmpReply(true, 1)))
            .Name.Should().Be("ICMP");

    [Theory]
    [InlineData("Icmp")]
    [InlineData("icmp")]
    [InlineData("ICMP")]
    public void CanHandle_IcmpKey_ShouldBeTrue(string key)
        => new IcmpDiscoveryProvider(new FakeIcmpClient(new IcmpReply(true, 1)))
            .CanHandle(key).Should().BeTrue();

    [Theory]
    [InlineData("Snmp")]
    [InlineData("Lldp")]
    [InlineData("")]
    public void CanHandle_OtherKey_ShouldBeFalse(string key)
        => new IcmpDiscoveryProvider(new FakeIcmpClient(new IcmpReply(true, 1)))
            .CanHandle(key).Should().BeFalse();

    [Fact]
    public async Task DiscoverAsync_Success_ShouldReturnReachableAttributesAndSummary()
    {
        var icmp = new FakeIcmpClient(new IcmpReply(true, 5));
        var provider = new IcmpDiscoveryProvider(icmp);

        var result = await provider.DiscoverAsync("192.0.2.10");

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Summary.Should().Contain("acessível").And.Contain("192.0.2.10");
        result.Attributes!["reachable"].Should().Be("true");
        result.Attributes!["roundtrip_ms"].Should().Be("5");
        icmp.Calls.Should().Be(1);
    }

    [Fact]
    public async Task DiscoverAsync_Failure_ShouldReturnUnreachableWithPtBrError()
    {
        var provider = new IcmpDiscoveryProvider(
            new FakeIcmpClient(new IcmpReply(false, 0, "Host inacessível.")));

        var result = await provider.DiscoverAsync("192.0.2.99");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Host inacessível.");
        result.Summary.Should().Contain("inacessível");
        result.Attributes!["reachable"].Should().Be("false");
    }

    [Fact]
    public async Task DiscoverAsync_EmptyTarget_ShouldFailWithoutCallingClient()
    {
        var icmp = new FakeIcmpClient(new IcmpReply(true, 1));
        var provider = new IcmpDiscoveryProvider(icmp);

        var result = await provider.DiscoverAsync("  ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino do ping é obrigatório.");
        icmp.Calls.Should().Be(0);
    }
}

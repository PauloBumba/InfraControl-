using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Discovery;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Discovery;

/// <summary>
/// F4-T004: provider SNMP de discovery — walk do system group MIB-II
/// (escopo aprovado: só system group, sem ifTable) com a seam
/// ISnmpClient existente (sem rede real nos testes).
/// </summary>
public sealed class SnmpDiscoveryProviderTests
{
    private sealed class FakeSnmpClient(
        Func<string, IReadOnlyList<SnmpVariable>> walkFactory) : ISnmpClient
    {
        public int Calls { get; private set; }
        public string? LastOid { get; private set; }

        public Task<IReadOnlyList<SnmpVariable>> WalkAsync(
            string target,
            string oid,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastOid = oid;
            return Task.FromResult(walkFactory(target));
        }
    }

    private sealed class ThrowingSnmpClient : ISnmpClient
    {
        public Task<IReadOnlyList<SnmpVariable>> WalkAsync(
            string target,
            string oid,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
            => throw new TimeoutException("timeout SNMP");
    }

    private static IReadOnlyList<SnmpVariable> SampleSystemGroup() =>
    [
        new("1.3.6.1.2.1.1.1.0", "Cisco IOS XE Software"),
        new("1.3.6.1.2.1.1.3.0", "123456"),
        new("1.3.6.1.2.1.1.5.0", "sw-core-01"),
        new("1.3.6.1.2.1.1.6.0", "Data Center A"),
        new("1.3.6.1.2.1.1.4.0", "noc")
    ];

    [Fact]
    public void Name_ShouldBeSnmp()
        => new SnmpDiscoveryProvider(new FakeSnmpClient(_ => SampleSystemGroup()))
            .Name.Should().Be("SNMP");

    [Theory]
    [InlineData("Snmp")]
    [InlineData("snmp")]
    [InlineData("SNMP")]
    public void CanHandle_SnmpKey_ShouldBeTrue(string key)
        => new SnmpDiscoveryProvider(new FakeSnmpClient(_ => SampleSystemGroup()))
            .CanHandle(key).Should().BeTrue();

    [Theory]
    [InlineData("Icmp")]
    [InlineData("Lldp")]
    [InlineData("")]
    public void CanHandle_OtherKey_ShouldBeFalse(string key)
        => new SnmpDiscoveryProvider(new FakeSnmpClient(_ => SampleSystemGroup()))
            .CanHandle(key).Should().BeFalse();

    [Fact]
    public async Task DiscoverAsync_Success_ShouldReturnSystemGroupAttributesAndSummary()
    {
        var client = new FakeSnmpClient(_ => SampleSystemGroup());
        var provider = new SnmpDiscoveryProvider(client);

        var findings = await provider.DiscoverAsync("192.0.2.60");

        var result = findings.Should().ContainSingle().Subject;
        result.Kind.Should().Be(DiscoveryResultKind.Host);
        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Summary.Should().Contain("sw-core-01").And.Contain("Cisco IOS XE Software");
        result.Attributes!["reachable"].Should().Be("true");
        result.Attributes!["sys_name"].Should().Be("sw-core-01");
        result.Attributes!["sys_descr"].Should().Be("Cisco IOS XE Software");
        result.Attributes!["sys_up_time"].Should().Be("123456");
        result.Attributes!["sys_location"].Should().Be("Data Center A");
        result.Attributes!["sys_contact"].Should().Be("noc");
        client.Calls.Should().Be(1);
        client.LastOid.Should().Be("1.3.6.1.2.1.1");
    }

    [Fact]
    public async Task DiscoverAsync_EmptyWalk_ShouldFailWithPtBrError()
    {
        var provider = new SnmpDiscoveryProvider(new FakeSnmpClient(_ => []));

        var findings = await provider.DiscoverAsync("192.0.2.60");

        var result = findings.Should().ContainSingle().Subject;
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("SNMP sem resposta.");
        result.Summary.Should().Contain("sem resposta");
        result.Attributes!["reachable"].Should().Be("false");
    }

    [Fact]
    public async Task DiscoverAsync_ClientThrows_ShouldFailWithPtBrError()
    {
        var provider = new SnmpDiscoveryProvider(new ThrowingSnmpClient());

        var findings = await provider.DiscoverAsync("192.0.2.60");

        var result = findings.Should().ContainSingle().Subject;
        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Falha ao consultar SNMP:");
        result.Summary.Should().Contain("indisponível");
        result.Attributes!["reachable"].Should().Be("false");
    }

    [Fact]
    public async Task DiscoverAsync_EmptyTarget_ShouldFailWithoutCallingClient()
    {
        var client = new FakeSnmpClient(_ => SampleSystemGroup());
        var provider = new SnmpDiscoveryProvider(client);

        var findings = await provider.DiscoverAsync(" ");

        var result = findings.Should().ContainSingle().Subject;
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino do SNMP é obrigatório.");
        client.Calls.Should().Be(0);
    }
}

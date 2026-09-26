using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Discovery;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Discovery;

/// <summary>
/// F4-T005: provider LLDP de discovery (decisões do usuário: leitura via
/// SNMP/LLDP-MIB; walk sem vizinhos → registrar "nenhum" como achado).
/// Vizinhos vêm da lldpRemTable (1.0.8802.1.1.2.1.4.1.1) agrupados por
/// índice —1 achado por vizinho, Kind=Neighbor.
/// </summary>
public sealed class LldpDiscoveryProviderTests
{
    private const string RemTable = "1.0.8802.1.1.2.1.4.1.1";

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
            => throw new TimeoutException("timeout");
    }

    // Index: timeMark.localPort.remIndex — dois vizinhos (remIndex 1 e 2).
    private static IReadOnlyList<SnmpVariable> SampleNeighbors() =>
    [
        new($"{RemTable}.7.0.11.1", "sw-core-01"),
        new($"{RemTable}.6.0.11.1", "Gi0/1"),
        new($"{RemTable}.3.0.11.1", "aa bb cc 00 00 01"),
        new($"{RemTable}.7.0.11.2", "sw-access-02"),
        new($"{RemTable}.5.0.11.2", "Gi1/0/24"),
        new($"{RemTable}.3.0.11.2", "aa bb cc 00 00 02")
    ];

    [Fact]
    public void Name_ShouldBeLldp()
        => new LldpDiscoveryProvider(new FakeSnmpClient(_ => SampleNeighbors()))
            .Name.Should().Be("LLDP");

    [Theory]
    [InlineData("Lldp")]
    [InlineData("lldp")]
    [InlineData("LLDP")]
    public void CanHandle_LldpKey_ShouldBeTrue(string key)
        => new LldpDiscoveryProvider(new FakeSnmpClient(_ => SampleNeighbors()))
            .CanHandle(key).Should().BeTrue();

    [Theory]
    [InlineData("Cdp")]
    [InlineData("Snmp")]
    [InlineData("")]
    public void CanHandle_OtherKey_ShouldBeFalse(string key)
        => new LldpDiscoveryProvider(new FakeSnmpClient(_ => SampleNeighbors()))
            .CanHandle(key).Should().BeFalse();

    [Fact]
    public async Task DiscoverAsync_TwoNeighbors_ShouldReturnOneFindingEach()
    {
        var client = new FakeSnmpClient(_ => SampleNeighbors());
        var provider = new LldpDiscoveryProvider(client);

        var findings = await provider.DiscoverAsync("192.0.2.80");

        findings.Should().HaveCount(2);
        findings.Should().OnlyContain(f => f.Kind == DiscoveryResultKind.Neighbor);

        var first = findings.Single(f => f.Summary!.Contains("sw-core-01"));
        first.Succeeded.Should().BeTrue();
        first.Summary.Should().Be("sw-core-01 — Gi0/1");
        first.Attributes!["sys_name"].Should().Be("sw-core-01");
        first.Attributes!["port_desc"].Should().Be("Gi0/1");
        first.Attributes!["chassis_id"].Should().Be("aa bb cc 00 00 01");
        first.Attributes!["local_port"].Should().Be("11");

        var second = findings.Single(f => f.Summary!.Contains("sw-access-02"));
        second.Summary.Should().Be("sw-access-02 — Gi1/0/24");
        second.Attributes!["port_id"].Should().Be("Gi1/0/24");

        client.Calls.Should().Be(1);
        client.LastOid.Should().Be(RemTable);
    }

    [Fact]
    public async Task DiscoverAsync_NeighborWithoutPort_ShouldSummarizeByNameOnly()
    {
        var provider = new LldpDiscoveryProvider(new FakeSnmpClient(_ =>
        [
            new SnmpVariable($"{RemTable}.7.0.3.9", "sw-solo-01")
        ]));

        var findings = await provider.DiscoverAsync("192.0.2.80");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Summary.Should().Be("sw-solo-01.");
        finding.Attributes!["local_port"].Should().Be("3");
    }

    [Fact]
    public async Task DiscoverAsync_EmptyWalk_ShouldRegisterNoNeighborsFinding()
    {
        var provider = new LldpDiscoveryProvider(new FakeSnmpClient(_ => []));

        var findings = await provider.DiscoverAsync("192.0.2.80");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Kind.Should().Be(DiscoveryResultKind.Neighbor);
        finding.Succeeded.Should().BeFalse();
        finding.Summary.Should().Be("Nenhum vizinho LLDP.");
        finding.Attributes!["neighbors"].Should().Be("0");
    }

    [Fact]
    public async Task DiscoverAsync_ClientThrows_ShouldFailWithPtBrError()
    {
        var provider = new LldpDiscoveryProvider(new ThrowingSnmpClient());

        var findings = await provider.DiscoverAsync("192.0.2.80");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Succeeded.Should().BeFalse();
        finding.Error.Should().StartWith("Falha ao consultar LLDP:");
        finding.Summary.Should().Contain("indisponível");
        finding.Attributes!["reachable"].Should().Be("false");
    }

    [Fact]
    public async Task DiscoverAsync_EmptyTarget_ShouldFailWithoutCallingClient()
    {
        var client = new FakeSnmpClient(_ => SampleNeighbors());
        var provider = new LldpDiscoveryProvider(client);

        var findings = await provider.DiscoverAsync("");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Succeeded.Should().BeFalse();
        finding.Error.Should().Be("Destino do LLDP é obrigatório.");
        client.Calls.Should().Be(0);
    }
}

using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Discovery;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Discovery;

/// <summary>
/// F4-T006: provider CDP de discovery — walk da cdpCacheTable
/// (CISCO-CDP-MIB, 1.3.6.1.4.1.9.9.23.1.2.1.1) via SNMP, mesmo padrão
/// aprovado no F4-T005: 1 achado por vizinho (Kind=Neighbor) e walk
/// vazio registra "nenhum". Índice da tabela: ifIndex.deviceIndex.
/// </summary>
public sealed class CdpDiscoveryProviderTests
{
    private const string CacheTable = "1.3.6.1.4.1.9.9.23.1.2.1.1";

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

    // Índice: ifIndex.deviceIndex — dois vizinhos (11.1 e 12.3).
    private static IReadOnlyList<SnmpVariable> SampleNeighbors() =>
    [
        new($"{CacheTable}.6.11.1", "sw-cisco-01"),
        new($"{CacheTable}.7.11.1", "GigabitEthernet0/1"),
        new($"{CacheTable}.4.11.1", "c0.01.02.03.04.01"),
        new($"{CacheTable}.5.11.1", "Cisco IOS Software"),
        new($"{CacheTable}.6.12.3", "sw-cisco-02"),
        new($"{CacheTable}.7.12.3", "Gi1/0/24")
    ];

    [Fact]
    public void Name_ShouldBeCdp()
        => new CdpDiscoveryProvider(new FakeSnmpClient(_ => SampleNeighbors()))
            .Name.Should().Be("CDP");

    [Theory]
    [InlineData("Cdp")]
    [InlineData("cdp")]
    [InlineData("CDP")]
    public void CanHandle_CdpKey_ShouldBeTrue(string key)
        => new CdpDiscoveryProvider(new FakeSnmpClient(_ => SampleNeighbors()))
            .CanHandle(key).Should().BeTrue();

    [Theory]
    [InlineData("Lldp")]
    [InlineData("Snmp")]
    [InlineData("")]
    public void CanHandle_OtherKey_ShouldBeFalse(string key)
        => new CdpDiscoveryProvider(new FakeSnmpClient(_ => SampleNeighbors()))
            .CanHandle(key).Should().BeFalse();

    [Fact]
    public async Task DiscoverAsync_TwoNeighbors_ShouldReturnOneFindingEach()
    {
        var client = new FakeSnmpClient(_ => SampleNeighbors());
        var provider = new CdpDiscoveryProvider(client);

        var findings = await provider.DiscoverAsync("192.0.2.90");

        findings.Should().HaveCount(2);
        findings.Should().OnlyContain(f => f.Kind == DiscoveryResultKind.Neighbor);

        var first = findings.Single(f => f.Summary!.Contains("sw-cisco-01"));
        first.Succeeded.Should().BeTrue();
        first.Summary.Should().Be("sw-cisco-01 — GigabitEthernet0/1");
        first.Attributes!["device_id"].Should().Be("sw-cisco-01");
        first.Attributes!["device_port"].Should().Be("GigabitEthernet0/1");
        first.Attributes!["address"].Should().Be("c0.01.02.03.04.01");
        first.Attributes!["local_if"].Should().Be("11");

        var second = findings.Single(f => f.Summary!.Contains("sw-cisco-02"));
        second.Summary.Should().Be("sw-cisco-02 — Gi1/0/24");

        client.Calls.Should().Be(1);
        client.LastOid.Should().Be(CacheTable);
    }

    [Fact]
    public async Task DiscoverAsync_NeighborWithoutPort_ShouldSummarizeByIdOnly()
    {
        var provider = new CdpDiscoveryProvider(new FakeSnmpClient(_ =>
            [new SnmpVariable($"{CacheTable}.6.4.9", "sw-solo-01")]));

        var findings = await provider.DiscoverAsync("192.0.2.90");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Summary.Should().Be("sw-solo-01.");
        finding.Attributes!["local_if"].Should().Be("4");
    }

    [Fact]
    public async Task DiscoverAsync_EmptyWalk_ShouldRegisterNoNeighborsFinding()
    {
        var provider = new CdpDiscoveryProvider(new FakeSnmpClient(_ => []));

        var findings = await provider.DiscoverAsync("192.0.2.90");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Kind.Should().Be(DiscoveryResultKind.Neighbor);
        finding.Succeeded.Should().BeFalse();
        finding.Summary.Should().Be("Nenhum vizinho CDP.");
        finding.Attributes!["neighbors"].Should().Be("0");
    }

    [Fact]
    public async Task DiscoverAsync_ClientThrows_ShouldFailWithPtBrError()
    {
        var provider = new CdpDiscoveryProvider(new ThrowingSnmpClient());

        var findings = await provider.DiscoverAsync("192.0.2.90");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Succeeded.Should().BeFalse();
        finding.Error.Should().StartWith("Falha ao consultar CDP:");
        finding.Summary.Should().Contain("indisponível");
        finding.Attributes!["reachable"].Should().Be("false");
    }

    [Fact]
    public async Task DiscoverAsync_EmptyTarget_ShouldFailWithoutCallingClient()
    {
        var client = new FakeSnmpClient(_ => SampleNeighbors());
        var provider = new CdpDiscoveryProvider(client);

        var findings = await provider.DiscoverAsync("");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Succeeded.Should().BeFalse();
        finding.Error.Should().Be("Destino do CDP é obrigatório.");
        client.Calls.Should().Be(0);
    }
}

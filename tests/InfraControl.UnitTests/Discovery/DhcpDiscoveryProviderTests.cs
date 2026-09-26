using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Discovery;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Discovery;

/// <summary>
/// F4-T008: provider DHCP de discovery — walk da cdsBindingsTable
/// (CISCO-DHCP-SNOOPING-MIB, 1.3.6.1.4.1.9.9.380.1.4.1) via SNMP.
/// Escopo aprovado: bindings de DHCP snooping (vendor Cisco, mesmo
/// natureza do CDP). Índice: vlan + mac (octetos). 1 achado
/// Kind=DhcpLease por binding.
/// </summary>
public sealed class DhcpDiscoveryProviderTests
{
    private const string BindingsTable = "1.3.6.1.4.1.9.9.380.1.4.1";

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

    // Índice: vlan + mac em octetos (aa:bb:cc:dd:ee:01 = 170.187.204.221.238.1).
    private static IReadOnlyList<SnmpVariable> SampleBindings() =>
    [
        new($"{BindingsTable}.1.10.170.187.204.221.238.1", "10"),
        new($"{BindingsTable}.2.10.170.187.204.221.238.1", "aa:bb:cc:dd:ee:01"),
        new($"{BindingsTable}.4.10.170.187.204.221.238.1", "192.0.2.10"),
        new($"{BindingsTable}.5.10.170.187.204.221.238.1", "11"),
        new($"{BindingsTable}.8.10.170.187.204.221.238.1", "pc-01"),
        new($"{BindingsTable}.1.20.170.187.204.221.238.2", "20"),
        new($"{BindingsTable}.2.20.170.187.204.221.238.2", "aa:bb:cc:dd:ee:02"),
        new($"{BindingsTable}.4.20.170.187.204.221.238.2", "192.0.2.20"),
        new($"{BindingsTable}.5.20.170.187.204.221.238.2", "12"),
        new($"{BindingsTable}.8.20.170.187.204.221.238.2", "pc-02")
    ];

    [Fact]
    public void Name_ShouldBeDhcp()
        => new DhcpDiscoveryProvider(new FakeSnmpClient(_ => SampleBindings()))
            .Name.Should().Be("DHCP");

    [Theory]
    [InlineData("Dhcp")]
    [InlineData("dhcp")]
    [InlineData("DHCP")]
    public void CanHandle_DhcpKey_ShouldBeTrue(string key)
        => new DhcpDiscoveryProvider(new FakeSnmpClient(_ => SampleBindings()))
            .CanHandle(key).Should().BeTrue();

    [Theory]
    [InlineData("Icmp")]
    [InlineData("Arp")]
    [InlineData("")]
    public void CanHandle_OtherKey_ShouldBeFalse(string key)
        => new DhcpDiscoveryProvider(new FakeSnmpClient(_ => SampleBindings()))
            .CanHandle(key).Should().BeFalse();

    [Fact]
    public async Task DiscoverAsync_TwoBindings_ShouldReturnOneFindingEach()
    {
        var client = new FakeSnmpClient(_ => SampleBindings());
        var provider = new DhcpDiscoveryProvider(client);

        var findings = await provider.DiscoverAsync("192.0.2.1");

        findings.Should().HaveCount(2);
        findings.Should().OnlyContain(f => f.Kind == DiscoveryResultKind.DhcpLease);

        var first = findings.Single(f => f.Summary!.Contains("192.0.2.10"));
        first.Succeeded.Should().BeTrue();
        first.Summary.Should().Be("192.0.2.10 — aa:bb:cc:dd:ee:01");
        first.Attributes!["ip"].Should().Be("192.0.2.10");
        first.Attributes!["mac"].Should().Be("aa:bb:cc:dd:ee:01");
        first.Attributes!["vlan"].Should().Be("10");
        first.Attributes!["local_if"].Should().Be("11");
        first.Attributes!["hostname"].Should().Be("pc-01");

        var second = findings.Single(f => f.Summary!.Contains("192.0.2.20"));
        second.Attributes!["vlan"].Should().Be("20");
        second.Attributes!["local_if"].Should().Be("12");

        client.Calls.Should().Be(1);
        client.LastOid.Should().Be(BindingsTable);
    }

    [Fact]
    public async Task DiscoverAsync_EntryWithoutIp_ShouldBeDiscarded()
    {
        var provider = new DhcpDiscoveryProvider(new FakeSnmpClient(_ =>
            [
                new SnmpVariable($"{BindingsTable}.2.10.170.187.204.221.238.9", "aa:bb:cc:dd:ee:09"),
                new SnmpVariable($"{BindingsTable}.4.10.170.187.204.221.238.1", "192.0.2.10"),
                new SnmpVariable($"{BindingsTable}.2.10.170.187.204.221.238.1", "aa:bb:cc:dd:ee:01")
            ]));

        var findings = await provider.DiscoverAsync("192.0.2.1");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Attributes!["ip"].Should().Be("192.0.2.10");
        finding.Attributes!["mac"].Should().Be("aa:bb:cc:dd:ee:01");
    }

    [Fact]
    public async Task DiscoverAsync_EntryWithoutMac_ShouldSummarizeByIpOnly()
    {
        var provider = new DhcpDiscoveryProvider(new FakeSnmpClient(_ =>
            [new SnmpVariable($"{BindingsTable}.4.10.170.187.204.221.238.1", "192.0.2.10")]));

        var findings = await provider.DiscoverAsync("192.0.2.1");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Summary.Should().Be("192.0.2.10.");
        finding.Attributes!["ip"].Should().Be("192.0.2.10");
    }

    [Fact]
    public async Task DiscoverAsync_EmptyWalk_ShouldRegisterNoBindingsFinding()
    {
        var provider = new DhcpDiscoveryProvider(new FakeSnmpClient(_ => []));

        var findings = await provider.DiscoverAsync("192.0.2.1");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Kind.Should().Be(DiscoveryResultKind.DhcpLease);
        finding.Succeeded.Should().BeFalse();
        finding.Summary.Should().Be("Nenhuma entrada DHCP.");
        finding.Attributes!["dhcp_bindings"].Should().Be("0");
    }

    [Fact]
    public async Task DiscoverAsync_ClientThrows_ShouldFailWithPtBrError()
    {
        var provider = new DhcpDiscoveryProvider(new ThrowingSnmpClient());

        var findings = await provider.DiscoverAsync("192.0.2.1");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Succeeded.Should().BeFalse();
        finding.Error.Should().StartWith("Falha ao consultar DHCP:");
        finding.Summary.Should().Contain("indisponível");
        finding.Attributes!["reachable"].Should().Be("false");
    }

    [Fact]
    public async Task DiscoverAsync_EmptyTarget_ShouldFailWithoutCallingClient()
    {
        var client = new FakeSnmpClient(_ => SampleBindings());
        var provider = new DhcpDiscoveryProvider(client);

        var findings = await provider.DiscoverAsync("");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Succeeded.Should().BeFalse();
        finding.Error.Should().Be("Destino do DHCP é obrigatório.");
        client.Calls.Should().Be(0);
    }
}

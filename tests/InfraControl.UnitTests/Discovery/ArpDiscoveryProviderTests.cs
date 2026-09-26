using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Discovery;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Discovery;

/// <summary>
/// F4-T007: provider ARP de discovery — walk do ipNetToMediaTable
/// (IP-MIB, 1.3.6.1.2.1.4.22.1) via SNMP. Decisões aprovadas: 1 achado
/// por entrada (Kind=ArpEntry) e sem filtro de tipo (todas as entradas).
/// Índice: ifIndex.a.b.c.d.
/// </summary>
public sealed class ArpDiscoveryProviderTests
{
    private const string MediaTable = "1.3.6.1.2.1.4.22.1";

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

    // Índice: ifIndex.a.b.c.d — duas entradas (dynamic + manual).
    private static IReadOnlyList<SnmpVariable> SampleEntries() =>
    [
        new($"{MediaTable}.1.11.192.0.2.10", "11"),
        new($"{MediaTable}.2.11.192.0.2.10", "aa:bb:cc:dd:ee:01"),
        new($"{MediaTable}.3.11.192.0.2.10", "192.0.2.10"),
        new($"{MediaTable}.4.11.192.0.2.10", "3"),
        new($"{MediaTable}.1.12.192.0.2.20", "12"),
        new($"{MediaTable}.2.12.192.0.2.20", "aa:bb:cc:dd:ee:02"),
        new($"{MediaTable}.3.12.192.0.2.20", "192.0.2.20"),
        new($"{MediaTable}.4.12.192.0.2.20", "4")
    ];

    [Fact]
    public void Name_ShouldBeArp()
        => new ArpDiscoveryProvider(new FakeSnmpClient(_ => SampleEntries()))
            .Name.Should().Be("ARP");

    [Theory]
    [InlineData("Arp")]
    [InlineData("arp")]
    [InlineData("ARP")]
    public void CanHandle_ArpKey_ShouldBeTrue(string key)
        => new ArpDiscoveryProvider(new FakeSnmpClient(_ => SampleEntries()))
            .CanHandle(key).Should().BeTrue();

    [Theory]
    [InlineData("Icmp")]
    [InlineData("Lldp")]
    [InlineData("")]
    public void CanHandle_OtherKey_ShouldBeFalse(string key)
        => new ArpDiscoveryProvider(new FakeSnmpClient(_ => SampleEntries()))
            .CanHandle(key).Should().BeFalse();

    [Fact]
    public async Task DiscoverAsync_TwoEntries_ShouldReturnOneFindingEachWithTypes()
    {
        var client = new FakeSnmpClient(_ => SampleEntries());
        var provider = new ArpDiscoveryProvider(client);

        var findings = await provider.DiscoverAsync("192.0.2.1");

        findings.Should().HaveCount(2);
        findings.Should().OnlyContain(f => f.Kind == DiscoveryResultKind.ArpEntry);

        var first = findings.Single(f => f.Summary!.Contains("192.0.2.10"));
        first.Succeeded.Should().BeTrue();
        first.Summary.Should().Be("192.0.2.10 — aa:bb:cc:dd:ee:01");
        first.Attributes!["ip"].Should().Be("192.0.2.10");
        first.Attributes!["mac"].Should().Be("aa:bb:cc:dd:ee:01");
        first.Attributes!["local_if"].Should().Be("11");
        first.Attributes!["arp_type"].Should().Be("3");

        var second = findings.Single(f => f.Summary!.Contains("192.0.2.20"));
        second.Attributes!["arp_type"].Should().Be("4");

        client.Calls.Should().Be(1);
        client.LastOid.Should().Be(MediaTable);
    }

    [Fact]
    public async Task DiscoverAsync_EntryWithoutMac_ShouldSummarizeByIpOnly()
    {
        var provider = new ArpDiscoveryProvider(new FakeSnmpClient(_ =>
            [new SnmpVariable($"{MediaTable}.3.11.192.0.2.99", "192.0.2.99")]));

        var findings = await provider.DiscoverAsync("192.0.2.1");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Summary.Should().Be("192.0.2.99.");
        finding.Attributes!["ip"].Should().Be("192.0.2.99");
    }

    [Fact]
    public async Task DiscoverAsync_EmptyWalk_ShouldRegisterNoEntriesFinding()
    {
        var provider = new ArpDiscoveryProvider(new FakeSnmpClient(_ => []));

        var findings = await provider.DiscoverAsync("192.0.2.1");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Kind.Should().Be(DiscoveryResultKind.ArpEntry);
        finding.Succeeded.Should().BeFalse();
        finding.Summary.Should().Be("Nenhuma entrada ARP.");
        finding.Attributes!["arp_entries"].Should().Be("0");
    }

    [Fact]
    public async Task DiscoverAsync_ClientThrows_ShouldFailWithPtBrError()
    {
        var provider = new ArpDiscoveryProvider(new ThrowingSnmpClient());

        var findings = await provider.DiscoverAsync("192.0.2.1");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Succeeded.Should().BeFalse();
        finding.Error.Should().StartWith("Falha ao consultar ARP:");
        finding.Summary.Should().Contain("indisponível");
        finding.Attributes!["reachable"].Should().Be("false");
    }

    [Fact]
    public async Task DiscoverAsync_EmptyTarget_ShouldFailWithoutCallingClient()
    {
        var client = new FakeSnmpClient(_ => SampleEntries());
        var provider = new ArpDiscoveryProvider(client);

        var findings = await provider.DiscoverAsync("");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Succeeded.Should().BeFalse();
        finding.Error.Should().Be("Destino do ARP é obrigatório.");
        client.Calls.Should().Be(0);
    }
}

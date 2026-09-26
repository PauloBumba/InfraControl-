using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Discovery;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Discovery;

/// <summary>
/// F4-T009: provider MAC Tables de discovery — walk da dot1dTpFdbTable
/// (BRIDGE-MIB RFC 4188, 1.3.6.1.2.1.17.4.3) via SNMP. Decisões
/// aprovadas: filtro só status=3 (learned) e chave de provider "Mac".
/// Índice: MAC em 6 octetos. 1 achado Kind=MacTableEntry por entrada.
/// </summary>
public sealed class MacTableDiscoveryProviderTests
{
    private const string FdbTable = "1.3.6.1.2.1.17.4.3";

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

    // Índice: MAC em octetos (aa:bb:cc:dd:ee:01 = 170.187.204.221.238.1).
    private static IReadOnlyList<SnmpVariable> SampleEntries() =>
    [
        new($"{FdbTable}.1.170.187.204.221.238.1", "aa:bb:cc:dd:ee:01"),
        new($"{FdbTable}.2.170.187.204.221.238.1", "11"),
        new($"{FdbTable}.3.170.187.204.221.238.1", "3"),
        new($"{FdbTable}.1.170.187.204.221.238.2", "aa:bb:cc:dd:ee:02"),
        new($"{FdbTable}.2.170.187.204.221.238.2", "12"),
        new($"{FdbTable}.3.170.187.204.221.238.2", "3")
    ];

    [Fact]
    public void Name_ShouldBeMac()
        => new MacTableDiscoveryProvider(new FakeSnmpClient(_ => SampleEntries()))
            .Name.Should().Be("MAC");

    [Theory]
    [InlineData("Mac")]
    [InlineData("mac")]
    [InlineData("MAC")]
    public void CanHandle_MacKey_ShouldBeTrue(string key)
        => new MacTableDiscoveryProvider(new FakeSnmpClient(_ => SampleEntries()))
            .CanHandle(key).Should().BeTrue();

    [Theory]
    [InlineData("Arp")]
    [InlineData("MacTable")]
    [InlineData("")]
    public void CanHandle_OtherKey_ShouldBeFalse(string key)
        => new MacTableDiscoveryProvider(new FakeSnmpClient(_ => SampleEntries()))
            .CanHandle(key).Should().BeFalse();

    [Fact]
    public async Task DiscoverAsync_TwoLearnedEntries_ShouldReturnOneFindingEach()
    {
        var client = new FakeSnmpClient(_ => SampleEntries());
        var provider = new MacTableDiscoveryProvider(client);

        var findings = await provider.DiscoverAsync("192.0.2.1");

        findings.Should().HaveCount(2);
        findings.Should().OnlyContain(f => f.Kind == DiscoveryResultKind.MacTableEntry);

        var first = findings.Single(f => f.Summary!.Contains("ee:01"));
        first.Succeeded.Should().BeTrue();
        first.Summary.Should().Be("aa:bb:cc:dd:ee:01 — porta 11");
        first.Attributes!["mac"].Should().Be("aa:bb:cc:dd:ee:01");
        first.Attributes!["bridge_port"].Should().Be("11");
        first.Attributes!["fdb_status"].Should().Be("3");

        var second = findings.Single(f => f.Summary!.Contains("ee:02"));
        second.Attributes!["bridge_port"].Should().Be("12");

        client.Calls.Should().Be(1);
        client.LastOid.Should().Be(FdbTable);
    }

    [Fact]
    public async Task DiscoverAsync_NonLearnedStatuses_ShouldBeDiscarded()
    {
        var provider = new MacTableDiscoveryProvider(new FakeSnmpClient(_ =>
        [
            // self(4), invalid(2), other(1) — descartadas; só learned(3).
            new SnmpVariable($"{FdbTable}.1.170.187.204.221.238.3", "aa:bb:cc:dd:ee:03"),
            new SnmpVariable($"{FdbTable}.2.170.187.204.221.238.3", "13"),
            new SnmpVariable($"{FdbTable}.3.170.187.204.221.238.3", "4"),
            new SnmpVariable($"{FdbTable}.1.170.187.204.221.238.4", "aa:bb:cc:dd:ee:04"),
            new SnmpVariable($"{FdbTable}.3.170.187.204.221.238.4", "2"),
            new SnmpVariable($"{FdbTable}.1.170.187.204.221.238.5", "aa:bb:cc:dd:ee:05"),
            new SnmpVariable($"{FdbTable}.3.170.187.204.221.238.5", "1")
        ]));

        var findings = await provider.DiscoverAsync("192.0.2.1");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Succeeded.Should().BeFalse();
        finding.Summary.Should().Be("Nenhuma entrada MAC.");
        finding.Attributes!["mac_entries"].Should().Be("0");
    }

    [Fact]
    public async Task DiscoverAsync_EmptyWalk_ShouldRegisterNoEntriesFinding()
    {
        var provider = new MacTableDiscoveryProvider(new FakeSnmpClient(_ => []));

        var findings = await provider.DiscoverAsync("192.0.2.1");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Kind.Should().Be(DiscoveryResultKind.MacTableEntry);
        finding.Succeeded.Should().BeFalse();
        finding.Summary.Should().Be("Nenhuma entrada MAC.");
        finding.Attributes!["mac_entries"].Should().Be("0");
    }

    [Fact]
    public async Task DiscoverAsync_ClientThrows_ShouldFailWithPtBrError()
    {
        var provider = new MacTableDiscoveryProvider(new ThrowingSnmpClient());

        var findings = await provider.DiscoverAsync("192.0.2.1");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Succeeded.Should().BeFalse();
        finding.Error.Should().StartWith("Falha ao consultar MAC:");
        finding.Summary.Should().Contain("indisponível");
        finding.Attributes!["reachable"].Should().Be("false");
    }

    [Fact]
    public async Task DiscoverAsync_EmptyTarget_ShouldFailWithoutCallingClient()
    {
        var client = new FakeSnmpClient(_ => SampleEntries());
        var provider = new MacTableDiscoveryProvider(client);

        var findings = await provider.DiscoverAsync("");

        var finding = findings.Should().ContainSingle().Subject;
        finding.Succeeded.Should().BeFalse();
        finding.Error.Should().Be("Destino da MAC é obrigatório.");
        client.Calls.Should().Be(0);
    }
}

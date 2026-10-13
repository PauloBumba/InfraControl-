using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F29-3: SnmpInterfacesMonitoringProvider — agregado de saúde das
/// interfaces (ifTable status/erros + ifXTable octets HC correlacionados por
/// ifIndex), via seam ISnmpClient, sem rede real.
/// </summary>
public sealed class SnmpInterfacesProviderTests
{
    // ifTable: ifIndex 1 (up), 2 (down, 5/2 erros), 3 (up, 1/0 erros)
    private static IReadOnlyList<SnmpVariable> SampleIfTable() =>
    [
        new("1.3.6.1.2.1.2.2.1.8.1", "1"),
        new("1.3.6.1.2.1.2.2.1.14.1", "0"),
        new("1.3.6.1.2.1.2.2.1.20.1", "0"),
        new("1.3.6.1.2.1.2.2.1.8.2", "2"),
        new("1.3.6.1.2.1.2.2.1.14.2", "5"),
        new("1.3.6.1.2.1.2.2.1.20.2", "2"),
        new("1.3.6.1.2.1.2.2.1.8.3", "1"),
        new("1.3.6.1.2.1.2.2.1.14.3", "1"),
        new("1.3.6.1.2.1.2.2.1.20.3", "0")
    ];

    // ifXTable: ifIndex 1 (1000/2000), 2 (100/50), 3 (300/400)
    private static IReadOnlyList<SnmpVariable> SampleIfXTable() =>
    [
        new("1.3.6.1.2.1.31.1.1.1.6.1", "1000"),
        new("1.3.6.1.2.1.31.1.1.1.10.1", "2000"),
        new("1.3.6.1.2.1.31.1.1.1.6.2", "100"),
        new("1.3.6.1.2.1.31.1.1.1.10.2", "50"),
        new("1.3.6.1.2.1.31.1.1.1.6.3", "300"),
        new("1.3.6.1.2.1.31.1.1.1.10.3", "400")
    ];

    private sealed class FakeSnmpClient(
        Func<string, IReadOnlyList<SnmpVariable>> walkFactory) : ISnmpClient
    {
        public Task<IReadOnlyList<SnmpVariable>> WalkAsync(
            string target,
            string oid,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
            => Task.FromResult(walkFactory(oid));
    }

    private sealed class ThrowingOnFirstOidClient(string firstOid) : ISnmpClient
    {
        private int _calls;

        public Task<IReadOnlyList<SnmpVariable>> WalkAsync(
            string target,
            string oid,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            _calls++;
            if (_calls == 1 && oid == firstOid)
                throw new TimeoutException("SNMP timeout simulado.");
            return Task.FromResult(oid == SnmpInterfacesMonitoringProvider.IfTableOid
                ? SampleIfTable()
                : SampleIfXTable());
        }
    }

    [Fact]
    public void Provider_NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new SnmpInterfacesMonitoringProvider(
            new FakeSnmpClient(_ => []));

        provider.Name.Should().Be("SnmpInterfaces");
        provider.CanHandle("SnmpInterfaces").Should().BeTrue();
        provider.CanHandle("snmpinterfaces").Should().BeTrue();
        provider.CanHandle("SnmpSystem").Should().BeFalse();
    }

    [Fact]
    public async Task Success_ShouldAggregateStatusErrorsAndOctetsByIfIndex()
    {
        var client = new FakeSnmpClient(oid =>
            oid == SnmpInterfacesMonitoringProvider.IfTableOid
                ? SampleIfTable()
                : SampleIfXTable());
        var provider = new SnmpInterfacesMonitoringProvider(client);

        var result = await provider.CheckAsync("192.0.2.10");

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        var values = result.Values!;
        values["interfaceCount"].Should().Be("3");
        values["upCount"].Should().Be("2"); // ifIndex 1 e 3
        values["totalInErrors"].Should().Be("6"); // 0+5+1
        values["totalOutErrors"].Should().Be("2"); // 0+2+0
        values["totalInOctets"].Should().Be("1400"); // 1000+100+300
        values["totalOutOctets"].Should().Be("2450"); // 2000+50+400
    }

    [Fact]
    public async Task EmptyIfTable_ShouldFailHonest()
    {
        var client = new FakeSnmpClient(_ => []);
        var provider = new SnmpInterfacesMonitoringProvider(client);

        var result = await provider.CheckAsync("192.0.2.10");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("ifTable");
    }

    [Fact]
    public async Task IfXTableFailure_ShouldStillReportStatusAndErrorsWithZeroOctets()
    {
        // ifXTable lança → octets ficam 0, mas status/erros do ifTable seguem
        var client = new FakeSnmpClient(oid =>
            oid == SnmpInterfacesMonitoringProvider.IfTableOid
                ? SampleIfTable()
                : throw new TimeoutException("ifXTable indisponível."));
        var provider = new SnmpInterfacesMonitoringProvider(client);

        var result = await provider.CheckAsync("192.0.2.10");

        result.Succeeded.Should().BeTrue();
        result.Values!["interfaceCount"].Should().Be("3");
        result.Values!["upCount"].Should().Be("2");
        result.Values!["totalInErrors"].Should().Be("6");
        result.Values!["totalInOctets"].Should().Be("0");
        result.Values!["totalOutOctets"].Should().Be("0");
    }

    [Fact]
    public async Task IfTableFailure_ShouldFail()
    {
        var client = new ThrowingOnFirstOidClient(
            SnmpInterfacesMonitoringProvider.IfTableOid);
        var provider = new SnmpInterfacesMonitoringProvider(client);

        var result = await provider.CheckAsync("192.0.2.10");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("ifTable");
    }

    [Fact]
    public async Task EmptyTarget_ShouldFail()
    {
        var provider = new SnmpInterfacesMonitoringProvider(
            new FakeSnmpClient(_ => SampleIfTable()));

        var result = await provider.CheckAsync("  ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("obrigatório");
    }

    [Fact]
    public async Task InvalidCounterAndStatus_ShouldBeTreatedAsZeroNotInvented()
    {
        // valor não numérico → 0 (nunca inventa); interface ainda conta
        var client = new FakeSnmpClient(oid =>
            oid == SnmpInterfacesMonitoringProvider.IfTableOid
                ? new SnmpVariable[]
                {
                    new("1.3.6.1.2.1.2.2.1.8.1", "up"), // não numérico → 0 (não up)
                    new("1.3.6.1.2.1.2.2.1.14.1", "-5"), // negativo → 0
                    new("1.3.6.1.2.1.2.2.1.20.1", "abc") // inválido → 0
                }
                : new SnmpVariable[] { new("1.3.6.1.2.1.31.1.1.1.6.1", "777") });
        var provider = new SnmpInterfacesMonitoringProvider(client);

        var result = await provider.CheckAsync("192.0.2.10");

        result.Succeeded.Should().BeTrue();
        result.Values!["interfaceCount"].Should().Be("1");
        result.Values!["upCount"].Should().Be("0");
        result.Values!["totalInErrors"].Should().Be("0");
        result.Values!["totalOutErrors"].Should().Be("0");
    }
}

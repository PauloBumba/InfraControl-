using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T002c: SnmpPrinterCountersProvider — total de páginas via
/// prtMarkerLifeCount (Printer MIB RFC 3805, counter monotônico).
/// Informativo (sem alerta); volume mensal = diff entre checks (query
/// futura, histórico numérico fica no Zabbix — REGRA 8 ADR-028).
/// </summary>
public sealed class SnmpPrinterCountersProviderTests
{
    private const string LifeCountOid = "1.3.6.1.2.1.43.10.2.1.4";

    private static IReadOnlyList<SnmpVariable> SampleLifeCount(long total = 125_000, int index = 1)
        => [new($"{LifeCountOid}.{index}", total.ToString())];

    private sealed class FakeSnmpClient(
        Func<string, IReadOnlyList<SnmpVariable>> walkFactory) : ISnmpClient
    {
        public List<string> AskedOids { get; } = [];

        public Task<IReadOnlyList<SnmpVariable>> WalkAsync(
            string target,
            string oid,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            AskedOids.Add(oid);
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
    public void NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new SnmpPrinterCountersProvider(new FakeSnmpClient(_ => SampleLifeCount()));

        provider.Name.Should().Be("SnmpPrinterCounters");
        provider.CanHandle("SnmpPrinterCounters").Should().BeTrue();
        provider.CanHandle("snmpprintercounters").Should().BeTrue();
        provider.CanHandle("SnmpToner").Should().BeFalse();
    }

    [Fact]
    public async Task Success_ShouldReportTotalPages()
    {
        var provider = new SnmpPrinterCountersProvider(
            new FakeSnmpClient(_ => SampleLifeCount(total: 125_000)));

        var result = await provider.CheckAsync("192.0.2.80");

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Values["totalPages"].Should().Be("125000");
    }

    [Fact]
    public async Task MultipleMarkers_ShouldReportMaxAsTotal()
    {
        // Impressoras com mono+color reporters (index 1 e 2): o total
        // agregado é o maior counter (monotônico por device).
        var provider = new SnmpPrinterCountersProvider(new FakeSnmpClient(_ =>
        [
            new($"{LifeCountOid}.1", "100000"),
            new($"{LifeCountOid}.2", "25000")
        ]));

        var result = await provider.CheckAsync("192.0.2.80");

        result.Succeeded.Should().BeTrue();
        result.Values["totalPages"].Should().Be("100000");
    }

    [Fact]
    public async Task ZeroCounter_ShouldReportZero()
    {
        var provider = new SnmpPrinterCountersProvider(
            new FakeSnmpClient(_ => SampleLifeCount(total: 0)));

        var result = await provider.CheckAsync("192.0.2.80");

        result.Succeeded.Should().BeTrue();
        result.Values["totalPages"].Should().Be("0");
    }

    [Fact]
    public async Task HugeCounter_ShouldNotOverflow()
    {
        // Counter 64-bit real pode passar de int.MaxValue — guard em long.
        var provider = new SnmpPrinterCountersProvider(
            new FakeSnmpClient(_ => SampleLifeCount(total: 9_000_000_000_000)));

        var result = await provider.CheckAsync("192.0.2.80");

        result.Succeeded.Should().BeTrue();
        result.Values["totalPages"].Should().Be("9000000000000");
    }

    [Fact]
    public async Task NegativeOrGarbageValues_ShouldBeSkipped()
    {
        var provider = new SnmpPrinterCountersProvider(new FakeSnmpClient(_ =>
        [
            new($"{LifeCountOid}.1", "-5"),
            new($"{LifeCountOid}.2", "not-a-number")
        ]));

        var result = await provider.CheckAsync("192.0.2.80");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Nenhum prtMarkerLifeCount válido encontrado na impressora.");
    }

    [Fact]
    public async Task EmptyWalk_ShouldFailWithPtBrError()
    {
        var provider = new SnmpPrinterCountersProvider(new FakeSnmpClient(_ => []));

        var result = await provider.CheckAsync("192.0.2.80");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Nenhum prtMarkerLifeCount válido encontrado na impressora.");
    }

    [Fact]
    public async Task EmptyTarget_ShouldFailWithoutCallingSnmp()
    {
        var snmp = new FakeSnmpClient(_ => SampleLifeCount());
        var provider = new SnmpPrinterCountersProvider(snmp);

        var result = await provider.CheckAsync("  ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino do SNMP é obrigatório.");
        snmp.AskedOids.Should().BeEmpty();
    }

    [Fact]
    public async Task SnmpThrows_ShouldFailWithPtBrWrappedError()
    {
        var provider = new SnmpPrinterCountersProvider(new ThrowingSnmpClient());

        var result = await provider.CheckAsync("192.0.2.80");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Falha ao consultar SNMP:");
    }

    [Fact]
    public async Task ShouldWalkPrtMarkerLifeCount()
    {
        var snmp = new FakeSnmpClient(_ => SampleLifeCount());
        var provider = new SnmpPrinterCountersProvider(snmp);

        await provider.CheckAsync("192.0.2.80");

        snmp.AskedOids.Should().ContainSingle().Which.Should().Be(LifeCountOid,
            "a fonte é o prtMarkerLifeCount do Printer MIB (RFC 3805)");
    }
}

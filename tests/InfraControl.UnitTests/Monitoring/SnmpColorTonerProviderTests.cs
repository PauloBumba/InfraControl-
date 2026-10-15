using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T002a: SnmpColorTonerProvider (Printer MIB prtMarkerSuppliesTable,
/// toner C/M/Y). Walk na mesma tabela do SnmpToner monocromático, classifica
/// por descrição (cyan/magenta/yellow + pt-BR) e reporta nível por cor +
/// pior cor para o AlertEvaluator.
/// </summary>
public sealed class SnmpColorTonerProviderTests
{
    private const string SuppliesTableOid = "1.3.6.1.2.1.43.11.1.1";

    private static IReadOnlyList<SnmpVariable> SampleCMY(
        int cyan = 60, int magenta = 40, int yellow = 80) =>
    [
        new("1.3.6.1.2.1.43.11.1.1.6.1", "Black Toner"),
        new("1.3.6.1.2.1.43.11.1.1.8.1", "100"),
        new("1.3.6.1.2.1.43.11.1.1.9.1", "55"),
        new("1.3.6.1.2.1.43.11.1.1.6.2", "Cyan Toner Cartridge"),
        new("1.3.6.1.2.1.43.11.1.1.8.2", "100"),
        new("1.3.6.1.2.1.43.11.1.1.9.2", cyan.ToString()),
        new("1.3.6.1.2.1.43.11.1.1.6.3", "Magenta Toner Cartridge"),
        new("1.3.6.1.2.1.43.11.1.1.8.3", "100"),
        new("1.3.6.1.2.1.43.11.1.1.9.3", magenta.ToString()),
        new("1.3.6.1.2.1.43.11.1.1.6.4", "Yellow Toner Cartridge"),
        new("1.3.6.1.2.1.43.11.1.1.8.4", "100"),
        new("1.3.6.1.2.1.43.11.1.1.9.4", yellow.ToString())
    ];

    private sealed class FakeSnmpClient(Func<string, IReadOnlyList<SnmpVariable>> walkFactory) : ISnmpClient
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<SnmpVariable>> WalkAsync(
            string target,
            string oid,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            Calls++;
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
        var provider = new SnmpColorTonerProvider(new FakeSnmpClient(_ => SampleCMY()));

        provider.Name.Should().Be("SnmpColorToner");
        provider.CanHandle("SnmpColorToner").Should().BeTrue();
        provider.CanHandle("snmpcolortoner").Should().BeTrue();
        provider.CanHandle("SnmpToner").Should().BeFalse();
    }

    [Fact]
    public async Task Success_ShouldReportPerColorAndWorst()
    {
        var provider = new SnmpColorTonerProvider(
            new FakeSnmpClient(_ => SampleCMY(cyan: 60, magenta: 15, yellow: 80)));

        var result = await provider.CheckAsync("192.0.2.60");

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Values["cyanPercent"].Should().Be("60");
        result.Values["magentaPercent"].Should().Be("15");
        result.Values["yellowPercent"].Should().Be("80");
        result.Values["worstPercent"].Should().Be("15");
        result.Values["worstColor"].Should().Be("magenta");
    }

    [Fact]
    public async Task Percent_ShouldBeCalculatedFromLevelOverCapacity()
    {
        var provider = new SnmpColorTonerProvider(new FakeSnmpClient(_ =>
        [
            new("1.3.6.1.2.1.43.11.1.1.6.1", "Cyan Toner"),
            new("1.3.6.1.2.1.43.11.1.1.8.1", "200"),
            new("1.3.6.1.2.1.43.11.1.1.9.1", "30")
        ]));

        var result = await provider.CheckAsync("192.0.2.60");

        result.Succeeded.Should().BeTrue();
        result.Values["cyanPercent"].Should().Be("15");
        result.Values["worstPercent"].Should().Be("15");
        result.Values["worstColor"].Should().Be("cyan");
    }

    [Theory]
    [InlineData("Ciano Toner")]
    [InlineData("Cyan Ink")]
    [InlineData("Tinta Ciano")]
    public async Task MatchesPtBrAndEnglishCyanTerms_ShouldSucceed(string description)
    {
        var provider = new SnmpColorTonerProvider(new FakeSnmpClient(_ =>
        [
            new("1.3.6.1.2.1.43.11.1.1.6.1", description),
            new("1.3.6.1.2.1.43.11.1.1.8.1", "100"),
            new("1.3.6.1.2.1.43.11.1.1.9.1", "70")
        ]));

        var result = await provider.CheckAsync("192.0.2.60");

        result.Succeeded.Should().BeTrue();
        result.Values["cyanPercent"].Should().Be("70");
    }

    [Theory]
    [InlineData("Amarelo Toner")]
    [InlineData("Yellow Ink")]
    [InlineData("Tinta Amarela")]
    public async Task MatchesPtBrAndEnglishYellowTerms_ShouldSucceed(string description)
    {
        var provider = new SnmpColorTonerProvider(new FakeSnmpClient(_ =>
        [
            new("1.3.6.1.2.1.43.11.1.1.6.1", description),
            new("1.3.6.1.2.1.43.11.1.1.8.1", "100"),
            new("1.3.6.1.2.1.43.11.1.1.9.1", "90")
        ]));

        var result = await provider.CheckAsync("192.0.2.60");

        result.Succeeded.Should().BeTrue();
        result.Values["yellowPercent"].Should().Be("90");
    }

    [Fact]
    public async Task OnlyBlackToner_ShouldFailWithPtBrError()
    {
        var provider = new SnmpColorTonerProvider(new FakeSnmpClient(_ =>
        [
            new("1.3.6.1.2.1.43.11.1.1.6.1", "Black Toner Cartridge"),
            new("1.3.6.1.2.1.43.11.1.1.8.1", "100"),
            new("1.3.6.1.2.1.43.11.1.1.9.1", "90")
        ]));

        var result = await provider.CheckAsync("192.0.2.60");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Nenhum toner colorido encontrado na impressora.");
    }

    [Fact]
    public async Task SentinelLevels_ShouldBeSkipped()
    {
        var provider = new SnmpColorTonerProvider(new FakeSnmpClient(_ =>
        [
            new("1.3.6.1.2.1.43.11.1.1.6.1", "Cyan Toner"),
            new("1.3.6.1.2.1.43.11.1.1.8.1", "100"),
            new("1.3.6.1.2.1.43.11.1.1.9.1", "-1"),
            new("1.3.6.1.2.1.43.11.1.1.6.2", "Magenta Toner"),
            new("1.3.6.1.2.1.43.11.1.1.8.2", "100"),
            new("1.3.6.1.2.1.43.11.1.1.9.2", "50")
        ]));

        var result = await provider.CheckAsync("192.0.2.60");

        result.Succeeded.Should().BeTrue();
        result.Values.Should().NotContainKey("cyanPercent",
            "sentinela -1 (unknown) não vira nível");
        result.Values["magentaPercent"].Should().Be("50");
        result.Values["worstColor"].Should().Be("magenta");
    }

    [Fact]
    public async Task EmptyTarget_ShouldFailWithoutCallingSnmp()
    {
        var snmp = new FakeSnmpClient(_ => SampleCMY());
        var provider = new SnmpColorTonerProvider(snmp);

        var result = await provider.CheckAsync("  ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino do SNMP é obrigatório.");
        snmp.Calls.Should().Be(0);
    }

    [Fact]
    public async Task SnmpThrows_ShouldFailWithPtBrWrappedError()
    {
        var provider = new SnmpColorTonerProvider(new ThrowingSnmpClient());

        var result = await provider.CheckAsync("192.0.2.60");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Falha ao consultar SNMP:");
    }

    [Fact]
    public async Task ShouldWalkPrinterSuppliesTable()
    {
        string? askedOid = null;
        var provider = new SnmpColorTonerProvider(new FakeSnmpClient(oid =>
        {
            askedOid = oid;
            return SampleCMY();
        }));

        await provider.CheckAsync("192.0.2.60");

        askedOid.Should().Be(SuppliesTableOid,
            "a fonte é a prtMarkerSuppliesTable do Printer MIB (RFC 3805)");
    }
}

using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F5-T004: SnmpTonerProvider/SnmpDrumProvider (Printer MIB prtMarkerSuppliesTable)
/// com seam ISnmpClient — walk de amostra, sem rede real.
/// </summary>
public sealed class PrinterSuppliesProviderTests
{
    private const string SuppliesTableOid = "1.3.6.1.2.1.43.11.1.1";

    private static IReadOnlyList<SnmpVariable> SampleToner(int level = 45, int max = 100) =>
    [
        new("1.3.6.1.2.1.43.11.1.1.6.1", "Black Toner Cartridge"),
        new("1.3.6.1.2.1.43.11.1.1.8.1", max.ToString()),
        new("1.3.6.1.2.1.43.11.1.1.9.1", level.ToString())
    ];

    private sealed class FakeSnmpClient(Func<string, IReadOnlyList<SnmpVariable>> walkFactory) : ISnmpClient
    {
        public int Calls { get; private set; }

        public SnmpVersion? LastVersion { get; private set; }

        public Task<IReadOnlyList<SnmpVariable>> WalkAsync(
            string target,
            string oid,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(walkFactory(oid));
        }

        public Task<IReadOnlyList<SnmpVariable>> WalkAsync(
            string target,
            string oid,
            TimeSpan timeout,
            SnmpVersion version,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastVersion = version;
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
    public void TonerProvider_NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new SnmpTonerProvider(new FakeSnmpClient(_ => SampleToner()));

        provider.Name.Should().Be("SnmpToner");
        provider.CanHandle("SnmpToner").Should().BeTrue();
        provider.CanHandle("snmptoner").Should().BeTrue();
        provider.CanHandle("SnmpDrum").Should().BeFalse();
    }

    [Fact]
    public void DrumProvider_NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new SnmpDrumProvider(new FakeSnmpClient(_ => SampleToner()));

        provider.Name.Should().Be("SnmpDrum");
        provider.CanHandle("SnmpDrum").Should().BeTrue();
        provider.CanHandle("SnmpToner").Should().BeFalse();
    }

    [Fact]
    public async Task Toner_Success_ShouldReturnLevelPercentAndDescription()
    {
        var snmp = new FakeSnmpClient(_ => SampleToner(level: 45, max: 100));
        var provider = new SnmpTonerProvider(snmp);

        var result = await provider.CheckAsync("192.0.2.50");

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Values.Should().ContainKey("levelPercent").WhoseValue.Should().Be("45");
        result.Values.Should().ContainKey("description").WhoseValue.Should().Be("Black Toner Cartridge");
        snmp.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Toner_CalculatedFromLevelOverCapacity_ShouldReturnPercent()
    {
        var provider = new SnmpTonerProvider(
            new FakeSnmpClient(_ => SampleToner(level: 30, max: 200)));

        var result = await provider.CheckAsync("192.0.2.50");

        result.Succeeded.Should().BeTrue();
        result.Values["levelPercent"].Should().Be("15");
    }

    [Fact]
    public async Task Toner_MultipleCartridges_ShouldReturnWorstLevel()
    {
        var provider = new SnmpTonerProvider(new FakeSnmpClient(_ =>
        [
            new("1.3.6.1.2.1.43.11.1.1.6.1", "Black Toner"),
            new("1.3.6.1.2.1.43.11.1.1.8.1", "100"),
            new("1.3.6.1.2.1.43.11.1.1.9.1", "45"),
            new("1.3.6.1.2.1.43.11.1.1.6.2", "Toner Cartridge"),
            new("1.3.6.1.2.1.43.11.1.1.8.2", "100"),
            new("1.3.6.1.2.1.43.11.1.1.9.2", "20")
        ]));

        var result = await provider.CheckAsync("192.0.2.50");

        result.Succeeded.Should().BeTrue();
        result.Values["levelPercent"].Should().Be("20",
            "o pior carretel é o que importa para reposição");
    }

    [Fact]
    public async Task Toner_NoMatchingSupply_ShouldFailWithPtBrError()
    {
        var provider = new SnmpTonerProvider(new FakeSnmpClient(_ =>
        [
            new("1.3.6.1.2.1.43.11.1.1.6.1", "Staple Cartridge"),
            new("1.3.6.1.2.1.43.11.1.1.8.1", "100"),
            new("1.3.6.1.2.1.43.11.1.1.9.1", "90")
        ]));

        var result = await provider.CheckAsync("192.0.2.50");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Nenhum toner encontrado na impressora.");
    }

    [Fact]
    public async Task Drum_MatchingDescription_ShouldReturnLevelPercent()
    {
        var provider = new SnmpDrumProvider(new FakeSnmpClient(_ =>
        [
            new("1.3.6.1.2.1.43.11.1.1.6.1", "Drum Unit"),
            new("1.3.6.1.2.1.43.11.1.1.8.1", "100"),
            new("1.3.6.1.2.1.43.11.1.1.9.1", "72")
        ]));

        var result = await provider.CheckAsync("192.0.2.50");

        result.Succeeded.Should().BeTrue();
        result.Values["levelPercent"].Should().Be("72");
        result.Values["description"].Should().Be("Drum Unit");
    }

    [Fact]
    public async Task Drum_TonerIntegratedCartridge_ShouldFallbackToTonerLevel()
    {
        // Canon imageRUNNER T06: cilindro integrado ao toner — a tabela só
        // tem o cartucho de toner, então o nível do cilindro é o dele.
        var provider = new SnmpDrumProvider(new FakeSnmpClient(_ =>
        [
            new("1.3.6.1.2.1.43.11.1.1.6.1.1", "Canon Toner T06 Black"),
            new("1.3.6.1.2.1.43.11.1.1.8.1.1", "100"),
            new("1.3.6.1.2.1.43.11.1.1.9.1.1", "52")
        ]));

        var result = await provider.CheckAsync("192.0.2.50");

        result.Succeeded.Should().BeTrue();
        result.Values["levelPercent"].Should().Be("52");
    }

    [Fact]
    public async Task Drum_InkTankPrinter_ShouldNotFallback()
    {
        // Epson EcoTank: garrafa de tinta não é cartucho toner — não tem
        // cilindro, então o SnmpDrum continua honesto ("—" na tela).
        var provider = new SnmpDrumProvider(new FakeSnmpClient(_ =>
        [
            new("1.3.6.1.2.1.43.11.1.1.6.1.1", "Black Ink Bottle"),
            new("1.3.6.1.2.1.43.11.1.1.8.1.1", "100"),
            new("1.3.6.1.2.1.43.11.1.1.9.1.1", "27")
        ]));

        var result = await provider.CheckAsync("192.0.2.50");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Nenhum drum encontrado na impressora.");
    }

    [Fact]
    public async Task Drum_DrumEntryWithoutLevel_ShouldNotFallback()
    {
        // Existe entrada de drum mas sem nível válido (sentinela) — não é
        // all-in-one; falha honesta em vez de reportar toner como cilindro.
        var provider = new SnmpDrumProvider(new FakeSnmpClient(_ =>
        [
            new("1.3.6.1.2.1.43.11.1.1.6.1.1", "Drum Unit"),
            new("1.3.6.1.2.1.43.11.1.1.8.1.1", "100"),
            new("1.3.6.1.2.1.43.11.1.1.9.1.1", "-1"),
            new("1.3.6.1.2.1.43.11.1.1.6.1.2", "Black Toner"),
            new("1.3.6.1.2.1.43.11.1.1.8.1.2", "100"),
            new("1.3.6.1.2.1.43.11.1.1.9.1.2", "70")
        ]));

        var result = await provider.CheckAsync("192.0.2.50");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Nenhum drum encontrado na impressora.");
    }

    [Theory]
    [InlineData("Cilindro Imaging")]
    [InlineData("Imaging Unit")]
    [InlineData("DRUM")]
    public async Task Drum_MatchesPtBrAndEnglishTerms_ShouldSucceed(string description)
    {
        var provider = new SnmpDrumProvider(new FakeSnmpClient(_ =>
        [
            new("1.3.6.1.2.1.43.11.1.1.6.1", description),
            new("1.3.6.1.2.1.43.11.1.1.8.1", "100"),
            new("1.3.6.1.2.1.43.11.1.1.9.1", "60")
        ]));

        var result = await provider.CheckAsync("192.0.2.50");

        result.Succeeded.Should().BeTrue();
        result.Values["levelPercent"].Should().Be("60");
    }

    [Fact]
    public async Task Toner_EmptyTarget_ShouldFailWithoutCallingSnmp()
    {
        var snmp = new FakeSnmpClient(_ => SampleToner());
        var provider = new SnmpTonerProvider(snmp);

        var result = await provider.CheckAsync("  ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino do SNMP é obrigatório.");
        snmp.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Toner_SnmpThrows_ShouldFailWithPtBrWrappedError()
    {
        var provider = new SnmpTonerProvider(new ThrowingSnmpClient());

        var result = await provider.CheckAsync("192.0.2.50");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Falha ao consultar SNMP:");
    }

    [Fact]
    public async Task Toner_ShouldWalkPrinterSuppliesTable()
    {
        string? askedOid = null;
        var provider = new SnmpTonerProvider(new FakeSnmpClient(oid =>
        {
            askedOid = oid;
            return SampleToner();
        }));

        await provider.CheckAsync("192.0.2.50");

        askedOid.Should().Be(SuppliesTableOid,
            "a fonte é a prtMarkerSuppliesTable do Printer MIB (RFC 3805)");
    }

    [Fact]
    public async Task Toner_ColorCartridge_ShouldNotMatchBlackToner()
    {
        // Tons de cor são do SnmpColorToner — o SnmpToner não pode reportar
        // nível de magenta como "Toner preto".
        var provider = new SnmpTonerProvider(new FakeSnmpClient(_ =>
        [
            new("1.3.6.1.2.1.43.11.1.1.6.1.1", "Canon GPR-66 Magenta Toner"),
            new("1.3.6.1.2.1.43.11.1.1.8.1.1", "100"),
            new("1.3.6.1.2.1.43.11.1.1.9.1.1", "49")
        ]));

        var result = await provider.CheckAsync("192.0.2.50");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Nenhum toner encontrado na impressora.");
    }

    [Fact]
    public async Task Toner_CompositeInstance_ShouldParseLevel()
    {
        // OIDs reais das impressoras HSDS (Zabbix): instância composta
        // "1.1" — ex.: ...43.11.1.1.9.1.1 = level da instância 1.1.
        var provider = new SnmpTonerProvider(new FakeSnmpClient(_ =>
        [
            new("1.3.6.1.2.1.43.11.1.1.6.1.1", "Canon Toner T06 Black"),
            new("1.3.6.1.2.1.43.11.1.1.8.1.1", "100"),
            new("1.3.6.1.2.1.43.11.1.1.9.1.1", "52")
        ]));

        var result = await provider.CheckAsync("192.0.2.50");

        result.Succeeded.Should().BeTrue();
        result.Values["levelPercent"].Should().Be("52");
        result.Values["description"].Should().Be("Canon Toner T06 Black");
    }

    [Fact]
    public async Task Toner_ShouldWalkWithSnmpV1()
    {
        // Impressoras HSDS só respondem SNMPv1 (o Zabbix usa v1 nos hosts
        // prn-*/HSDS_*); v2c estourava timeout e zerava toner/drum.
        var snmp = new FakeSnmpClient(_ => SampleToner());
        var provider = new SnmpTonerProvider(snmp);

        await provider.CheckAsync("192.0.2.50");

        snmp.LastVersion.Should().Be(SnmpVersion.V1);
    }

    [Fact]
    public async Task Drum_ShouldWalkWithSnmpV1()
    {
        var snmp = new FakeSnmpClient(_ => SampleToner());
        var provider = new SnmpDrumProvider(snmp);

        await provider.CheckAsync("192.0.2.50");

        snmp.LastVersion.Should().Be(SnmpVersion.V1);
    }
}

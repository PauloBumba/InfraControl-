using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T004 S2 (docs/52 §S2): SnmpRadioProvider — fallback SNMP do AP pela
/// IEEE 802.11 MIB (ficha 45 §C/D/E): estações móveis, SSID desejado, canal,
/// potência de TX, RSSI e privacidade, por <c>&lt;coluna&gt;.&lt;ifIndex&gt;</c>.
///
/// REGRA da casa: só afirma o que o device respondeu — coluna sem resposta =
/// chave AUSENTE (nunca 0); walk vazio/lixo = falha honesta em pt-BR. Sem
/// vendor (MIB é universal), sem repositório — só a seam ISnmpClient, no
/// molde do SnmpInterfacesMonitoringProvider.
/// </summary>
public sealed class SnmpRadioProviderTests
{
    // 2 rádios: ifIndex 5 (2.4 GHz, completo) e ifIndex 6 (5 GHz, parcial)
    private static IReadOnlyList<SnmpVariable> SampleDot11Walk() =>
    [
        new("1.3.6.1.2.1.80.1.1.3.1.5", "4"),          // mobileStations
        new("1.3.6.1.2.1.80.1.1.3.2.5", "Corp"),        // desiredSSID
        new("1.3.6.1.2.1.80.1.1.3.4.5", "6"),           // currentChannel
        new("1.3.6.1.2.1.80.1.1.3.5.5", "3"),           // txPowerLevel
        new("1.3.6.1.2.1.80.1.1.3.6.5", "-61"),         // rssi
        new("1.3.6.1.2.1.80.1.1.3.7.5", "1"),           // privacyImplemented
        new("1.3.6.1.2.1.80.1.1.3.1.6", "9"),           // mobileStations
        new("1.3.6.1.2.1.80.1.1.3.2.6", "Visitantes"),  // desiredSSID
        new("1.3.6.1.2.1.80.1.1.3.4.6", "36")           // currentChannel
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

    private sealed class ThrowingClient : ISnmpClient
    {
        public Task<IReadOnlyList<SnmpVariable>> WalkAsync(
            string target,
            string oid,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
            => throw new TimeoutException("SNMP timeout simulado.");
    }

    [Fact]
    public void Provider_NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new SnmpRadioProvider(new FakeSnmpClient(_ => []));

        provider.Name.Should().Be("SnmpRadio");
        provider.CanHandle("SnmpRadio").Should().BeTrue();
        provider.CanHandle("snmpradio").Should().BeTrue();
        provider.CanHandle("SnmpHealth").Should().BeFalse();
    }

    [Fact]
    public async Task Success_ShouldReportPerRadioKeysAndRadioCount()
    {
        var provider = new SnmpRadioProvider(
            new FakeSnmpClient(_ => SampleDot11Walk()));

        var result = await provider.CheckAsync("192.0.2.30");

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        var values = result.Values!;
        values["radioCount"].Should().Be("2");
        // rádio ifIndex 5 — completo
        values["mobileStations.5"].Should().Be("4");
        values["ssid.5"].Should().Be("Corp");
        values["channel.5"].Should().Be("6");
        values["txPowerLevel.5"].Should().Be("3");
        values["rssi.5"].Should().Be("-61");
        values["privacyImplemented.5"].Should().Be("1");
        // rádio ifIndex 6 — só o que respondeu
        values["mobileStations.6"].Should().Be("9");
        values["ssid.6"].Should().Be("Visitantes");
        values["channel.6"].Should().Be("36");
        // coluna que o device NÃO respondeu para o ifIndex 6 = chave ausente
        values.ContainsKey("rssi.6").Should().BeFalse(
            "métrica ausente é chave ausente, nunca 0 inventado");
        values.ContainsKey("txPowerLevel.6").Should().BeFalse();
    }

    [Fact]
    public async Task Success_ShouldIgnoreUnknownColumnsBlankValuesAndForeignOids()
    {
        // coluna desconhecida (.99), ifIndex não numérico, sufixo extra,
        // OID fora da raiz dot11 e valor em branco — tudo fora do resultado
        var walk = new SnmpVariable[]
        {
            new("1.3.6.1.2.1.80.1.1.3.4.5", "6"),
            new("1.3.6.1.2.1.80.1.1.3.99.5", "42"),
            new("1.3.6.1.2.1.80.1.1.3.6.abc", "-70"),
            new("1.3.6.1.2.1.80.1.1.3.5.5.9", "3"),
            new("1.3.6.1.2.1.2.2.1.8.5", "1"),
            new("1.3.6.1.2.1.80.1.1.3.7.5", "   ")
        };
        var provider = new SnmpRadioProvider(new FakeSnmpClient(_ => walk));

        var result = await provider.CheckAsync("192.0.2.30");

        result.Succeeded.Should().BeTrue();
        var values = result.Values!;
        values.Should().Equal(
            new KeyValuePair<string, string>("radioCount", "1"),
            new KeyValuePair<string, string>("channel.5", "6"));
    }

    [Fact]
    public async Task EmptyWalk_ShouldFailHonestPtBr()
    {
        var provider = new SnmpRadioProvider(new FakeSnmpClient(_ => []));

        var result = await provider.CheckAsync("192.0.2.30");

        result.Succeeded.Should().BeFalse();
        result.Values.Should().BeNull();
        result.Error.Should().Contain("dot11");
    }

    [Fact]
    public async Task OnlyJunk_ShouldFailHonestPtBr()
    {
        var walk = new SnmpVariable[]
        {
            new("1.3.6.1.2.1.80.1.1.3.99.5", "42"),
            new("1.3.6.1.2.1.80.1.1.3.6.abc", "-70")
        };
        var provider = new SnmpRadioProvider(new FakeSnmpClient(_ => walk));

        var result = await provider.CheckAsync("192.0.2.30");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("dot11");
    }

    [Fact]
    public async Task SnmpThrow_ShouldFailWithPtBrMessage()
    {
        var provider = new SnmpRadioProvider(new ThrowingClient());

        var result = await provider.CheckAsync("192.0.2.30");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("802.11");
        result.Error.Should().Contain("SNMP timeout simulado.");
    }

    [Fact]
    public async Task EmptyTarget_ShouldFail()
    {
        var provider = new SnmpRadioProvider(
            new FakeSnmpClient(_ => SampleDot11Walk()));

        var result = await provider.CheckAsync("  ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("obrigatório");
    }
}

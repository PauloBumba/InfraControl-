using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// F30-T001 S4a (docs/42 §B L136-147): Velocidade/Duplex passam a ser
/// persistidos em <c>SwitchPort</c> como cadastro semi-estático.
/// REGRAS travadas aqui:
/// 1. cadastro manual (<c>Update</c>) NÃO mexe nisso — só <c>Observe</c>
///    (controller SNMP/UniFi) grava, conforme SwitchPort.cs L11-12;
/// 2. ausência de reporte NÃO apaga o último valor conhecido (velocidade é
///    semi-estática: porta caiu ≠ perdeu o enlace de 1G);
/// 3. taxa fora da enumeração da ficha ⇒ <c>null</c> (2.5G/5G multi-gig não
///    constam em docs/42 L144 — nada inventado, pendência na §7);
/// 4. PoE continua FORA (ADR-028 §10) — §7 item 8 do docs/51.
/// </summary>
public sealed class SwitchPortSpeedDuplexTests
{
    private static readonly Guid SomeSwitchId = Guid.NewGuid();

    // ---------- mapeamento: token observado → enum ----------

    [Theory]
    [InlineData("10M", SwitchPortSpeed.Speed10M)]
    [InlineData("100M", SwitchPortSpeed.Speed100M)]
    [InlineData("1G", SwitchPortSpeed.Speed1G)]
    [InlineData("1000M", SwitchPortSpeed.Speed1G)]
    [InlineData("1000", SwitchPortSpeed.Speed1G)] // UniFi: "speed":"1000" (Mbps puro)
    [InlineData("10G", SwitchPortSpeed.Speed10G)]
    [InlineData("10000M", SwitchPortSpeed.Speed10G)] // ReadSpeed normaliza int → "{n}M"
    [InlineData("40G", SwitchPortSpeed.Speed40G)]
    [InlineData("40000M", SwitchPortSpeed.Speed40G)]
    [InlineData("100G", SwitchPortSpeed.Speed100G)]
    [InlineData("100000M", SwitchPortSpeed.Speed100G)]
    [InlineData("1g", SwitchPortSpeed.Speed1G)] // case-insensitive
    [InlineData("  1G  ", SwitchPortSpeed.Speed1G)] // trim
    [InlineData("auto", SwitchPortSpeed.Auto)]
    public void Parse_WithAKnownRate_ShouldMapToTheFichaEnum(
        string observed, SwitchPortSpeed expected)
        => SwitchPortSpeedParser.Parse(observed).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")] // porta down: controller reporta 0
    [InlineData("2500M")] // multi-gig fora da enum da ficha (§7 pendência)
    [InlineData("5000M")]
    [InlineData("2.5G")]
    [InlineData("banana")]
    public void Parse_WithoutAFichaRate_ShouldReturnNull_NeverAFakeValue(
        string? observed)
        => SwitchPortSpeedParser.Parse(observed)
            .Should().BeNull("não existe enum para essa taxa — null, nunca 0 nem Auto");

    [Theory]
    [InlineData(true, SwitchPortDuplex.Full)]
    [InlineData(false, SwitchPortDuplex.Half)]
    public void Parse_Duplex_ShouldMapTheBoolean(bool fullDuplex, SwitchPortDuplex expected)
        => SwitchPortDuplexParser.Parse(fullDuplex).Should().Be(expected);

    [Fact]
    public void Parse_Duplex_WhenControllerOmittedTheField_ShouldReturnNull_NotHalf()
        => SwitchPortDuplexParser.Parse(null)
            .Should().BeNull("propriedade ausente não é meio-duplex — hoje isso era inventado");

    // ---------- entidade ----------

    [Fact]
    public void Create_ShouldLeaveSpeedAndDuplexNull()
    {
        var port = SwitchPort.Create(SomeSwitchId, "Gi1/0/1");

        port.Speed.Should().BeNull("nada foi observado ainda");
        port.Duplex.Should().BeNull();
    }

    [Fact]
    public void Observe_ShouldRecordLinkSpeedAndDuplex()
    {
        var port = SwitchPort.Create(SomeSwitchId, "Gi1/0/1");

        var changed = port.Observe(
            linkUp: true, speed: SwitchPortSpeed.Speed1G, duplex: SwitchPortDuplex.Full);

        changed.Should().BeTrue();
        port.LinkUp.Should().BeTrue();
        port.Speed.Should().Be(SwitchPortSpeed.Speed1G);
        port.Duplex.Should().Be(SwitchPortDuplex.Full);
    }

    [Fact]
    public void Observe_WithoutSpeedReport_ShouldKeepTheLastKnownOne()
    {
        var port = SwitchPort.Create(SomeSwitchId, "Gi1/0/1");
        port.Observe(true, SwitchPortSpeed.Speed1G, SwitchPortDuplex.Full);

        var changed = port.Observe(
            linkUp: false, speed: null, duplex: null);

        changed.Should().BeTrue("o link mudou de verdade");
        port.LinkUp.Should().BeFalse();
        port.Speed.Should().Be(
            SwitchPortSpeed.Speed1G, "porta sem resposta não apaga a velocidade conhecida");
        port.Duplex.Should().Be(SwitchPortDuplex.Full);
    }

    [Fact]
    public void Observe_WithNothingNew_ShouldReportNoChangeAndRaiseNoEvent()
    {
        var port = SwitchPort.Create(SomeSwitchId, "Gi1/0/1");
        port.Observe(true, SwitchPortSpeed.Speed1G, SwitchPortDuplex.Full);
        port.ClearDomainEvents();

        var changed = port.Observe(
            true, SwitchPortSpeed.Speed1G, SwitchPortDuplex.Full);

        changed.Should().BeFalse("sync idempotente: nada mudou");
        port.DomainEvents.Should().BeEmpty("sem evento = sem contagem de update no sync");
    }

    [Fact]
    public void Observe_WhenSpeedChanges_ShouldReportChange()
    {
        var port = SwitchPort.Create(SomeSwitchId, "Gi1/0/1");
        port.Observe(true, SwitchPortSpeed.Speed1G, null);

        var changed = port.Observe(true, SwitchPortSpeed.Speed100M, null);

        changed.Should().BeTrue();
        port.Speed.Should().Be(SwitchPortSpeed.Speed100M);
    }

    [Fact]
    public void Update_FromManualCadastro_ShouldNotTouchSpeedOrDuplex()
    {
        var port = SwitchPort.Create(SomeSwitchId, "Gi1/0/1");
        port.Observe(true, SwitchPortSpeed.Speed1G, SwitchPortDuplex.Full);

        port.Update("Gi1/0/1", "Uplink p/ core", linkUp: false);

        port.Speed.Should().Be(
            SwitchPortSpeed.Speed1G,
            "velocidade vem do controller, não do cadastro manual (SwitchPort.cs L11-12)");
        port.Duplex.Should().Be(SwitchPortDuplex.Full);
        port.LinkUp.Should().BeFalse();
    }
}

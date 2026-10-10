using FluentAssertions;
using InfraControl.Infrastructure.NetworkServices;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// F18-T005 (ADR-042 §3): driver Cisco — `show access-lists` parseado
/// com cabeçalhos Standard/Extended/IP access list (IOS-XE), protocolo
/// extraído só de palavra conhecida, expressão CRU preservada e linhas
/// que não são regra/cabeçalho ignoradas.
/// </summary>
public sealed class CiscoFirewallVendorReaderTests
{
    private readonly CiscoFirewallVendorReader _reader = new();

    [Fact]
    public void VendorAndCommand_ShouldBeCiscoShowAccessLists()
    {
        _reader.Vendor.Should().Be("cisco");
        _reader.Command.Should().Be("show access-lists");
    }

    [Fact]
    public void Parse_ShouldReadHeadersRulesAndKeepRawExpression()
    {
        var rules = _reader.Parse("""
            Standard IP access list 10
                10 permit 192.168.0.0, wildcard bits 255.255.0.0
                20 deny   any
            Extended IP access list OUTSIDE-IN
                10 permit tcp host 10.0.0.5 any eq 443 (match count 12)
                20 deny ip any any
            IP access list BLOCK-VPN
                5 permit udp any any eq 500
            !
            5 access list entries
            """);

        rules.Should().HaveCount(5);

        rules[0].Acl.Should().Be("10");
        rules[0].Scope.Should().Be("Standard");
        rules[0].Sequence.Should().Be(10);
        rules[0].Action.Should().Be("permit");
        rules[0].Protocol.Should().BeEmpty("ACL padrão não tem palavra de protocolo");
        rules[0].Expression.Should().Be("192.168.0.0, wildcard bits 255.255.0.0");
        rules[1].Action.Should().Be("deny");
        rules[1].Expression.Should().Be("any");

        rules[2].Acl.Should().Be("OUTSIDE-IN");
        rules[2].Scope.Should().Be("Extended");
        rules[2].Protocol.Should().Be("tcp");
        rules[2].Expression.Should().Be(
            "host 10.0.0.5 any eq 443 (match count 12)",
            "a expressão fica crua — sem semântica inventada");
        rules[3].Protocol.Should().Be("ip");
        rules[3].Expression.Should().Be("any any");

        rules[4].Acl.Should().Be("BLOCK-VPN");
        rules[4].Scope.Should().BeEmpty("'IP access list X' do IOS-XE não traz o tipo");
        rules[4].Sequence.Should().Be(5);
        rules[4].Protocol.Should().Be("udp");
    }

    [Fact]
    public void Parse_WhenLineIsNotRuleOrHeader_ShouldIgnoreIt()
    {
        var rules = _reader.Parse("""
            30 deny ip any any
            5 access list entries
            """);

        rules.Should().BeEmpty("linha fora de um ACL conhecido não vira regra inventada");
    }

    [Fact]
    public void Parse_WhenRuleAppearsBeforeHeader_ShouldIgnoreIt()
    {
        var rules = _reader.Parse("10 permit tcp any any");

        rules.Should().BeEmpty("regra órfã sem cabeçalho de ACL é ignorada");
    }
}

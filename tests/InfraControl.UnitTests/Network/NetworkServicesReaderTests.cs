using FluentAssertions;
using InfraControl.Application.Network.Services;
using InfraControl.Infrastructure.Monitoring;
using InfraControl.Infrastructure.NetworkServices;
using InfraControl.Infrastructure.NetworkServices.Ports;
using Microsoft.Extensions.Options;
using Moq;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// F18-T005 (ADR-042): leitura dos serviços de rede — parse SNMP de
/// leases (ipNetToMedia/ipNetToPhysical) e rotas (ipRouteTable com
/// rótulos RFC1213), MAC normalizado, semântica de "não configurado",
/// vendor de firewall desconhecido e quebra honesta quando o SNMP falha.
/// </summary>
public sealed class NetworkServicesReaderTests
{
    [Fact]
    public async Task GetDhcpLeasesAsync_ShouldParseArpTableAndNormalizeMac()
    {
        var snmp = new Mock<ISnmpClient>();
        snmp.Setup(s => s.WalkAsync(
                It.IsAny<string>(), NetworkServicesReader.ArpTableOid,
                It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                // ipNetToMediaTable: .1 ifIndex · .2 physAddress · .3 netAddress
                new SnmpVariable("1.3.6.1.2.1.4.22.1.1.1.192.168.1.10", "1"),
                new SnmpVariable("1.3.6.1.2.1.4.22.1.2.1.192.168.1.10", "001122334455"),
                new SnmpVariable("1.3.6.1.2.1.4.22.1.3.1.192.168.1.10", "192.168.1.10"),
                // MAC com separadores também normaliza
                new SnmpVariable("1.3.6.1.2.1.4.22.1.1.2.192.168.1.20", "2"),
                new SnmpVariable("1.3.6.1.2.1.4.22.1.2.2.192.168.1.20", "AA-BB-CC-DD-EE-FF"),
                new SnmpVariable("1.3.6.1.2.1.4.22.1.3.2.192.168.1.20", "192.168.1.20"),
                // entrada sem netAddress (.3) não identifica lease — descartada
                new SnmpVariable("1.3.6.1.2.1.4.22.1.2.3.10.0.0.99", "112233445566"),
            });

        var reader = Reader(snmp.Object);

        var leases = await reader.GetDhcpLeasesAsync();

        leases.Should().HaveCount(2, "entrada sem IP é descartada");
        leases[0].Ip.Should().Be("192.168.1.10");
        leases[0].Mac.Should().Be("00:11:22:33:44:55", "hex de 12 dígitos vira aa:bb:…");
        leases[0].IfIndex.Should().Be(1);
        leases[1].Mac.Should().Be("aa:bb:cc:dd:ee:ff", "separadores removidos e caixa baixa");
        leases[1].IfIndex.Should().Be(2);
    }

    [Fact]
    public async Task GetRoutesAsync_ShouldParseIpRouteTableWithRfc1213Labels()
    {
        var snmp = new Mock<ISnmpClient>();
        snmp.Setup(s => s.WalkAsync(
                It.IsAny<string>(), NetworkServicesReader.RouteTableOid,
                It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                // rota default: type=3 indirect, proto=12 ospf
                new SnmpVariable("1.3.6.1.2.1.4.21.1.1.0.0.0.0", "0.0.0.0"),
                new SnmpVariable("1.3.6.1.2.1.4.21.1.2.0.0.0.0", "2"),
                new SnmpVariable("1.3.6.1.2.1.4.21.1.3.0.0.0.0", "192.168.1.1"),
                new SnmpVariable("1.3.6.1.2.1.4.21.1.4.0.0.0.0", "10"),
                new SnmpVariable("1.3.6.1.2.1.4.21.1.10.0.0.0.0", "3"),
                new SnmpVariable("1.3.6.1.2.1.4.21.1.11.0.0.0.0", "12"),
                new SnmpVariable("1.3.6.1.2.1.4.21.1.13.0.0.0.0", "0.0.0.0"),
                // rede local: type=2 direct, proto=2 local
                new SnmpVariable("1.3.6.1.2.1.4.21.1.1.10.0.0.0", "10.0.0.0"),
                new SnmpVariable("1.3.6.1.2.1.4.21.1.2.10.0.0.0", "1"),
                new SnmpVariable("1.3.6.1.2.1.4.21.1.10.10.0.0.0", "2"),
                new SnmpVariable("1.3.6.1.2.1.4.21.1.11.10.0.0.0", "2"),
                new SnmpVariable("1.3.6.1.2.1.4.21.1.13.10.0.0.0", "255.0.0.0"),
                // proto fora do mapa = cru, sem rótulo inventado
                new SnmpVariable("1.3.6.1.2.1.4.21.1.1.172.16.0.0", "172.16.0.0"),
                new SnmpVariable("1.3.6.1.2.1.4.21.1.10.172.16.0.0", "1"),
                new SnmpVariable("1.3.6.1.2.1.4.21.1.11.172.16.0.0", "99"),
            });

        var reader = Reader(snmp.Object);

        var routes = await reader.GetRoutesAsync();

        routes.Select(route => route.Destination).Should().Equal(
            new[] { "0.0.0.0", "10.0.0.0", "172.16.0.0" },
            "ordenado por destino");

        var @default = routes[0];
        @default.Mask.Should().Be("0.0.0.0");
        @default.NextHop.Should().Be("192.168.1.1");
        @default.IfIndex.Should().Be(2);
        @default.Metric.Should().Be(10);
        @default.Type.Should().Be("indirect", "ipRouteType 3 (RFC1213)");
        @default.Protocol.Should().Be("ospf", "ipRouteProto 12 (RFC1213)");

        routes[1].Type.Should().Be("direct");
        routes[1].Protocol.Should().Be("local");
        routes[1].Mask.Should().Be("255.0.0.0");

        routes[2].Protocol.Should().Be("99", "número fora do mapa fica cru");
        routes[2].Type.Should().Be("other");
        routes[2].Mask.Should().BeEmpty("coluna ausente = vazio, nunca suposto");
    }

    [Fact]
    public async Task GetDhcpLeasesAsync_WhenNotConfigured_ShouldThrowPtBr()
    {
        var snmp = new Mock<ISnmpClient>();
        var reader = Reader(snmp.Object, target: "");

        var act = () => reader.GetDhcpLeasesAsync();

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("NetworkServices:Target");
        snmp.Verify(s => s.WalkAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetDhcpLeasesAsync_WhenSnmpFails_ShouldWrapWithPtBrReason()
    {
        var snmp = new Mock<ISnmpClient>();
        snmp.Setup(s => s.WalkAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("community recusada"));
        var reader = Reader(snmp.Object);

        var act = () => reader.GetDhcpLeasesAsync();

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should()
            .Contain("via SNMP", "quebra vira indisponibilidade honesta (503), nunca 500");
        exception.Which.Message.Should().Contain("192.168.1.1");
    }

    [Fact]
    public async Task GetFirewallRulesAsync_ShouldRunVendorCommandAndParse()
    {
        var ssh = new FakeRunner("""
            Extended IP access list OUTSIDE-IN
                10 permit tcp host 10.0.0.5 any eq 443
            """);
        var reader = Reader(Mock.Of<ISnmpClient>(), ssh: ssh);

        var rules = await reader.GetFirewallRulesAsync();

        ssh.LastCommand.Should().Be("show access-lists", "driver Cisco define o comando");
        rules.Should().ContainSingle();
        rules[0].Acl.Should().Be("OUTSIDE-IN");
        rules[0].Protocol.Should().Be("tcp");
    }

    [Fact]
    public async Task GetFirewallRulesAsync_WhenVendorUnknown_ShouldListSupported()
    {
        var ssh = new FakeRunner(string.Empty);
        var reader = Reader(
            Mock.Of<ISnmpClient>(), ssh: ssh, vendor: "juniper");

        var act = () => reader.GetFirewallRulesAsync();

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("juniper");
        exception.Which.Message.Should().Contain("cisco", "o erro lista os drivers suportados");
        ssh.LastCommand.Should().BeNull("sem driver não se abre sessão SSH");
    }

    private static NetworkServicesReader Reader(
        ISnmpClient snmp, ISshCommandRunner? ssh = null,
        string vendor = "cisco", string target = "192.168.1.1")
        => new(
            snmp,
            ssh ?? Mock.Of<ISshCommandRunner>(),
            new FirewallVendorReaderSelector([new CiscoFirewallVendorReader()]),
            Options.Create(new NetworkServicesOptions
            {
                Target = target,
                Vendor = vendor
            }));

    private sealed class FakeRunner(string output) : ISshCommandRunner
    {
        public string? LastCommand { get; private set; }

        public Task<string> ExecuteAsync(
            string command, CancellationToken cancellationToken = default)
        {
            LastCommand = command;
            return Task.FromResult(output);
        }
    }
}

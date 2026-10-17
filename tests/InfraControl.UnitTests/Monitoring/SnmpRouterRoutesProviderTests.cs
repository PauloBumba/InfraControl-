using System.Text.Json;
using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T007a: SnmpRouterRoutesProvider — rotas IPv4 via ipCidrRouteTable
/// (IP-FORWARD-MIB RFC 2096). O INDEX é composto (dest+mask+tos+nextHop);
/// classifica proto local/netmgmt = estática, ospf/bgp/igrp/eigrp =
/// dinâmica; rota padrão (0.0.0.0) expõe defaultGateway. Informativo —
/// sem regra no evaluator.
/// </summary>
public sealed class SnmpRouterRoutesProviderTests
{
    private const string RouteTableOid = "1.3.6.1.2.1.4.24.4.1";

    private const string ColDest = "1";
    private const string ColNextHop = "2";
    private const string ColType = "4";
    private const string ColProto = "5";

    // Instância composta padrão (dest 192.168.1.0 / mask 255.255.255.0 /
    // tos 0 / nextHop 10.0.0.1) — 13 sub-ids.
    private const string InstanceStub =
        "192.168.1.0.255.255.255.0.0.10.0.0.1";

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

    private static IEnumerable<SnmpVariable> Row(
        string instance, string dest, string nextHop, string type, string proto)
    {
        yield return new($"{RouteTableOid}.{ColDest}.{instance}", dest);
        yield return new($"{RouteTableOid}.{ColNextHop}.{instance}", nextHop);
        yield return new($"{RouteTableOid}.{ColType}.{instance}", type);
        yield return new($"{RouteTableOid}.{ColProto}.{instance}", proto);
    }

    private static IReadOnlyList<SnmpVariable> Table(params IEnumerable<SnmpVariable>[] rows)
        => [.. rows.SelectMany(row => row)];

    [Fact]
    public void NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new SnmpRouterRoutesProvider(new FakeSnmpClient(_ =>
            Table(Row(InstanceStub, "192.168.1.0", "10.0.0.1", "4", "2"))));

        provider.Name.Should().Be("SnmpRouterRoutes");
        provider.CanHandle("SnmpRouterRoutes").Should().BeTrue();
        provider.CanHandle("snmprouterroutes").Should().BeTrue();
        provider.CanHandle("SnmpRouterSessions").Should().BeFalse();
    }

    [Fact]
    public async Task Success_ShouldReportRoutesAndDefaultGateway()
    {
        // Rota padrão (dest 0.0.0.0) + rota local estática.
        var defaultInstance = "0.0.0.0.0.0.0.0.0.10.0.0.254";
        var localInstance = "10.10.0.0.255.255.0.0.0.10.0.0.1";
        var provider = new SnmpRouterRoutesProvider(new FakeSnmpClient(_ => Table(
            Row(defaultInstance, "0.0.0.0", "10.0.0.254", "4", "2"),
            Row(localInstance, "10.10.0.0", "10.0.0.1", "3", "2"))));

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeTrue();
        result.Values!["routeCount"].Should().Be("2");
        result.Values["defaultGateway"].Should().Be("10.0.0.254");
        result.Values["staticCount"].Should().Be("2");
        result.Values["dynamicCount"].Should().Be("0");

        var routes = JsonSerializer.Deserialize<List<JsonElement>>(
            result.Values["routes"]!);
        routes!.Should().HaveCount(2);
        routes[0].GetProperty("dest").GetString().Should().Be("0.0.0.0");
        routes[0].GetProperty("proto").GetString().Should().Be("local");
    }

    [Fact]
    public async Task Success_ShouldClassifyDynamicProtocols()
    {
        // OSPF (14) + BGP (15) = dinâmicas; staticCount deve ser 0.
        var ospfInstance = "172.16.0.0.255.255.0.0.0.10.1.0.1";
        var bgpInstance = "198.51.100.0.255.255.255.0.0.10.2.0.1";
        var provider = new SnmpRouterRoutesProvider(new FakeSnmpClient(_ => Table(
            Row(ospfInstance, "172.16.0.0", "10.1.0.1", "4", "14"),
            Row(bgpInstance, "198.51.100.0", "10.2.0.1", "4", "15"))));

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeTrue();
        result.Values!["staticCount"].Should().Be("0");
        result.Values["dynamicCount"].Should().Be("2");

        var routes = JsonSerializer.Deserialize<List<JsonElement>>(
            result.Values["routes"]!);
        routes![0].GetProperty("proto").GetString().Should().Be("ospf");
        routes[1].GetProperty("proto").GetString().Should().Be("bgp");
    }

    [Fact]
    public async Task Success_ShouldExcludeInvalidRoutes()
    {
        // type=2 (invalid) é descartada; só a válida (type=4) conta.
        var invalidInstance = "203.0.113.0.255.255.255.0.0.10.3.0.99";
        var validInstance = "10.0.0.0.255.0.0.0.0.10.0.0.1";
        var provider = new SnmpRouterRoutesProvider(new FakeSnmpClient(_ => Table(
            Row(invalidInstance, "203.0.113.0", "10.3.0.99", "2", "1"),
            Row(validInstance, "10.0.0.0", "10.0.0.1", "4", "3"))));

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeTrue();
        result.Values!["routeCount"].Should().Be("1");
        result.Values["staticCount"].Should().Be("1");
    }

    [Fact]
    public async Task EmptyTable_ShouldFailWithPtBrMessage()
    {
        var provider = new SnmpRouterRoutesProvider(new FakeSnmpClient(_ =>
            Array.Empty<SnmpVariable>()));

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(
            "Nenhuma rota IPv4 válida encontrada no router/firewall.");
    }

    [Fact]
    public async Task EmptyTarget_ShouldFailWithPtBrMessage()
    {
        var provider = new SnmpRouterRoutesProvider(new FakeSnmpClient(_ =>
            Array.Empty<SnmpVariable>()));

        var result = await provider.CheckAsync("  ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino do SNMP é obrigatório.");
    }

    [Fact]
    public async Task SnmpThrow_ShouldFailWithPtBrMessage()
    {
        var provider = new SnmpRouterRoutesProvider(new ThrowingSnmpClient());

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Falha ao consultar SNMP: ");
    }

    [Fact]
    public async Task Walk_ShouldAskRouteTableOid()
    {
        var client = new FakeSnmpClient(_ =>
            Table(Row(InstanceStub, "192.168.1.0", "10.0.0.1", "4", "2")));
        var provider = new SnmpRouterRoutesProvider(client);

        await provider.CheckAsync("192.0.2.1");

        client.AskedOids.Should().ContainSingle()
            .Which.Should().Be(RouteTableOid);
    }

    [Fact]
    public async Task Success_ShouldCapRoutesJsonAt20()
    {
        // 25 rotas — a lista JSON deve top 20, routeCount = 25.
        var rows = new List<IEnumerable<SnmpVariable>>();
        for (var i = 0; i < 25; i++)
        {
            var instance = $"10.{i}.0.0.255.255.0.0.0.10.0.0.1";
            rows.Add(Row(instance, $"10.{i}.0.0", "10.0.0.1", "4", "2"));
        }

        var provider = new SnmpRouterRoutesProvider(new FakeSnmpClient(_ =>
            Table([.. rows])));

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeTrue();
        result.Values!["routeCount"].Should().Be("25");

        var routes = JsonSerializer.Deserialize<List<JsonElement>>(
            result.Values["routes"]!);
        routes!.Should().HaveCount(20);
    }
}

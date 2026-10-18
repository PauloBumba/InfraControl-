using System.Text.Json;
using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T007b: SnmpRouterSessionsProvider — sessões TCP via tcpConnTable
/// (TCP-MIB RFC 4022). O INDEX é composto (localAddr.localPort.remAddr.
/// remPort); conta total/established/listen e o top de remotos (exclui o
/// wildcard 0.0.0.0 dos listen). Informativo — sem regra no evaluator.
/// </summary>
public sealed class SnmpRouterSessionsProviderTests
{
    private const string ConnTableOid = "1.3.6.1.2.1.6.13.1";

    private const string ColState = "1";
    private const string ColRemAddress = "4";

    // Instância composta (local 10.0.0.1:443 / rem 203.0.113.5:51515).
    private const string InstanceStub =
        "10.0.0.1.443.203.0.113.5.51515";

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
        string instance, string state, string remAddress)
    {
        yield return new($"{ConnTableOid}.{ColState}.{instance}", state);
        yield return new($"{ConnTableOid}.{ColRemAddress}.{instance}", remAddress);
    }

    private static IReadOnlyList<SnmpVariable> Table(params IEnumerable<SnmpVariable>[] rows)
        => [.. rows.SelectMany(row => row)];

    [Fact]
    public void NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new SnmpRouterSessionsProvider(new FakeSnmpClient(_ =>
            Table(Row(InstanceStub, "5", "203.0.113.5"))));

        provider.Name.Should().Be("SnmpRouterSessions");
        provider.CanHandle("SnmpRouterSessions").Should().BeTrue();
        provider.CanHandle("snmproutersessions").Should().BeTrue();
        provider.CanHandle("SnmpRouterRoutes").Should().BeFalse();
    }

    [Fact]
    public async Task Success_ShouldCountsAndTopRemotes()
    {
        // 2 established para o mesmo remoto + 1 listen (wildcard).
        var listenInstance = "10.0.0.1.22.0.0.0.0.0";
        var e1 = "10.0.0.1.443.203.0.113.5.51515";
        var e2 = "10.0.0.1.443.203.0.113.5.51516";
        var provider = new SnmpRouterSessionsProvider(new FakeSnmpClient(_ => Table(
            Row(listenInstance, "2", "0.0.0.0"),
            Row(e1, "5", "203.0.113.5"),
            Row(e2, "5", "203.0.113.5"))));

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeTrue();
        result.Values!["sessionCount"].Should().Be("3");
        result.Values["establishedCount"].Should().Be("2");
        result.Values["listenCount"].Should().Be("1");

        var top = JsonSerializer.Deserialize<List<JsonElement>>(
            result.Values["topRemoteIps"]!);
        top!.Should().ContainSingle();
        top[0].GetProperty("ip").GetString().Should().Be("203.0.113.5");
        top[0].GetProperty("connections").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Success_ShouldExcludeListenWildcardFromTopRemotes()
    {
        // Só listen (0.0.0.0) — topRemoteIps vazio, mas contagens ok.
        var listenInstance = "10.0.0.1.80.0.0.0.0.0";
        var provider = new SnmpRouterSessionsProvider(new FakeSnmpClient(_ =>
            Table(Row(listenInstance, "2", "0.0.0.0"))));

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeTrue();
        result.Values!["listenCount"].Should().Be("1");
        result.Values["topRemoteIps"].Should().Be("[]");
    }

    [Fact]
    public async Task Success_ShouldOrderTopRemotesByConnectionsThenIp()
    {
        // 2 remotos: A com 3, B com 1 — A primeiro; empate → ordem alfabética.
        var rows = new List<IEnumerable<SnmpVariable>>
        {
            Row("10.0.0.1.1.203.0.113.10.1000", "5", "203.0.113.10"),
            Row("10.0.0.1.2.203.0.113.10.1001", "5", "203.0.113.10"),
            Row("10.0.0.1.3.203.0.113.10.1002", "5", "203.0.113.10"),
            Row("10.0.0.1.4.203.0.113.20.1003", "5", "203.0.113.20")
        };
        var provider = new SnmpRouterSessionsProvider(new FakeSnmpClient(_ =>
            Table([.. rows])));

        var result = await provider.CheckAsync("192.0.2.1");

        var top = JsonSerializer.Deserialize<List<JsonElement>>(
            result.Values!["topRemoteIps"]!);
        top!.Should().HaveCount(2);
        top[0].GetProperty("ip").GetString().Should().Be("203.0.113.10");
        top[0].GetProperty("connections").GetInt32().Should().Be(3);
        top[1].GetProperty("ip").GetString().Should().Be("203.0.113.20");
    }

    [Fact]
    public async Task EmptyTable_ShouldFailWithPtBrMessage()
    {
        var provider = new SnmpRouterSessionsProvider(new FakeSnmpClient(_ =>
            Array.Empty<SnmpVariable>()));

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(
            "Nenhuma sessão TCP válida encontrada no router/firewall.");
    }

    [Fact]
    public async Task EmptyTarget_ShouldFailWithPtBrMessage()
    {
        var provider = new SnmpRouterSessionsProvider(new FakeSnmpClient(_ =>
            Array.Empty<SnmpVariable>()));

        var result = await provider.CheckAsync("");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino do SNMP é obrigatório.");
    }

    [Fact]
    public async Task SnmpThrow_ShouldFailWithPtBrMessage()
    {
        var provider = new SnmpRouterSessionsProvider(new ThrowingSnmpClient());

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Falha ao consultar SNMP: ");
    }

    [Fact]
    public async Task Walk_ShouldAskConnTableOid()
    {
        var client = new FakeSnmpClient(_ =>
            Table(Row(InstanceStub, "5", "203.0.113.5")));
        var provider = new SnmpRouterSessionsProvider(client);

        await provider.CheckAsync("192.0.2.1");

        client.AskedOids.Should().ContainSingle()
            .Which.Should().Be(ConnTableOid);
    }

    [Fact]
    public async Task RowWithoutState_ShouldBeIgnored()
    {
        // Linha só com remAddress (sem state) — descartada; sobra 1 válida.
        var orphanInstance = "10.0.0.1.9.203.0.113.9.9";
        var validInstance = "10.0.0.1.10.203.0.113.8.8";
        var provider = new SnmpRouterSessionsProvider(new FakeSnmpClient(_ =>
            Table(
                new[] { new SnmpVariable($"{ConnTableOid}.{ColRemAddress}.{orphanInstance}", "203.0.113.9") },
                Row(validInstance, "5", "203.0.113.8"))));

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeTrue();
        result.Values!["sessionCount"].Should().Be("1");
    }
}

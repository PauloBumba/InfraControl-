using System.Text.Json;
using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T007b: SnmpRouterSessionsProvider — sessões TCP via tcpConnTable
/// (TCP-MIB RFC 4022). O INDEX é composto (localAddr.localPort.remoteAddr.remotePort);
/// estados: listen(2), established(5), demais = other. Informativo — sem regra
/// no evaluator.
/// </summary>
public sealed class SnmpRouterSessionsProviderTests
{
    private const string ConnStateOid = "1.3.6.1.2.1.6.13.1.1";

    // Estado tcpConnState (RFC 4022)
    private const string StateListen = "2";
    private const string StateEstablished = "5";
    private const string StateOther = "1";

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

    private static IReadOnlyList<SnmpVariable> Table(params (string oid, string state)[] rows)
        => rows.Select(row => new SnmpVariable(row.oid, row.state)).ToArray();

    [Fact]
    public void NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new SnmpRouterSessionsProvider(new FakeSnmpClient(_ =>
            Table(("1.3.6.1.2.1.6.13.1.1.192.168.1.1.80.10.0.0.1.443", "5"))));

        provider.Name.Should().Be("SnmpRouterSessions");
        provider.CanHandle("SnmpRouterSessions").Should().BeTrue();
        provider.CanHandle("snmproutersessions").Should().BeTrue();
        provider.CanHandle("SnmpRouterRoutes").Should().BeFalse();
    }

    [Fact]
    public async Task Success_ShouldReportSessionCounts()
    {
        // 2 established + 1 listen + 1 other
        var provider = new SnmpRouterSessionsProvider(new FakeSnmpClient(_ => Table(
            ("1.3.6.1.2.1.6.13.1.1.192.168.1.1.80.10.0.0.1.443", "5"),
            ("1.3.6.1.2.1.6.13.1.1.192.168.1.1.80.10.0.0.2.443", "5"),
            ("1.3.6.1.2.1.6.13.1.1.0.0.0.0.22.0.0.0.0.0", "2"),
            ("1.3.6.1.2.1.6.13.1.1.192.168.1.1.12345.10.0.0.3.54321", "1"))));

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeTrue();
        result.Values!["sessionCount"].Should().Be("4");
        result.Values["establishedCount"].Should().Be("2");
        result.Values["listenCount"].Should().Be("1");
    }

    [Fact]
    public async Task Success_ShouldIncludeTopIpsInJson()
    {
        // 3 sessões do mesmo IP remoto + 2 de outro + 1 de terceiro
        var provider = new SnmpRouterSessionsProvider(new FakeSnmpClient(_ => Table(
            ("1.3.6.1.2.1.6.13.1.1.192.168.1.1.80.10.0.0.1.443", "5"),
            ("1.3.6.1.2.1.6.13.1.1.192.168.1.1.81.10.0.0.1.444", "5"),
            ("1.3.6.1.2.1.6.13.1.1.192.168.1.1.82.10.0.0.1.445", "5"),
            ("1.3.6.1.2.1.6.13.1.1.192.168.1.1.83.10.0.0.2.443", "5"),
            ("1.3.6.1.2.1.6.13.1.1.192.168.1.1.84.10.0.0.2.444", "5"),
            ("1.3.6.1.2.1.6.13.1.1.192.168.1.1.85.10.0.0.3.443", "5"))));

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeTrue();

        var topIps = JsonSerializer.Deserialize<List<JsonElement>>(
            result.Values["topIps"]!);
        topIps!.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task EmptyTable_ShouldFailWithPtBrMessage()
    {
        var provider = new SnmpRouterSessionsProvider(new FakeSnmpClient(_ =>
            Array.Empty<SnmpVariable>()));

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(
            "Nenhuma sessão TCP encontrada no router/firewall.");
    }

    [Fact]
    public async Task EmptyTarget_ShouldFailWithPtBrMessage()
    {
        var provider = new SnmpRouterSessionsProvider(new FakeSnmpClient(_ =>
            Array.Empty<SnmpVariable>()));

        var result = await provider.CheckAsync("  ");

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
    public async Task Walk_ShouldAskConnStateOid()
    {
        var client = new FakeSnmpClient(_ =>
            Table(("1.3.6.1.2.1.6.13.1.1.192.168.1.1.80.10.0.0.1.443", "5")));
        var provider = new SnmpRouterSessionsProvider(client);

        await provider.CheckAsync("192.0.2.1");

        client.AskedOids.Should().ContainSingle()
            .Which.Should().Be(ConnStateOid);
    }
}

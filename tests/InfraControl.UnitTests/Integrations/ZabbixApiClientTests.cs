using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Infrastructure.Integrations.Zabbix;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F27 Fase 2.1: cliente JSON-RPC do Zabbix — user.login com cache de
/// sessão, Bearer no Zabbix ≥ 8 (o 8.0 rejeita o campo auth — §1.2.1),
/// campo auth ≤ 7, re-login em sessão recusada e erros identificáveis
/// sem token em mensagem.
/// </summary>
public sealed class ZabbixApiClientTests
{
    private const string SessionToken = "sess-token-abc";

    [Fact]
    public async Task GetVersionAsync_ShouldCallApiinfoWithoutAuth()
    {
        var handler = new ScriptedHandler();
        handler.When(_ => true,
            _ => Rpc("""{"jsonrpc":"2.0","result":"8.0.0","id":1}"""));

        var client = CreateClient(handler);

        var version = await client.GetVersionAsync();

        version.Should().Be("8.0.0");
        var body = ReadBody(handler.Requests[0]);
        body.GetProperty("method").GetString().Should().Be("apiinfo.version");
        body.TryGetProperty("auth", out _).Should().BeFalse(
            "apiinfo.version não exige autenticação");
        handler.Requests[0].Headers.Contains("Authorization").Should().BeFalse();
    }

    [Fact]
    public async Task ListHostsAsync_OnZabbix8_ShouldLoginAndUseBearerHeader()
    {
        var handler = new ScriptedHandler();
        handler.When(r => MethodIs(r, "user.login"),
            _ => Rpc($$"""{"jsonrpc":"2.0","result":"{{SessionToken}}","id":1}"""));
        handler.When(r => MethodIs(r, "apiinfo.version"),
            _ => Rpc("""{"jsonrpc":"2.0","result":"8.0.0","id":1}"""));
        handler.When(r => MethodIs(r, "host.get"),
            _ => Rpc("""
                {"jsonrpc":"2.0","result":[],"id":1}
                """));

        var client = CreateClient(handler);

        await client.ListHostsAsync();

        var login = ReadBody(handler.Requests[0]);
        login.GetProperty("method").GetString().Should().Be("user.login");
        login.GetProperty("params").GetProperty("username").GetString().Should().Be("admin");

        var hostGet = handler.Requests[^1];
        hostGet.Headers.Contains("Authorization").Should().BeTrue();
        hostGet.Headers.GetValues("Authorization").Single()
            .Should().Be($"Bearer {SessionToken}");
        ReadBody(hostGet).TryGetProperty("auth", out _).Should().BeFalse(
            "no Zabbix 8 o campo auth é rejeitado — só Bearer");
    }

    [Fact]
    public async Task ListHostsAsync_OnZabbix7_ShouldUseAuthFieldInBody()
    {
        var handler = new ScriptedHandler();
        handler.When(r => MethodIs(r, "user.login"),
            _ => Rpc($$"""{"jsonrpc":"2.0","result":"{{SessionToken}}","id":1}"""));
        handler.When(r => MethodIs(r, "apiinfo.version"),
            _ => Rpc("""{"jsonrpc":"2.0","result":"7.0.4","id":1}"""));
        handler.When(r => MethodIs(r, "host.get"),
            _ => Rpc("""{"jsonrpc":"2.0","result":[],"id":1}"""));

        var client = CreateClient(handler);

        await client.ListHostsAsync();

        var hostGet = handler.Requests[^1];
        hostGet.Headers.Contains("Authorization").Should().BeFalse();
        ReadBody(hostGet).GetProperty("auth").GetString().Should().Be(SessionToken);
    }

    [Fact]
    public async Task ListHostsAsync_ShouldLoginOnlyOnceAcrossCalls()
    {
        var handler = new ScriptedHandler();
        handler.When(r => MethodIs(r, "user.login"),
            _ => Rpc($$"""{"jsonrpc":"2.0","result":"{{SessionToken}}","id":1}"""));
        handler.When(r => MethodIs(r, "apiinfo.version"),
            _ => Rpc("""{"jsonrpc":"2.0","result":"8.0.0","id":1}"""));
        handler.When(r => MethodIs(r, "host.get"),
            _ => Rpc("""{"jsonrpc":"2.0","result":[],"id":1}"""));
        handler.When(r => MethodIs(r, "hostgroup.get"),
            _ => Rpc("""{"jsonrpc":"2.0","result":[],"id":1}"""));

        var client = CreateClient(handler);

        await client.ListHostsAsync();
        await client.ListHostGroupsAsync();

        handler.Requests.Count(r => MethodIs(r, "user.login"))
            .Should().Be(1, "a sessão deve ser cacheada no cliente");
    }

    [Fact]
    public async Task ListHostGroupsAsync_ShouldNotSendRealHostsParameter()
    {
        // Regressão do smoke ao vivo (2026-10-10): o Zabbix 8.0 rejeita
        // `real_hosts` em hostgroup.get (parâmetro removido após o 7.x —
        // "unexpected parameter real_hosts", -32602).
        var handler = new ScriptedHandler();
        handler.When(r => MethodIs(r, "user.login"),
            _ => Rpc($$"""{"jsonrpc":"2.0","result":"{{SessionToken}}","id":1}"""));
        handler.When(r => MethodIs(r, "apiinfo.version"),
            _ => Rpc("""{"jsonrpc":"2.0","result":"8.0.0","id":1}"""));
        handler.When(r => MethodIs(r, "hostgroup.get"),
            _ => Rpc("""{"jsonrpc":"2.0","result":[{"groupid":"2","name":"Zabbix servers"}],"id":1}"""));

        var client = CreateClient(handler);

        var groups = await client.ListHostGroupsAsync();

        groups.Should().HaveCount(1);
        var parameters = ReadBody(handler.Requests[^1]).GetProperty("params");
        parameters.TryGetProperty("real_hosts", out _).Should().BeFalse(
            "o Zabbix 8.0 rejeita real_hosts — nunca enviar");
    }

    [Fact]
    public async Task ListHostsAsync_WhenSessionRejected_ShouldReloginAndRetryOnce()
    {
        var handler = new ScriptedHandler();
        handler.When(r => MethodIs(r, "user.login"),
            _ => Rpc($$"""{"jsonrpc":"2.0","result":"{{SessionToken}}","id":1}"""));
        handler.When(r => MethodIs(r, "apiinfo.version"),
            _ => Rpc("""{"jsonrpc":"2.0","result":"8.0.0","id":1}"""));
        handler.When(r => MethodIs(r, "host.get"),
            new Func<HttpRequestMessage, HttpResponseMessage>[]
            {
                _ => Rpc("""{"jsonrpc":"2.0","error":{"code":-32602,"message":"Session terminated, re-login,","data":"Session terminated, re-login."},"id":1}"""),
                _ => Rpc("""{"jsonrpc":"2.0","result":[],"id":1}""")
            });

        var client = CreateClient(handler);

        var act = async () => await client.ListHostsAsync();

        await act.Should().NotThrowAsync();
        handler.Requests.Count(r => MethodIs(r, "user.login")).Should().Be(2,
            "sessão recusada deve gerar re-login e uma nova tentativa");
    }

    [Fact]
    public async Task ListHostsAsync_OnJsonRpcError_ShouldThrowWithoutTokenInMessage()
    {
        var handler = new ScriptedHandler();
        handler.When(r => MethodIs(r, "user.login"),
            _ => Rpc($$"""{"jsonrpc":"2.0","result":"{{SessionToken}}","id":1}"""));
        handler.When(r => MethodIs(r, "apiinfo.version"),
            _ => Rpc("""{"jsonrpc":"2.0","result":"8.0.0","id":1}"""));
        handler.When(r => MethodIs(r, "host.get"),
            _ => Rpc("""{"jsonrpc":"2.0","error":{"code":-32601,"message":"Method not found.","data":"Incorrect API \"host.get\"."},"id":1}"""));

        var client = CreateClient(handler);

        var act = async () => await client.ListHostsAsync();

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("host.get").And.Contain("-32601");
        exception.Which.Message.Should().NotContain(SessionToken);
    }

    [Fact]
    public async Task ListHostsAsync_ShouldParseInterfacesTemplatesAndStatus()
    {
        var handler = new ScriptedHandler();
        handler.When(r => MethodIs(r, "user.login"),
            _ => Rpc($$"""{"jsonrpc":"2.0","result":"{{SessionToken}}","id":1}"""));
        handler.When(r => MethodIs(r, "apiinfo.version"),
            _ => Rpc("""{"jsonrpc":"2.0","result":"8.0.0","id":1}"""));
        handler.When(r => MethodIs(r, "host.get"),
            _ => Rpc("""
                {"jsonrpc":"2.0","result":[{
                  "hostid":"10084","host":"zbx-01","name":"Servidor Zabbix",
                  "status":"0","flags":"0",
                  "interfaces":[{"ip":"10.0.0.5","port":"10050","type":"1","main":"1"}],
                  "parentTemplates":[{"templateid":"10001"}],
                  "groups":[{"groupid":"4"}]
                }],"id":1}
                """));

        var client = CreateClient(handler);

        var hosts = await client.ListHostsAsync();

        hosts.Should().HaveCount(1);
        var host = hosts[0];
        host.HostId.Should().Be("10084");
        host.Host.Should().Be("zbx-01");
        host.Enabled.Should().BeTrue();
        host.Discovered.Should().BeFalse();
        host.Interfaces.Single().Ip.Should().Be("10.0.0.5");
        host.Interfaces.Single().Port.Should().Be(10050);
        host.TemplateIds.Should().ContainSingle().Which.Should().Be("10001");
        host.GroupIds.Should().ContainSingle().Which.Should().Be("4");
    }

    [Fact]
    public async Task ListHostsAsync_OnZabbix8HostGroups_ShouldParseHostgroupsAndRequestSelectHostGroups()
    {
        // Regressão F27: Zabbix 7.2+ renomeou groups → hostgroups; o 8.0
        // ignora selectGroups silenciosamente (sem erro, sem dados) — o
        // read-model ficava sem grupos. O cliente pede os dois selects e
        // lê os dois campos (só um vem preenchido por versão).
        var handler = new ScriptedHandler();
        handler.When(r => MethodIs(r, "user.login"),
            _ => Rpc($$"""{"jsonrpc":"2.0","result":"{{SessionToken}}","id":1}"""));
        handler.When(r => MethodIs(r, "apiinfo.version"),
            _ => Rpc("""{"jsonrpc":"2.0","result":"8.0.0","id":1}"""));
        handler.When(r => MethodIs(r, "host.get"),
            _ => Rpc("""
                {"jsonrpc":"2.0","result":[{
                  "hostid":"10818","host":"sw-192-168-127-1","name":"HP A5120",
                  "status":"0","flags":"0",
                  "interfaces":[{"ip":"192.168.127.1","port":"10051","type":"1","main":"1"}],
                  "parentTemplates":[{"templateid":"10564"}],
                  "hostgroups":[{"groupid":"22"}]
                }],"id":1}
                """));

        var client = CreateClient(handler);

        var hosts = await client.ListHostsAsync();

        hosts.Should().HaveCount(1);
        hosts[0].GroupIds.Should().ContainSingle().Which.Should().Be("22");

        var body = ReadBody(handler.Requests[^1]).GetProperty("params");
        body.GetProperty("selectHostGroups").EnumerateArray()
            .Select(e => e.GetString()).Should().ContainSingle().Which.Should().Be("groupid");
        body.GetProperty("selectGroups").EnumerateArray()
            .Select(e => e.GetString()).Should().ContainSingle().Which.Should().Be("groupid");
    }

    [Fact]
    public async Task ListProblemsAsync_ShouldParseObjectIdNormalizeRecoveryAndJoinHostsViaTriggers()
    {
        // Regressão F27 Fase 10.1: r_eventid "0" = sem recuperação; o
        // host vem do join objectid (triggerid) → trigger.get…selectHosts
        // (o problem.get do 8.0 removeu selectHosts).
        var handler = new ScriptedHandler();
        handler.When(r => MethodIs(r, "user.login"),
            _ => Rpc($$"""{"jsonrpc":"2.0","result":"{{SessionToken}}","id":1}"""));
        handler.When(r => MethodIs(r, "apiinfo.version"),
            _ => Rpc("""{"jsonrpc":"2.0","result":"8.0.0","id":1}"""));
        handler.When(r => MethodIs(r, "problem.get"),
            _ => Rpc("""
                {"jsonrpc":"2.0","result":[
                  {"eventid":"11","name":"ICMP Ping: Unavailable","severity":"4",
                   "clock":"1791679701","r_eventid":"0","objectid":"33179"},
                  {"eventid":"5","name":"Resolvido","severity":"2",
                   "clock":"1791670000","r_eventid":"88","objectid":"40001"}
                ],"id":1}
                """));
        handler.When(r => MethodIs(r, "trigger.get"),
            _ => Rpc("""
                {"jsonrpc":"2.0","result":[
                  {"triggerid":"33179","hosts":[
                    {"hostid":"10818","host":"sw-192-168-127-1","name":"HP A5120 (CORE CPD)"}]},
                  {"triggerid":"40001","hosts":[]}
                ],"id":1}
                """));

        var client = CreateClient(handler);

        var problems = await client.ListProblemsAsync();

        problems.Should().HaveCount(2);
        problems[0].RecoveryEventId.Should().BeNull("r_eventid 0 = ainda aberto");
        problems[0].ObjectId.Should().Be("33179");
        problems[0].HostNames.Should().Equal("HP A5120 (CORE CPD)");
        problems[1].RecoveryEventId.Should().Be("88");
        problems[1].HostNames.Should().BeEmpty();
    }

    [Fact]
    public async Task ListProblemsAsync_WhenTriggerLookupFails_ShouldReturnProblemsWithoutHosts()
    {
        var handler = new ScriptedHandler();
        handler.When(r => MethodIs(r, "user.login"),
            _ => Rpc($$"""{"jsonrpc":"2.0","result":"{{SessionToken}}","id":1}"""));
        handler.When(r => MethodIs(r, "apiinfo.version"),
            _ => Rpc("""{"jsonrpc":"2.0","result":"8.0.0","id":1}"""));
        handler.When(r => MethodIs(r, "problem.get"),
            _ => Rpc("""
                {"jsonrpc":"2.0","result":[
                  {"eventid":"11","name":"ICMP Ping: Unavailable","severity":"4",
                   "clock":"1791679701","r_eventid":"0","objectid":"33179"}
                ],"id":1}
                """));
        handler.When(r => MethodIs(r, "trigger.get"),
            _ => Rpc("""{"jsonrpc":"2.0","error":{"code":-32602,"message":"Invalid params.","data":"boom"},"id":1}"""));

        var client = CreateClient(handler);

        var problems = await client.ListProblemsAsync();

        problems.Should().ContainSingle();
        problems[0].EventId.Should().Be("11");
        problems[0].HostNames.Should().BeNull(
            "falha no join não derruba a lista — só fica sem hosts");
    }

    [Fact]
    public async Task GetHistoryAsync_ShouldApplyPeriodAndLimit()
    {
        var handler = new ScriptedHandler();
        handler.When(r => MethodIs(r, "user.login"),
            _ => Rpc($$"""{"jsonrpc":"2.0","result":"{{SessionToken}}","id":1}"""));
        handler.When(r => MethodIs(r, "apiinfo.version"),
            _ => Rpc("""{"jsonrpc":"2.0","result":"8.0.0","id":1}"""));
        handler.When(r => MethodIs(r, "history.get"),
            _ => Rpc("""
                {"jsonrpc":"2.0","result":[
                  {"itemid":"50001","clock":"1759900000","value":"42"}
                ],"id":1}
                """));

        var client = CreateClient(handler);
        var from = DateTimeOffset.FromUnixTimeSeconds(1759900000).UtcDateTime;
        var to = from.AddHours(1);

        var values = await client.GetHistoryAsync("50001", from, to, limit: 500);

        values.Should().HaveCount(1);
        values[0].Value.Should().Be("42");
        values[0].ClockUtc.Should().Be(from);

        var parameters = ReadBody(handler.Requests[^1]).GetProperty("params");
        parameters.GetProperty("limit").GetInt32().Should().Be(500);
        parameters.GetProperty("time_from").GetInt32().Should().Be(1759900000);
        parameters.GetProperty("time_till").GetInt32().Should().Be(1759903600);
        parameters.GetProperty("itemids")[0].GetString().Should().Be("50001");
    }

    [Fact]
    public async Task NotConfigured_ShouldThrowBeforeAnyHttpCall()
    {
        var handler = new ScriptedHandler();
        var client = CreateClient(handler, configure: false);

        var act = async () => await client.GetVersionAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não configurada*");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task HttpFailure_ShouldSurfaceAsHttpRequestException()
    {
        var handler = new ScriptedHandler();
        handler.When(_ => true,
            _ => throw new HttpRequestException("zabbix fora do ar"));

        var client = CreateClient(handler);

        var act = async () => await client.GetVersionAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── Infra ───────────────────────────────────────────────────────

    private static ZabbixApiClient CreateClient(
        HttpMessageHandler handler,
        string apiUrl = "http://zabbix.exemplo.test:8080",
        bool configure = true)
    {
        var factory = new SingleClientFactory(handler);
        var options = Options.Create(new ZabbixIntegrationOptions
        {
            ApiUrl = configure ? apiUrl : string.Empty,
            Username = "admin",
            Password = "senha-dev"
        });

        return new ZabbixApiClient(
            factory, options, NullLogger<ZabbixApiClient>.Instance);
    }

    private static bool MethodIs(HttpRequestMessage request, string method)
    {
        if (request.Content is null)
            return false;

        var body = ReadBody(request);
        return body.TryGetProperty("method", out var value) &&
               value.GetString() == method;
    }

    private static JsonElement ReadBody(HttpRequestMessage request)
    {
        var json = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static HttpResponseMessage Rpc(string json)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return response;
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(handler, disposeHandler: false);
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly List<(Func<HttpRequestMessage, bool> Match,
            Func<HttpRequestMessage, HttpResponseMessage> Respond)> _routes = [];

        public List<HttpRequestMessage> Requests { get; } = [];

        public void When(
            Func<HttpRequestMessage, bool> match,
            Func<HttpRequestMessage, HttpResponseMessage> respond)
            => _routes.Add((match, respond));

        public void When(
            Func<HttpRequestMessage, bool> match,
            params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
        {
            var index = 0;
            _routes.Add((match, request =>
                responses[Math.Min(index++, responses.Length - 1)](request)));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            foreach (var (match, respond) in _routes)
            {
                if (match(request))
                    return Task.FromResult(respond(request));
            }

            throw new InvalidOperationException(
                $"Nenhuma rota para {request.Method} {request.RequestUri}");
        }
    }
}

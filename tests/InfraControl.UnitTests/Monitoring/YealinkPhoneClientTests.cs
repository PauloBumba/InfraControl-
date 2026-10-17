using System.Net;
using System.Text;
using FluentAssertions;
using InfraControl.Infrastructure.Monitoring;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T006a: YealinkPhoneClient — GET HTTP/JSON nas rotas da API Yealink
/// (/api/phone/line, /api/phone/call/quality, /api/phone/status) com
/// parsing tolerante por chave (JsonDocument nativo). Seam testável via
/// HttpMessageHandler injectable — nenhuma rede real.
/// </summary>
public sealed class YealinkPhoneClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(
        string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static YealinkPhoneClient Client(StubHandler handler, string? user = null, string? pass = null)
        => new(Options.Create(new YealinkOptions
        {
            Username = user ?? "",
            Password = pass ?? ""
        }), handler);

    // ── GetSipInfoAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetSip_ShouldParseEnvelopeAndHitLineRoute()
    {
        var handler = new StubHandler(_ => Json(
            """{"code":0,"message":"Success","data":{"line":[{"index":1,"reg_status":"Registered","extension":"1001","sip_server":"pbx.local","codec":"G.711"}]}}"""));

        var info = await Client(handler).GetSipInfoAsync("192.0.2.70", TimeSpan.FromSeconds(3));

        info.Should().NotBeNull();
        info!.Registered.Should().BeTrue();
        info.Extension.Should().Be("1001");
        info.SipServer.Should().Be("pbx.local");
        info.Codec.Should().Be("G.711");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/phone/line");
        handler.LastRequest.RequestUri.Host.Should().Be("192.0.2.70");
    }

    [Fact]
    public async Task GetSip_Unregistered_ShouldParseFalse()
    {
        var handler = new StubHandler(_ => Json(
            """{"code":0,"data":{"line":{"reg_status":"Unregistered","extension":"1002"}}}"""));

        var info = await Client(handler).GetSipInfoAsync("192.0.2.71", TimeSpan.FromSeconds(3));

        info.Should().NotBeNull();
        info!.Registered.Should().BeFalse();
        info.Extension.Should().Be("1002");
    }

    [Fact]
    public async Task GetSip_StatusKeyVariants_ShouldBeTolerant()
    {
        var handler = new StubHandler(_ => Json(
            """{"data":{"line":{"registered":true,"account":"1003","server":"10.0.0.5"}}}"""));

        var info = await Client(handler).GetSipInfoAsync("192.0.2.72", TimeSpan.FromSeconds(3));

        info.Should().NotBeNull();
        info!.Registered.Should().BeTrue();
        info.Extension.Should().Be("1003");
        info.SipServer.Should().Be("10.0.0.5");
    }

    [Fact]
    public async Task GetSip_WithoutRecognizableFields_ShouldReturnNullAsUnsupported()
    {
        var handler = new StubHandler(_ => Json("""{"code":1,"message":"err"}"""));

        var info = await Client(handler).GetSipInfoAsync("192.0.2.73", TimeSpan.FromSeconds(3));

        info.Should().BeNull();
    }

    // ── GetCallQualityAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetQuality_ShouldParseMetricsAndHitQualityRoute()
    {
        var handler = new StubHandler(_ => Json(
            """{"code":0,"data":{"mos":4.2,"jitter":8.5,"latency":32.0,"packet_loss":1.25}}"""));

        var info = await Client(handler).GetCallQualityAsync("192.0.2.74", TimeSpan.FromSeconds(3));

        info.Should().NotBeNull();
        info!.Mos.Should().Be(4.2m);
        info.JitterMs.Should().Be(8.5m);
        info.LatencyMs.Should().Be(32.0m);
        info.PacketLossPercent.Should().Be(1.25m);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/phone/call/quality");
    }

    [Fact]
    public async Task GetQuality_NoCallActive_ShouldReturnNullAsUnsupported()
    {
        var handler = new StubHandler(_ => Json("""{"code":0,"data":{}}"""));

        var info = await Client(handler).GetCallQualityAsync("192.0.2.75", TimeSpan.FromSeconds(3));

        info.Should().BeNull();
    }

    // ── GetPoeStatusAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetPoe_ShouldParsePowerAndHitStatusRoute()
    {
        var handler = new StubHandler(_ => Json(
            """{"code":0,"data":{"poe_status":"up","power":3.1}}"""));

        var info = await Client(handler).GetPoeStatusAsync("192.0.2.76", TimeSpan.FromSeconds(3));

        info.Should().NotBeNull();
        info!.Powered.Should().BeTrue();
        info.PowerWatts.Should().Be(3.1m);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/phone/status");
    }

    [Fact]
    public async Task GetPoe_Down_ShouldParseFalse()
    {
        var handler = new StubHandler(_ => Json(
            """{"code":0,"data":{"poe":"down"}}"""));

        var info = await Client(handler).GetPoeStatusAsync("192.0.2.77", TimeSpan.FromSeconds(3));

        info.Should().NotBeNull();
        info!.Powered.Should().BeFalse();
    }

    // ── erros de transporte ──────────────────────────────────────────────

    [Fact]
    public async Task HttpError_ShouldThrowInvalidOperation()
    {
        var handler = new StubHandler(_ => Json("nope", HttpStatusCode.Unauthorized));

        var act = () => Client(handler).GetSipInfoAsync(
            "192.0.2.78", TimeSpan.FromSeconds(3));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*HTTP 401*");
    }

    [Fact]
    public async Task EmptyBody_ShouldReturnNull()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("", Encoding.UTF8, "application/json")
        });

        var info = await Client(handler).GetSipInfoAsync("192.0.2.79", TimeSpan.FromSeconds(3));

        info.Should().BeNull();
    }

    [Fact]
    public async Task InvalidJson_ShouldThrow()
    {
        var handler = new StubHandler(_ => Json("{not json"));

        var act = () => Client(handler).GetSipInfoAsync(
            "192.0.2.80", TimeSpan.FromSeconds(3));

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }

    [Fact]
    public async Task Ipv6Target_ShouldBeBracketedInUri()
    {
        var handler = new StubHandler(_ => Json(
            """{"code":0,"data":{"line":{"reg_status":"Registered","extension":"1001"}}}"""));

        await Client(handler).GetSipInfoAsync("2001:db8::1", TimeSpan.FromSeconds(3));

        handler.LastRequest!.RequestUri!.Host.Should().Be("[2001:db8::1]");
    }
}

using FluentAssertions;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F5-T005: provider HTTP/HTTPS do monitoring (CheckKind.HttpWan).
/// Decisões aprovadas: HTTPS com fallback HTTP, qualquer resposta HTTP
/// recebida conta como sucesso (erro de rede/timeout = falha) e o probe
/// valida o certificado TLS (self-signed = falha).
/// </summary>
public sealed class HttpWanMonitoringProviderTests
{
    private sealed class FakeHttpProbeClient(Func<string, HttpProbeReply> factory)
        : IHttpProbeClient
    {
        public List<string> Urls { get; } = [];

        public Task<HttpProbeReply> SendAsync(
            string url,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            Urls.Add(url);
            return Task.FromResult(factory(url));
        }
    }

    private sealed class ThrowingHttpProbeClient : IHttpProbeClient
    {
        public Task<HttpProbeReply> SendAsync(
            string url,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
            => throw new HttpRequestException("boom");
    }

    [Fact]
    public void Name_ShouldBeHttpWan()
        => new HttpWanMonitoringProvider(new FakeHttpProbeClient(_ => default!))
            .Name.Should().Be("HttpWan");

    [Theory]
    [InlineData("HttpWan")]
    [InlineData("httpwan")]
    [InlineData("HTTPWAN")]
    public void CanHandle_HttpWanKey_ShouldBeTrue(string key)
        => new HttpWanMonitoringProvider(new FakeHttpProbeClient(_ => default!))
            .CanHandle(key).Should().BeTrue();

    [Theory]
    [InlineData("Ping")]
    [InlineData("Http")]
    [InlineData("")]
    public void CanHandle_OtherKey_ShouldBeFalse(string key)
        => new HttpWanMonitoringProvider(new FakeHttpProbeClient(_ => default!))
            .CanHandle(key).Should().BeFalse();

    [Fact]
    public async Task CheckAsync_HttpsResponds_ShouldSucceedWithoutHttpFallback()
    {
        var client = new FakeHttpProbeClient(_ => new HttpProbeReply(true, 200));
        var provider = new HttpWanMonitoringProvider(client);

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        result.Values!["url"].Should().Be("https://192.0.2.1/");
        result.Values!["scheme"].Should().Be("https");
        result.Values!["status_code"].Should().Be("200");
        client.Urls.Should().Equal("https://192.0.2.1/");
    }

    [Fact]
    public async Task CheckAsync_HttpsFails_HttpFallbackResponds_ShouldSucceed()
    {
        var client = new FakeHttpProbeClient(url => url.StartsWith("https")
            ? new HttpProbeReply(false, null, "SSL handshake failed")
            : new HttpProbeReply(true, 302));
        var provider = new HttpWanMonitoringProvider(client);

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeTrue();
        result.Values!["url"].Should().Be("http://192.0.2.1/");
        result.Values!["scheme"].Should().Be("http");
        result.Values!["status_code"].Should().Be("302");
        client.Urls.Should().Equal("https://192.0.2.1/", "http://192.0.2.1/");
    }

    [Fact]
    public async Task CheckAsync_BothSchemesFail_ShouldFailWithPtBrErrorAndDetails()
    {
        var client = new FakeHttpProbeClient(url => url.StartsWith("https")
            ? new HttpProbeReply(false, null, "connection refused")
            : new HttpProbeReply(false, null, "timeout"));
        var provider = new HttpWanMonitoringProvider(client);

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("HTTP indisponível em 192.0.2.1.");
        result.Values!["https_error"].Should().Be("connection refused");
        result.Values!["http_error"].Should().Be("timeout");
        client.Urls.Should().HaveCount(2);
    }

    [Fact]
    public async Task CheckAsync_Ipv6Target_ShouldBracketUrl()
    {
        var client = new FakeHttpProbeClient(_ => new HttpProbeReply(true, 200));
        var provider = new HttpWanMonitoringProvider(client);

        var result = await provider.CheckAsync("2001:db8::1");

        result.Succeeded.Should().BeTrue();
        client.Urls.Should().Equal("https://[2001:db8::1]/");
    }

    [Fact]
    public async Task CheckAsync_EmptyTarget_ShouldFailWithoutCallingProbe()
    {
        var client = new FakeHttpProbeClient(_ => new HttpProbeReply(true, 200));
        var provider = new HttpWanMonitoringProvider(client);

        var result = await provider.CheckAsync("");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino do HTTP é obrigatório.");
        client.Urls.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAsync_ProbeThrows_ShouldFailWithPtBrErrorWithoutFallback()
    {
        var provider = new HttpWanMonitoringProvider(new ThrowingHttpProbeClient());

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Falha ao consultar HTTP:");
    }
}

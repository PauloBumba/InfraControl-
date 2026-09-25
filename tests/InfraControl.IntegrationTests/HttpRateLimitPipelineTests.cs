using System.Net;
using FluentAssertions;
using InfraControl.Api.RateLimiting;
using InfraControl.Application.Common.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace InfraControl.IntegrationTests;

/// <summary>
/// ADR-011 / docs/08 — pipeline HTTP real (Kestrel): middleware + GlobalLimiter,
/// 429 com envelope ApiResponse e Retry-After. O Redis é injetado como
/// indisponível de propósito para validar o fallback em memória sem depender
/// de ambiente; o caminho Redis está coberto nos testes unitários do limiter.
/// </summary>
public sealed class HttpRateLimitPipelineTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimit:Enabled"] = "true",
            ["RateLimit:Rules:Default:PermitLimit"] = "2",
            ["RateLimit:Rules:Default:WindowSeconds"] = "60",
            ["RateLimit:Rules:Login:PermitLimit"] = "1",
            ["RateLimit:Rules:Login:WindowSeconds"] = "60"
        });

        builder.Services.AddHttpRateLimiting(builder.Configuration);

        var redis = new Mock<IConnectionMultiplexer>();
        redis
            .Setup(connection => connection.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Throws(new RedisConnectionException(
                ConnectionFailureType.UnableToConnect, CommandFlags.None, "redis indisponível no teste", null, CommandStatus.Unknown));
        builder.Services.AddSingleton(redis.Object);

        _app = builder.Build();
        _app.UseRateLimiter();
        _app.MapGet("/ping", () => "ok");
        _app.MapGet("/login", () => "ok")
            .WithMetadata(new HttpRateLimitAttribute(RateLimitCategory.Login));

        await _app.StartAsync();

        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task Pipeline_ReturnsOkWithinLimitAnd429EnvelopeAfterLimit()
    {
        var first = await _client.GetAsync("/ping");
        var second = await _client.GetAsync("/ping");
        var third = await _client.GetAsync("/ping");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        third.Headers.RetryAfter?.Delta.Should().Be(TimeSpan.FromSeconds(60));

        var body = await third.Content.ReadAsStringAsync();
        body.Should().Contain("Limite de requisições excedido",
            "o 429 deve usar o envelope ApiResponse (F0-T023)");
        body.Should().Contain("\"isSuccess\":false");
    }

    [Fact]
    public async Task Pipeline_AppliesEndpointCategoryRule_WithoutLeakingToOtherEndpoints()
    {
        var firstLogin = await _client.GetAsync("/login");
        var secondLogin = await _client.GetAsync("/login");
        var ping = await _client.GetAsync("/ping");

        firstLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        secondLogin.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "a categoria Login permite 1 requisição por 60s");
        ping.StatusCode.Should().Be(HttpStatusCode.OK,
            "a categoria Default tem chave própria — não vaza a cota do Login");
    }
}

using System.Net;
using System.Text;
using FluentAssertions;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Infrastructure.Integrations.Unifi;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F20-T001/ADR-028 §2: cliente REST do UniFi Controller — login
/// POST /api/auth/login com cookie de sessão unifises, re-autenticação
/// automática em 401 (uma repetição) e listagem de dispositivos
/// (somente cadastro — sem telemetria persistida, regra 8).
/// </summary>
public sealed class UnifiApiClientTests
{
    // ── Login e sessão ──────────────────────────────────────────────

    [Fact]
    public async Task ListDevicesAsync_ShouldLoginAndCallStatDeviceWithSessionCookie()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-123; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/stat/device"),
            _ => Response(HttpStatusCode.OK, """
                {"meta":{"rc":"ok"},"data":[
                  {"mac":"aa:bb:cc:dd:ee:01","name":"gw-01","model":"UDM-Pro","type":"udm","ip":"192.168.1.1"},
                  {"mac":"aa:bb:cc:dd:ee:02","name":"sw-01","model":"USW-24-POE","type":"usw","ip":"192.168.1.2"}
                ]}
                """));

        var client = CreateClient(handler);

        var devices = await client.ListDevicesAsync();

        devices.Should().HaveCount(2);
        devices[0].Mac.Should().Be("aa:bb:cc:dd:ee:01");
        devices[0].Name.Should().Be("gw-01");
        devices[0].Model.Should().Be("UDM-Pro");
        devices[0].Type.Should().Be("udm");
        devices[0].Ip.Should().Be("192.168.1.1");

        handler.Requests.Should().HaveCount(2, "login + stat/device");
        handler.Requests[1].Headers.GetValues("Cookie")
            .Single().Should().Contain("unifises=sess-123");
        handler.Requests[1].RequestUri!.ToString()
            .Should().Contain("/proxy/network/api/s/default/stat/device");
    }

    [Fact]
    public async Task ListDevicesAsync_OnUnauthorized_ShouldReauthenticateOnceAndRetry()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-1; Path=/"),
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-2; Path=/"));
        var statCalls = 0;
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/stat/device"), _ =>
        {
            statCalls++;
            return statCalls == 1
                ? Response(HttpStatusCode.Unauthorized)
                : Response(HttpStatusCode.OK,
                    """{"meta":{"rc":"ok"},"data":[{"mac":"aa:bb:cc:dd:ee:03","name":"ap-01","model":"U6","type":"uap","ip":"192.168.1.3"}]}""");
        });

        var client = CreateClient(handler);

        var devices = await client.ListDevicesAsync();

        devices.Should().HaveCount(1);
        statCalls.Should().Be(2, "o 401 deve custar exatamente uma repetição");
        // login inicial + re-login do 401
        handler.Requests.Count(r => r.RequestUri!.AbsolutePath == "/api/auth/login")
            .Should().Be(2);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldThrowPtBr()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.Unauthorized));

        var client = CreateClient(handler);

        var send = () => client.ListDevicesAsync();

        (await send.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("*Credenciais*UniFi*");
    }

    [Fact]
    public async Task ListDevicesAsync_WhenNotConfigured_ShouldThrowPtBr()
    {
        var handler = new ScriptedHandler();
        var client = CreateClient(handler, baseUrl: string.Empty);

        var send = () => client.ListDevicesAsync();

        (await send.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Integração UniFi não configurada*");
        handler.Requests.Should().BeEmpty("sem configuração nada é chamado");
    }

    // ── Health (ADR-028 §5) ─────────────────────────────────────────

    [Fact]
    public async Task IsHealthyAsync_WhenSelfEndpointOk_ShouldReturnTrue()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=s; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/self",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}"""));

        var client = CreateClient(handler);

        (await client.IsHealthyAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task IsHealthyAsync_WhenControllerDown_ShouldReturnFalse()
    {
        var handler = new ScriptedHandler();
        handler.When(r => true, _ => throw new HttpRequestException("controller fora do ar"));

        var client = CreateClient(handler);

        (await client.IsHealthyAsync()).Should().BeFalse();
    }

    // ── Infra ───────────────────────────────────────────────────────

    private static UnifiApiClient CreateClient(
        HttpMessageHandler handler, string baseUrl = "https://unifi.exemplo.test")
    {
        var factory = new SingleClientFactory(handler);
        var options = Options.Create(new UnifiIntegrationOptions
        {
            BaseUrl = baseUrl,
            Username = "admin",
            Password = "senha-dev"
        });

        return new UnifiApiClient(
            factory, options, NullLogger<UnifiApiClient>.Instance);
    }

    private static HttpResponseMessage Response(
        HttpStatusCode statusCode, string? json = null, string? setCookie = null)
    {
        var response = new HttpResponseMessage(statusCode);
        if (json is not null)
            response.Content = new StringContent(json, Encoding.UTF8, "application/json");
        if (setCookie is not null)
            response.Headers.TryAddWithoutValidation("Set-Cookie", setCookie);
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

            return Task.FromResult(Response(HttpStatusCode.NotFound));
        }
    }
}

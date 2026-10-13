using System.Net;
using FluentAssertions;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.Webhook;
using InfraControl.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// F19-T007/ADR-028 §6: HttpWebhookNotificationDispatcher (POST real no
/// n8n, substitui o adaptador de log) — payload do POST, header
/// X-InfraControl-Secret, falha em 5xx e as policies de resiliência
/// (retry 3× 1s/2s/4s jitter + circuit breaker 5 falhas/30s → 60s).
/// </summary>
public sealed class HttpWebhookNotificationDispatcherTests
{
    // ── Payload e header ─────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_ShouldPostJsonPayloadToConfiguredUrl()
    {
        var recording = new RecordingHandler();
        var deliveryId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var dispatcher = Create(recording, secret: string.Empty);
        await dispatcher.SendAsync(new WebhookNotificationMessage(
            deliveryId, notificationId, userId,
            "https://n8n.exemplo.test/webhook/infracontrol",
            "Incidente resolvido",
            "O incidente 'Carga de CPU alta' foi resolvido.",
            DateTime.UtcNow,
            EventType: "IncidentResolvedDomainEvent"));

        recording.Request.Should().NotBeNull();
        recording.Request!.Method.Should().Be(HttpMethod.Post);
        recording.Request.RequestUri!.ToString()
            .Should().Be("https://n8n.exemplo.test/webhook/infracontrol");
        recording.Body.Should().Contain(deliveryId.ToString())
            .And.Contain(notificationId.ToString())
            .And.Contain(userId.ToString())
            .And.Contain("Incidente resolvido")
            // Fase 05: o payload leva o eventType p/ o switch do n8n.
            .And.Contain("IncidentResolvedDomainEvent");

        // sem Secret configurado → sem header de assinatura
        recording.Request.Headers.Contains("X-InfraControl-Secret").Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_WithSecret_ShouldAddInfraControlSecretHeader()
    {
        var recording = new RecordingHandler();

        var dispatcher = Create(recording, secret: "token-do-n8n");
        await dispatcher.SendAsync(Message());

        recording.Request!.Headers.GetValues("X-InfraControl-Secret")
            .Single().Should().Be("token-do-n8n");
    }

    [Fact]
    public async Task SendAsync_OnServerError_ShouldThrowHttpRequestException()
    {
        var recording = new RecordingHandler(HttpStatusCode.InternalServerError);

        var dispatcher = Create(recording, secret: string.Empty);

        // 5xx final (as policies de retry ficam no handler da DI) → erro
        // → o NotificationWebhookWorker marca a delivery Failed.
        var send = () => dispatcher.SendAsync(Message());

        await send.Should().ThrowAsync<HttpRequestException>();
    }

    // ── Policies (spec F19-T007/ADR-028 §6) ─────────────────────────

    [Fact]
    public void RetryOptions_ShouldMatchSpecOfThreeExponentialRetries()
    {
        var options = HttpWebhookNotificationDispatcher.CreateRetryOptions();

        options.MaxRetryAttempts.Should().Be(3);
        options.Delay.Should().Be(TimeSpan.FromSeconds(1));
        options.BackoffType.Should().Be(DelayBackoffType.Exponential);
        options.UseJitter.Should().BeTrue();
    }

    [Fact]
    public void CircuitBreakerOptions_ShouldMatchSpecOfFiveFailuresInThirtySeconds()
    {
        var options = HttpWebhookNotificationDispatcher.CreateCircuitBreakerOptions();

        options.FailureRatio.Should().Be(1.0);
        options.MinimumThroughput.Should().Be(5);
        options.SamplingDuration.Should().Be(TimeSpan.FromSeconds(30));
        options.BreakDuration.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task Resilience_Retry_ShouldAttemptFourTimesBeforeGivingUp()
    {
        var pipeline = BuildPipeline();
        var attempts = 0;

        async ValueTask<HttpResponseMessage> Flaky(CancellationToken cancellationToken)
        {
            attempts++;
            if (attempts < 4)
                throw new HttpRequestException("n8n fora do ar");

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        var result = await pipeline.ExecuteAsync(Flaky, CancellationToken.None);

        attempts.Should().Be(4);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Resilience_CircuitBreaker_ShouldOpenAfterFiveFailures()
    {
        var pipeline = BuildPipeline();
        var attempts = 0;

        async ValueTask<HttpResponseMessage> AlwaysFail(CancellationToken cancellationToken)
        {
            attempts++;
            throw new HttpRequestException("n8n fora do ar");
        }

        // 1ª execução = 4 tentativas do retry; 2ª = 5ª falha abre o
        // circuito → daí em diante o endpoint não é mais chamado.
        BrokenCircuitException? opened = null;
        for (var i = 0; i < 10 && opened is null; i++)
        {
            try
            {
                await pipeline.ExecuteAsync(AlwaysFail, CancellationToken.None);
            }
            catch (BrokenCircuitException exception)
            {
                opened = exception;
            }
            catch (HttpRequestException)
            {
                // retry esgotado — normal antes do circuito abrir
            }
        }

        opened.Should().NotBeNull("após 5 falhas o circuito deve abrir");
        var attemptsWhenOpen = attempts;

        var afterOpen = await Record.ExceptionAsync(
            async () => await pipeline.ExecuteAsync(AlwaysFail, CancellationToken.None));

        afterOpen.Should().BeOfType<BrokenCircuitException>();
        attempts.Should().Be(attemptsWhenOpen, "o circuito aberto não pode chamar o endpoint");
    }

    private static ResiliencePipeline<HttpResponseMessage> BuildPipeline()
    {
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();
        HttpWebhookNotificationDispatcher.ConfigureResilience(
            builder, TimeSpan.FromMilliseconds(5));
        return builder.Build();
    }

    private static WebhookNotificationMessage Message()
        => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "https://n8n.exemplo.test/webhook/infracontrol",
            "Alerta resolvido", "O alerta 'sw-01' foi resolvido.",
            DateTime.UtcNow);

    private static HttpWebhookNotificationDispatcher Create(
        HttpMessageHandler handler, string secret)
    {
        var factory = new SingleClientFactory(handler);
        var options = Options.Create(new NotificationChannelsOptions
        {
            Webhook = { Secret = secret }
        });

        return new HttpWebhookNotificationDispatcher(
            factory, options,
            NullLogger<HttpWebhookNotificationDispatcher>.Instance);
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode);
        }
    }
}

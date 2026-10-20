using System.Net;
using System.Text.Json;
using FluentAssertions;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.WhatsApp;
using InfraControl.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// ADR-059 §4 (F33-T002): EvolutionApiWhatsAppNotificationDispatcher —
/// POST /message/sendText/{instance} com header apikey, GroupId prioritário
/// sobre Phone, validações pt-BR de config, falha em 5xx e as policies de
/// resiliência (retry 3× 1s/2s/4s jitter + CB 5/30s → 60s), espelho do
/// HttpWebhookNotificationDispatcher.
/// </summary>
public sealed class EvolutionApiWhatsAppNotificationDispatcherTests
{
    // ── Contrato do POST ─────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_ShouldPostSendTextWithGroupIdAndApikeyHeader()
    {
        var recording = new RecordingHandler();
        var deliveryId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var dispatcher = Create(recording, groupId: "120363431225988863@g.us", phone: string.Empty);
        await dispatcher.SendAsync(new WhatsAppNotificationMessage(
            deliveryId, Guid.NewGuid(), userId,
            Phone: string.Empty,
            Title: "Incidente aberto",
            Message: "O incidente 'Carga de CPU alta' foi aberto (High).",
            CreatedAt: new DateTime(2026, 10, 18, 14, 33, 0, DateTimeKind.Utc)));

        recording.Request.Should().NotBeNull();
        recording.Request!.Method.Should().Be(HttpMethod.Post);
        recording.Request.RequestUri!.ToString()
            .Should().Be("http://evolution.exemplo.test/message/sendText/infracontrol");
        recording.Request.Headers.GetValues("apikey").Single()
            .Should().Be("chave-secreta");

        var body = JsonDocument.Parse(recording.Body!);
        body.RootElement.GetProperty("number").GetString()
            .Should().Be("120363431225988863@g.us", "GroupId é prioritário (ADR-059 §3)");
        body.RootElement.GetProperty("text").GetString()
            .Should().Contain("Incidente aberto")
            .And.Contain("Carga de CPU alta")
            .And.Contain("18/10/2026 14:33");
    }

    [Fact]
    public async Task SendAsync_WithoutGroupId_FallsBackToPhone()
    {
        var recording = new RecordingHandler();

        var dispatcher = Create(recording, groupId: string.Empty, phone: "5511999999999");
        await dispatcher.SendAsync(Message(phone: "5511999999999"));

        var body = JsonDocument.Parse(recording.Body!);
        body.RootElement.GetProperty("number").GetString()
            .Should().Be("5511999999999", "sem Grupo o destino é o Phone (retrocompatível)");
    }

    // ── Validações de config (pt-BR) ─────────────────────────────────

    [Fact]
    public async Task SendAsync_WithoutGroupIdAndPhone_ThrowsPtBrConfigError()
    {
        var dispatcher = Create(new RecordingHandler(), groupId: string.Empty, phone: string.Empty);

        var send = () => dispatcher.SendAsync(Message(phone: string.Empty));

        (await send.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*GroupId ou Phone*");
    }

    [Fact]
    public async Task SendAsync_WithoutApiKey_ThrowsPtBrConfigError()
    {
        var dispatcher = Create(new RecordingHandler(), apiKey: string.Empty);

        var send = () => dispatcher.SendAsync(Message());

        (await send.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*ApiKey*");
    }

    [Fact]
    public async Task SendAsync_WithoutInstanceId_ThrowsPtBrConfigError()
    {
        var dispatcher = Create(new RecordingHandler(), instanceId: string.Empty);

        var send = () => dispatcher.SendAsync(Message());

        (await send.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*InstanceId*");
    }

    [Fact]
    public async Task SendAsync_OnServerError_ShouldThrowHttpRequestException()
    {
        var recording = new RecordingHandler(HttpStatusCode.InternalServerError);
        var dispatcher = Create(recording);

        // 5xx final (as policies de retry ficam no handler da DI) → erro
        // → o NotificationWhatsAppWorker marca a delivery Failed.
        var send = () => dispatcher.SendAsync(Message());

        await send.Should().ThrowAsync<HttpRequestException>();
    }

    // ── Policies (spec ADR-059 §4 — idêntica ao n8n) ─────────────────

    // ── F33-T005: sendMedia + fallback texto ──────────────────────────

    [Fact]
    public async Task SendAsync_WithChart_ShouldPostSendMediaWithCaptionAndPng()
    {
        var recording = new RecordingHandler();
        var dispatcher = Create(recording);
        var message = Message() with { Chart = Chart() };

        await dispatcher.SendAsync(message);

        recording.Request!.RequestUri!.ToString()
            .Should().Be("http://evolution.exemplo.test/message/sendMedia/infracontrol");
        recording.Request.Headers.GetValues("apikey").Single()
            .Should().Be("chave-secreta");

        var body = JsonDocument.Parse(recording.Body!);
        body.RootElement.GetProperty("number").GetString()
            .Should().Be("120363431225988863@g.us");
        body.RootElement.GetProperty("mediatype").GetString().Should().Be("image");
        body.RootElement.GetProperty("mimetype").GetString().Should().Be("image/png");
        body.RootElement.GetProperty("fileName").GetString()
            .Should().Be("infracontrol-evento.png");
        body.RootElement.GetProperty("caption").GetString()
            .Should().Contain("Alerta resolvido")
            .And.Contain("sw-01");
        var media = body.RootElement.GetProperty("media").GetString()!;
        Convert.FromBase64String(media).Should().StartWith([0x89, 0x50, 0x4E, 0x47]);
    }

    [Fact]
    public async Task SendAsync_WhenRendererFails_ShouldFallbackToText()
    {
        var recording = new RecordingHandler();
        var dispatcher = Create(recording, renderer: new ThrowingRenderer());

        await dispatcher.SendAsync(Message() with { Chart = Chart() });

        recording.Request!.RequestUri!.ToString()
            .Should().EndWith("/message/sendText/infracontrol",
                "geração do PNG falhou → o evento NUNCA fica por enviar");
        var body = JsonDocument.Parse(recording.Body!);
        body.RootElement.GetProperty("text").GetString()
            .Should().Contain("Alerta resolvido");
    }

    [Fact]
    public async Task SendAsync_WhenMediaFails_ShouldFallbackToText()
    {
        var recording = new RecordingHandler(HttpStatusCode.InternalServerError);
        var dispatcher = Create(recording);

        var send = () => dispatcher.SendAsync(Message() with { Chart = Chart() });

        // Sem Polly no harness de teste, o 5xx do sendMedia vira exceção
        // INTERNAMENTE tratada → degrada para sendText (que aqui também
        // responde 5xx → a exceção final sobe para o worker).
        await send.Should().ThrowAsync<HttpRequestException>();

        recording.Request!.RequestUri!.ToString()
            .Should().EndWith("/message/sendText/infracontrol",
                "falha de rede no sendMedia → fallback texto");
    }

    [Fact]
    public void RetryOptions_ShouldMatchSpecOfThreeExponentialRetries()
    {
        var options = EvolutionApiWhatsAppNotificationDispatcher.CreateRetryOptions();

        options.MaxRetryAttempts.Should().Be(3);
        options.Delay.Should().Be(TimeSpan.FromSeconds(1));
        options.BackoffType.Should().Be(DelayBackoffType.Exponential);
        options.UseJitter.Should().BeTrue();
    }

    [Fact]
    public void CircuitBreakerOptions_ShouldMatchSpecOfFiveFailuresInThirtySeconds()
    {
        var options = EvolutionApiWhatsAppNotificationDispatcher.CreateCircuitBreakerOptions();

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
                throw new HttpRequestException("Evolution fora do ar");

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
            throw new HttpRequestException("Evolution fora do ar");
        }

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
        EvolutionApiWhatsAppNotificationDispatcher.ConfigureResilience(
            builder, TimeSpan.FromMilliseconds(5));
        return builder.Build();
    }

    private static WhatsAppNotificationMessage Message(string phone = "5511999999999")
        => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            phone,
            "Alerta resolvido", "O alerta 'sw-01' foi resolvido.",
            DateTime.UtcNow);

    private static IncidentChartData Chart()
        => new(
            ChartCardKind.Incident, "Alerta Critical criado", "sw-01",
            "Critical", DateTime.UtcNow,
            [
                new ChartSeriesPoint(DateTime.UtcNow.AddMinutes(-30), 92),
                new ChartSeriesPoint(DateTime.UtcNow.AddMinutes(-10), 98)
            ]);

    private static EvolutionApiWhatsAppNotificationDispatcher Create(
        HttpMessageHandler handler,
        string groupId = "120363431225988863@g.us",
        string phone = "5511999999999",
        string baseUrl = "http://evolution.exemplo.test",
        string apiKey = "chave-secreta",
        string instanceId = "infracontrol",
        IIncidentImageRenderer? renderer = null)
    {
        var factory = new SingleClientFactory(handler);
        var options = Options.Create(new NotificationChannelsOptions
        {
            WhatsApp =
            {
                GroupId = groupId,
                Phone = phone,
                BaseUrl = baseUrl,
                ApiKey = apiKey,
                InstanceId = instanceId
            }
        });

        return new EvolutionApiWhatsAppNotificationDispatcher(
            factory, options,
            renderer ?? new FakeRenderer(),
            NullLogger<EvolutionApiWhatsAppNotificationDispatcher>.Instance);
    }

    private sealed class FakeRenderer : IIncidentImageRenderer
    {
        public byte[] RenderPng(IncidentChartData data)
            => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    }

    private sealed class ThrowingRenderer : IIncidentImageRenderer
    {
        public byte[] RenderPng(IncidentChartData data)
            => throw new InvalidOperationException("fonte indisponível");
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
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

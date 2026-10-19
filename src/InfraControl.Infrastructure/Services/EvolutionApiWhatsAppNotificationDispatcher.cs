using System.Net.Http.Json;
using System.Text.Json.Serialization;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.WhatsApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// ADR-059 §4 (F33-T002): dispatcher REAL do canal WhatsApp via Evolution
/// API — substitui <see cref="LoggingWhatsAppNotificationDispatcher"/>
/// condicionalmente (BaseUrl vazio = logging preservado, opt-in). POST em
/// <c>{BaseUrl}/message/sendText/{InstanceId}</c> com header <c>apikey</c> e
/// corpo <c>{ number, text }</c> (contrato real validado no spike F33-T001 —
/// o formato <c>textMessage.text</c> das docs v1 NÃO existe na v2.3.7).
/// Resiliência idêntica ao n8n (retry 3× 1s/2s/4s jitter + CB 5/30s→60s;
/// 4xx sem retry). REGRA (ADR-059 §1/regra 7): só saída de notificação — a
/// Evolution nunca decide estado de Incident/Alert no InfraControl.
/// </summary>
public sealed class EvolutionApiWhatsAppNotificationDispatcher(
    IHttpClientFactory httpClientFactory,
    IOptions<NotificationChannelsOptions> channelsOptions,
    ILogger<EvolutionApiWhatsAppNotificationDispatcher> logger) : IWhatsAppNotificationDispatcher
{
    /// <summary>Nome do client (policies Polly registradas na DI).</summary>
    public const string HttpClientName = "evolution-api";

    /// <summary>ADR-059 §4: mesmo spec do dispatcher n8n (F19-T007).</summary>
    public static void ConfigureResilience(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        TimeSpan? retryDelay = null)
        => builder
            .AddRetry(CreateRetryOptions(retryDelay))
            .AddCircuitBreaker(CreateCircuitBreakerOptions());

    /// <summary>Especificação do retry — exposta para teste.</summary>
    public static RetryStrategyOptions<HttpResponseMessage> CreateRetryOptions(
        TimeSpan? retryDelay = null)
        => new()
        {
            MaxRetryAttempts = 3,
            Delay = retryDelay ?? TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = Failures()
        };

    /// <summary>Especificação do circuit breaker — exposta para teste.</summary>
    public static CircuitBreakerStrategyOptions<HttpResponseMessage> CreateCircuitBreakerOptions()
        => new()
        {
            FailureRatio = 1.0,
            MinimumThroughput = 5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(60),
            ShouldHandle = Failures()
        };

    private static PredicateBuilder<HttpResponseMessage> Failures()
        => new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(static response => (int)response.StatusCode >= 500);

    public async Task SendAsync(
        WhatsAppNotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        var whatsapp = channelsOptions.Value.WhatsApp;

        if (string.IsNullOrWhiteSpace(whatsapp.BaseUrl))
            throw new InvalidOperationException(
                "Canal WhatsApp sem Evolution:BaseUrl configurado (Notifications:Channels:WhatsApp:BaseUrl).");

        if (string.IsNullOrWhiteSpace(whatsapp.InstanceId))
            throw new InvalidOperationException(
                "Canal WhatsApp sem Evolution:InstanceId configurado (Notifications:Channels:WhatsApp:InstanceId).");

        if (string.IsNullOrWhiteSpace(whatsapp.ApiKey))
            throw new InvalidOperationException(
                "Canal WhatsApp sem Evolution:ApiKey configurado (Notifications:Channels:WhatsApp:ApiKey).");

        // ADR-059 §3: GroupId (grupo) é prioritário sobre Phone (número).
        var number = whatsapp.GroupId;
        if (string.IsNullOrWhiteSpace(number))
            number = message.Phone;

        if (string.IsNullOrWhiteSpace(number))
            throw new InvalidOperationException(
                "Canal WhatsApp sem destino (Notifications:Channels:WhatsApp:GroupId ou Phone).");

        var client = httpClientFactory.CreateClient(HttpClientName);
        var url = $"{whatsapp.BaseUrl.TrimEnd('/')}/message/sendText/{whatsapp.InstanceId}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new EvolutionSendText(number, BuildText(message)))
        };
        request.Headers.TryAddWithoutValidation("apikey", whatsapp.ApiKey);

        using var response = await client.SendAsync(request, cancellationToken);

        // Resposta final não-2xx (após o retry do handler) → exceção → o
        // worker marca a delivery Failed com o erro pt-BR.
        response.EnsureSuccessStatusCode();

        logger.LogInformation(
            "WhatsApp {DeliveryId} enviado via Evolution para {Number} (usuário {UserId}): HTTP {StatusCode}.",
            message.DeliveryId, number, message.UserId, (int)response.StatusCode);
    }

    /// <summary>
    /// Texto simples do WhatsApp (ADR-059 §4): título + mensagem + data do
    /// evento. Com o gráfico habilitado (F33-T005) este mesmo texto vira a
    /// caption do sendMedia.
    /// </summary>
    private static string BuildText(WhatsAppNotificationMessage message)
        => $"{message.Title}\n{message.Message}\n{message.CreatedAt:dd/MM/yyyy HH:mm}";

    /// <summary>Corpo do POST — contrato real da v2.3.7 validado no spike.</summary>
    private sealed record EvolutionSendText(
        [property: JsonPropertyName("number")] string Number,
        [property: JsonPropertyName("text")] string Text);
}

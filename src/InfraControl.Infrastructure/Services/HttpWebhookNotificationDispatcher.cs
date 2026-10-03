using System.Net.Http.Json;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.Webhook;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// F19-T007/ADR-028: POST real no n8n via <c>IHttpClientFactory</c> com
/// resiliência Polly no handler (§6: retry 3× backoff exponencial 1s/2s/4s
/// com jitter + circuit breaker — 5 falhas em 30s abrem por 60s). Header
/// <c>X-InfraControl-Secret</c> quando Notifications:Channels:Webhook:Secret
/// está preenchido (ADR-028 §2).
/// REGRA (regra 7 da revisão F19/F20/F21): o n8n manda mensagem e executa
/// automações, mas NÃO decide <c>Incident = Resolved</c> — o InfraControl é
/// o único dono do estado do Incidente; este dispatcher é só saída de
/// notificação, não aceita comando de volta.
/// </summary>
public sealed class HttpWebhookNotificationDispatcher(
    IHttpClientFactory httpClientFactory,
    IOptions<NotificationChannelsOptions> channelsOptions,
    ILogger<HttpWebhookNotificationDispatcher> logger) : IWebhookNotificationDispatcher
{
    /// <summary>Nome do client (policies Polly registradas na DI).</summary>
    public const string HttpClientName = "n8n-webhook";

    /// <summary>
    /// F19-T007/ADR-028 §6: retry 3× backoff exponencial (1s, 2s, 4s) com
    /// jitter + circuit breaker (5 falhas em 30s → circuito aberto 60s).
    /// Trata erro de rede (HttpRequestException) e resposta 5xx; 4xx não
    /// faz retry (erro do chamador, não transitório). <paramref name="retryDelay"/>
    /// existe só para os testes acelerarem o backoff — a DI usa o default 1s.
    /// </summary>
    public static void ConfigureResilience(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        TimeSpan? retryDelay = null)
        => builder
            .AddRetry(CreateRetryOptions(retryDelay))
            .AddCircuitBreaker(CreateCircuitBreakerOptions());

    /// <summary>Especificação do retry (F19-T007) — exposta para teste.</summary>
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

    /// <summary>Especificação do circuit breaker (F19-T007) — exposta para teste.</summary>
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
        WebhookNotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(
            HttpMethod.Post, message.Url)
        {
            Content = JsonContent.Create(new WebhookPayload(
                message.DeliveryId,
                message.NotificationId,
                message.UserId,
                message.Title,
                message.Message,
                message.CreatedAt))
        };

        var secret = channelsOptions.Value.Webhook.Secret;
        if (!string.IsNullOrWhiteSpace(secret))
            request.Headers.TryAddWithoutValidation("X-InfraControl-Secret", secret);

        using var response = await client.SendAsync(request, cancellationToken);

        // Resposta final não-2xx (após o retry do handler) → exceção → o
        // worker marca a delivery Failed com o erro pt-BR.
        response.EnsureSuccessStatusCode();

        logger.LogInformation(
            "Webhook {DeliveryId} enviado para {Url} (usuário {UserId}): HTTP {StatusCode}.",
            message.DeliveryId, message.Url, message.UserId, (int)response.StatusCode);
    }

    /// <summary>Corpo do POST para o n8n (contrato de saída F19-T007).</summary>
    private sealed record WebhookPayload(
        Guid DeliveryId,
        Guid NotificationId,
        Guid UserId,
        string Title,
        string Message,
        DateTime CreatedAt);
}

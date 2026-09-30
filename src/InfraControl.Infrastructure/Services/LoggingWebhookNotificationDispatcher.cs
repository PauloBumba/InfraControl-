using InfraControl.Application.Notifications.Webhook;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Adaptador de log do canal Webhook (F9-T008) — espelho do
/// <see cref="LoggingWhatsAppNotificationDispatcher"/>: nenhum POST real;
/// um adaptador com HTTP (IHttpClientFactory) troca esta implementação
/// sem tocar em Application (endpoint real = pendência, junto do SMTP e
/// das credenciais dos demais canais).
/// </summary>
public sealed class LoggingWebhookNotificationDispatcher(
    ILogger<LoggingWebhookNotificationDispatcher> logger)
    : IWebhookNotificationDispatcher
{
    public Task SendAsync(
        WebhookNotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Webhook {DeliveryId} para {Url} (usuário {UserId}): {Title} — {Message}",
            message.DeliveryId, message.Url, message.UserId, message.Title, message.Message);

        return Task.CompletedTask;
    }
}

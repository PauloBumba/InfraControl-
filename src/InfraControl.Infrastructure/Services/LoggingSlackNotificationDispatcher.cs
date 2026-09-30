using InfraControl.Application.Notifications.Slack;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Adaptador de log do canal Slack (F9-T010) — espelho do
/// <see cref="LoggingTelegramNotificationDispatcher"/>: nenhuma chamada
/// real à API; um adaptador com token (bot Slack) troca esta
/// implementação sem tocar em Application (credencial real = pendência,
/// junto do SMTP e do bot token do Telegram).
/// </summary>
public sealed class LoggingSlackNotificationDispatcher(
    ILogger<LoggingSlackNotificationDispatcher> logger)
    : ISlackNotificationDispatcher
{
    public Task SendAsync(
        SlackNotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Mensagem Slack {DeliveryId} para o canal {Channel} (usuário {UserId}): {Title} — {Message}",
            message.DeliveryId, message.Channel, message.UserId, message.Title, message.Message);

        return Task.CompletedTask;
    }
}

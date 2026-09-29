using InfraControl.Application.Notifications.Telegram;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Adaptador de log do canal Telegram (F9-T006) — espelho do
/// <see cref="LoggingEmailNotificationDispatcher"/>: nenhuma chamada real à
/// API do Telegram; um adaptador com bot token troca esta implementação
/// sem tocar em Application (credencial real = pendência, junto do SMTP).
/// </summary>
public sealed class LoggingTelegramNotificationDispatcher(
    ILogger<LoggingTelegramNotificationDispatcher> logger)
    : ITelegramNotificationDispatcher
{
    public Task SendAsync(
        TelegramNotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Mensagem Telegram {DeliveryId} para o chat {ChatId} (usuário {UserId}): {Title} — {Message}",
            message.DeliveryId, message.ChatId, message.UserId, message.Title, message.Message);

        return Task.CompletedTask;
    }
}

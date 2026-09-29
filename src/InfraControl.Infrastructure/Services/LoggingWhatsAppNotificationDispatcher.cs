using InfraControl.Application.Notifications.WhatsApp;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Adaptador de log do canal WhatsApp (F9-T007) — espelho do
/// <see cref="LoggingTelegramNotificationDispatcher"/>: nenhuma chamada
/// real à API; um adaptador com credencial (Cloud API/Business) troca esta
/// implementação sem tocar em Application (credencial real = pendência,
/// junto do SMTP e do bot token do Telegram).
/// </summary>
public sealed class LoggingWhatsAppNotificationDispatcher(
    ILogger<LoggingWhatsAppNotificationDispatcher> logger)
    : IWhatsAppNotificationDispatcher
{
    public Task SendAsync(
        WhatsAppNotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Mensagem WhatsApp {DeliveryId} para o número {Phone} (usuário {UserId}): {Title} — {Message}",
            message.DeliveryId, message.Phone, message.UserId, message.Title, message.Message);

        return Task.CompletedTask;
    }
}

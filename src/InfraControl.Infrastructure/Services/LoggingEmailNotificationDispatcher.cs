using InfraControl.Application.Notifications.Email;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Adaptador de log do canal Email (F9-T005) — espelho do
/// `LoggingIdentityEmailSender`: nenhum SMTP real; um provider (SMTP/API)
/// troca esta implementação sem tocar em Application.
/// </summary>
public sealed class LoggingEmailNotificationDispatcher(
    ILogger<LoggingEmailNotificationDispatcher> logger)
    : IEmailNotificationDispatcher
{
    public Task SendAsync(
        EmailNotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Email de notificação {DeliveryId} para {To} (usuário {UserId}): {Subject} — {Body}",
            message.DeliveryId, message.To, message.UserId, message.Subject, message.Body);

        return Task.CompletedTask;
    }
}

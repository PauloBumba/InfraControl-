namespace InfraControl.Application.Notifications.Email;

/// <summary>
/// Mensagem do canal Email (F9-T005) — porta sem dependência de SMTP;
/// o adaptador concreto decide como enviar (logging p/ dev, provider em prod).
/// </summary>
public sealed record EmailNotificationMessage(
    Guid DeliveryId,
    Guid NotificationId,
    Guid UserId,
    string To,
    string Subject,
    string Body,
    DateTime CreatedAt);

public interface IEmailNotificationDispatcher
{
    Task SendAsync(
        EmailNotificationMessage message,
        CancellationToken cancellationToken = default);
}

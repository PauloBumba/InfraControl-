namespace InfraControl.Application.Notifications.Webhook;

/// <summary>
/// Mensagem do canal Webhook (F9-T008) — porta sem dependência de HTTP;
/// o adaptador concreto decide como enviar (logging p/ dev, POST real em
/// prod). <c>Url</c> vem da config global (Notifications:Channels:Webhook)
/// — espelho do WhatsApp/Telegram: destino por canal, não por usuário.
/// </summary>
public sealed record WebhookNotificationMessage(
    Guid DeliveryId,
    Guid NotificationId,
    Guid UserId,
    string Url,
    string Title,
    string Message,
    DateTime CreatedAt);

public interface IWebhookNotificationDispatcher
{
    Task SendAsync(
        WebhookNotificationMessage message,
        CancellationToken cancellationToken = default);
}

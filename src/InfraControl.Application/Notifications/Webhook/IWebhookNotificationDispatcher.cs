namespace InfraControl.Application.Notifications.Webhook;

/// <summary>
/// Mensagem do canal Webhook (F9-T008) — porta sem dependência de HTTP;
/// o adaptador concreto decide como enviar. F19-T007/ADR-028: o único
/// implementador é o HttpWebhookNotificationDispatcher (POST real com
/// resiliência Polly) — adaptador de log foi removido. <c>Url</c> vem da
/// config global (Notifications:Channels:Webhook) — espelho do
/// WhatsApp/Telegram: destino por canal, não por usuário.
/// Fase 05: <c>EventType</c> (default "Unknown" p/ compat) vai no payload
/// para o n8n ramificar no switch sem parsear o título/mensagem.
/// </summary>
public sealed record WebhookNotificationMessage(
    Guid DeliveryId,
    Guid NotificationId,
    Guid UserId,
    string Url,
    string Title,
    string Message,
    DateTime CreatedAt,
    string EventType = "Unknown");

public interface IWebhookNotificationDispatcher
{
    Task SendAsync(
        WebhookNotificationMessage message,
        CancellationToken cancellationToken = default);
}

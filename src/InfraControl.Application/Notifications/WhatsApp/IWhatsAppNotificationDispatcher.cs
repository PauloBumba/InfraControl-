namespace InfraControl.Application.Notifications.WhatsApp;

/// <summary>
/// Mensagem do canal WhatsApp (F9-T007) — porta sem dependência de SDK;
/// o adaptador concreto decide como enviar (logging p/ dev, provider em prod).
/// <c>Phone</c> vem da config global (Notifications:Channels:WhatsApp) —
/// espelho do Telegram (F9-T006): destino por canal, não por usuário.
/// </summary>
public sealed record WhatsAppNotificationMessage(
    Guid DeliveryId,
    Guid NotificationId,
    Guid UserId,
    string Phone,
    string Title,
    string Message,
    DateTime CreatedAt,
    IncidentChartData? Chart = null);

public interface IWhatsAppNotificationDispatcher
{
    Task SendAsync(
        WhatsAppNotificationMessage message,
        CancellationToken cancellationToken = default);
}

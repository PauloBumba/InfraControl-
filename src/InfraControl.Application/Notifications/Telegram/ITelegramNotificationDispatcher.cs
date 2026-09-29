namespace InfraControl.Application.Notifications.Telegram;

/// <summary>
/// Mensagem do canal Telegram (F9-T006) — porta sem dependência de SDK;
/// o adaptador concreto decide como enviar (logging p/ dev, API real em prod).
/// <c>ChatId</c> vem da config global (Notifications:Channels:Telegram) —
/// decisão do usuário: destino por canal, não por usuário.
/// </summary>
public sealed record TelegramNotificationMessage(
    Guid DeliveryId,
    Guid NotificationId,
    Guid UserId,
    string ChatId,
    string Title,
    string Message,
    DateTime CreatedAt);

public interface ITelegramNotificationDispatcher
{
    Task SendAsync(
        TelegramNotificationMessage message,
        CancellationToken cancellationToken = default);
}

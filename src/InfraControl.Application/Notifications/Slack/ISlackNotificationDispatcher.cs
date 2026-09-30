namespace InfraControl.Application.Notifications.Slack;

/// <summary>
/// Mensagem do canal Slack (F9-T010) — porta sem dependência de SDK;
/// o adaptador concreto decide como enviar (logging p/ dev, bot API em
/// prod). <c>Channel</c> vem da config global
/// (Notifications:Channels:Slack) — espelho do Telegram: destino por
/// canal, não por usuário.
/// </summary>
public sealed record SlackNotificationMessage(
    Guid DeliveryId,
    Guid NotificationId,
    Guid UserId,
    string Channel,
    string Title,
    string Message,
    DateTime CreatedAt);

public interface ISlackNotificationDispatcher
{
    Task SendAsync(
        SlackNotificationMessage message,
        CancellationToken cancellationToken = default);
}

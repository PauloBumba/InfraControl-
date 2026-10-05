namespace InfraControl.Application.Notifications.SignalR;

/// <summary>
/// Payload enviado ao canal SignalR (F9-T004) — título/mensagem da
/// notificação já resolvidos pelo worker. <c>EventType</c> espelha o feed
/// persistido (entrega 3 do backlog de UX): o frontend usa o mesmo campo
/// para exibir o tipo junto de título/mensagem no evento em tempo real.
/// </summary>
public sealed record NotificationSignalRMessage(
    Guid DeliveryId,
    Guid NotificationId,
    Guid UserId,
    string Title,
    string Message,
    string EventType,
    DateTime CreatedAt);

/// <summary>
/// Porta de push do canal SignalR (F9-T004) — implementada no Api com
/// IHubContext; o worker de deliveries a consome. Interface sem
/// dependência de SignalR (regra BuildingBlocks/ports).
/// </summary>
public interface ISignalRNotificationDispatcher
{
    Task SendAsync(
        NotificationSignalRMessage message,
        CancellationToken cancellationToken = default);
}

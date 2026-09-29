using InfraControl.Api.Hubs;
using InfraControl.Application.Notifications.SignalR;
using Microsoft.AspNetCore.SignalR;

namespace InfraControl.Api.Services;

/// <summary>
/// Push do canal SignalR (F9-T004): entrega ao grupo
/// <c>user:{userId}</c> do Hub. Singleton (IHubContext é singleton).
/// </summary>
public sealed class SignalRNotificationDispatcher(
    IHubContext<NotificationsHub> hubContext,
    ILogger<SignalRNotificationDispatcher> logger)
    : ISignalRNotificationDispatcher
{
    public async Task SendAsync(
        NotificationSignalRMessage message,
        CancellationToken cancellationToken = default)
    {
        await hubContext.Clients
            .Group(NotificationsHub.UserGroup(message.UserId))
            .SendAsync("notification", new
            {
                deliveryId = message.DeliveryId,
                notificationId = message.NotificationId,
                title = message.Title,
                message = message.Message,
                createdAt = message.CreatedAt
            }, cancellationToken);

        logger.LogInformation(
            "Notificação {NotificationId} enviada via SignalR ao usuário {UserId} (delivery {DeliveryId}).",
            message.NotificationId, message.UserId, message.DeliveryId);
    }
}

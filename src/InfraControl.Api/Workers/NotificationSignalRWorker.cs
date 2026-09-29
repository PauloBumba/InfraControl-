using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Application.Notifications.SignalR;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Api.Workers;

/// <summary>
/// Notification Worker do canal SignalR (F9-T004, docs/07): push para o
/// grupo `user:{userId}` via `ISignalRNotificationDispatcher`.
/// </summary>
public sealed class NotificationSignalRWorker(
    IServiceScopeFactory scopeFactory,
    ISignalRNotificationDispatcher dispatcher,
    ILogger<NotificationSignalRWorker> logger)
    : NotificationChannelWorkerBase(scopeFactory, logger, NotificationChannel.SignalR)
{
    protected override Task DispatchAsync(
        PendingDeliveryItem item,
        NotificationDelivery delivery,
        CancellationToken cancellationToken)
        => dispatcher.SendAsync(
            new NotificationSignalRMessage(
                item.DeliveryId, item.NotificationId, item.UserId,
                item.Title, item.Message, item.CreatedAt),
            cancellationToken);
}

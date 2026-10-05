using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Application.Notifications.SignalR;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.Api.Workers;

/// <summary>
/// Notification Worker do canal SignalR (F9-T004, docs/07): push para o
/// grupo `user:{userId}` via `ISignalRNotificationDispatcher`.
/// </summary>
public sealed class NotificationSignalRWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationSignalRWorker> logger)
    : NotificationChannelWorkerBase(scopeFactory, logger, NotificationChannel.SignalR)
{
    protected override Task DispatchAsync(
        PendingDeliveryItem item,
        NotificationDelivery delivery,
        IServiceProvider services,
        CancellationToken cancellationToken)
        => services.GetRequiredService<ISignalRNotificationDispatcher>().SendAsync(
            new NotificationSignalRMessage(
                item.DeliveryId, item.NotificationId, item.UserId,
                item.Title, item.Message, item.EventType, item.CreatedAt),
            cancellationToken);
}

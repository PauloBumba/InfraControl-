using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Application.Notifications.Email;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Api.Workers;

/// <summary>
/// Notification Worker do canal Email (F9-T005, docs/07): despacha via
/// `IEmailNotificationDispatcher` para o e-mail do destinatário.
/// </summary>
public sealed class NotificationEmailWorker(
    IServiceScopeFactory scopeFactory,
    IEmailNotificationDispatcher dispatcher,
    ILogger<NotificationEmailWorker> logger)
    : NotificationChannelWorkerBase(scopeFactory, logger, NotificationChannel.Email)
{
    protected override Task DispatchAsync(
        PendingDeliveryItem item,
        NotificationDelivery delivery,
        CancellationToken cancellationToken)
        => dispatcher.SendAsync(
            new EmailNotificationMessage(
                item.DeliveryId, item.NotificationId, item.UserId,
                item.RecipientEmail ?? string.Empty,
                item.Title, item.Message, item.CreatedAt),
            cancellationToken);
}

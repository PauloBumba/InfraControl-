using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Application.Notifications.Webhook;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InfraControl.Api.Workers;

/// <summary>
/// Notification Worker do canal Webhook (F9-T008, docs/07): despacha via
/// `IWebhookNotificationDispatcher` para a Url da config global
/// (Notifications:Channels:Webhook) — espelho do WhatsApp (F9-T007).
/// Url esvaziada entre enfileirar e despachar vira Failed com erro
/// pt-BR apontando a seção.
/// </summary>
public sealed class NotificationWebhookWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationChannelsOptions> channelsOptions,
    ILogger<NotificationWebhookWorker> logger)
    : NotificationChannelWorkerBase(scopeFactory, logger, NotificationChannel.Webhook)
{
    protected override Task DispatchAsync(
        PendingDeliveryItem item,
        NotificationDelivery delivery,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var url = channelsOptions.Value.Webhook.Url;
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException(
                "Canal Webhook sem Url configurada (Notifications:Channels:Webhook:Url).");

        return services.GetRequiredService<IWebhookNotificationDispatcher>().SendAsync(
            new WebhookNotificationMessage(
                item.DeliveryId, item.NotificationId, item.UserId,
                url, item.Title, item.Message, item.CreatedAt,
                EventType: item.EventType),
            cancellationToken);
    }
}

using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Application.Notifications.WhatsApp;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InfraControl.Api.Workers;

/// <summary>
/// Notification Worker do canal WhatsApp (F9-T007, docs/07): despacha via
/// `IWhatsAppNotificationDispatcher` para o Phone da config global
/// (Notifications:Channels:WhatsApp) — espelho do Telegram (F9-T006).
/// Phone esvaziado entre enfileirar e despachar vira Failed com erro
/// pt-BR apontando a seção.
/// </summary>
public sealed class NotificationWhatsAppWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationChannelsOptions> channelsOptions,
    ILogger<NotificationWhatsAppWorker> logger)
    : NotificationChannelWorkerBase(scopeFactory, logger, NotificationChannel.WhatsApp)
{
    protected override Task DispatchAsync(
        PendingDeliveryItem item,
        NotificationDelivery delivery,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var phone = channelsOptions.Value.WhatsApp.Phone;
        if (string.IsNullOrWhiteSpace(phone))
            throw new InvalidOperationException(
                "Canal WhatsApp sem Phone configurado (Notifications:Channels:WhatsApp:Phone).");

        return services.GetRequiredService<IWhatsAppNotificationDispatcher>().SendAsync(
            new WhatsAppNotificationMessage(
                item.DeliveryId, item.NotificationId, item.UserId,
                phone, item.Title, item.Message, item.CreatedAt),
            cancellationToken);
    }
}

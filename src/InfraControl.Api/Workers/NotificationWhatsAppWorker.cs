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
/// `IWhatsAppNotificationDispatcher` — ADR-059 §3 (F33-T003): destino é o
/// `GroupId` (broadcast no grupo) com fallback para o `Phone` individual
/// (Notifications:Channels:WhatsApp). Nenhum dos dois = Failed com erro
/// pt-BR apontando a seção (mesmo contrato do espelho Telegram).
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
        var whatsapp = channelsOptions.Value.WhatsApp;
        var destination = !string.IsNullOrWhiteSpace(whatsapp.GroupId)
            ? whatsapp.GroupId
            : whatsapp.Phone;
        if (string.IsNullOrWhiteSpace(destination))
            throw new InvalidOperationException(
                "Canal WhatsApp sem GroupId nem Phone configurado (Notifications:Channels:WhatsApp).");

        return services.GetRequiredService<IWhatsAppNotificationDispatcher>().SendAsync(
            new WhatsAppNotificationMessage(
                item.DeliveryId, item.NotificationId, item.UserId,
                destination, item.Title, item.Message, item.CreatedAt),
            cancellationToken);
    }
}

using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Application.Notifications.Telegram;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InfraControl.Api.Workers;

/// <summary>
/// Notification Worker do canal Telegram (F9-T006, docs/07): despacha via
/// `ITelegramNotificationDispatcher` para o ChatId da config global
/// (Notifications:Channels:Telegram) — destino por canal, decidido pelo
/// admin, não por usuário. ChatId esvaziado entre enfileirar e despachar
/// vira Failed com erro pt-BR apontando a seção.
/// </summary>
public sealed class NotificationTelegramWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationChannelsOptions> channelsOptions,
    ILogger<NotificationTelegramWorker> logger)
    : NotificationChannelWorkerBase(scopeFactory, logger, NotificationChannel.Telegram)
{
    protected override Task DispatchAsync(
        PendingDeliveryItem item,
        NotificationDelivery delivery,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var chatId = channelsOptions.Value.Telegram.ChatId;
        if (string.IsNullOrWhiteSpace(chatId))
            throw new InvalidOperationException(
                "Canal Telegram sem ChatId configurado (Notifications:Channels:Telegram:ChatId).");

        return services.GetRequiredService<ITelegramNotificationDispatcher>().SendAsync(
            new TelegramNotificationMessage(
                item.DeliveryId, item.NotificationId, item.UserId,
                chatId, item.Title, item.Message, item.CreatedAt),
            cancellationToken);
    }
}

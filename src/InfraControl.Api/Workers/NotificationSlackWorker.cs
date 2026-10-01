using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Application.Notifications.Slack;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InfraControl.Api.Workers;

/// <summary>
/// Notification Worker do canal Slack (F9-T010, docs/07): despacha via
/// `ISlackNotificationDispatcher` para o Channel da config global
/// (Notifications:Channels:Slack) — espelho do Telegram (F9-T006).
/// Channel esvaziado entre enfileirar e despachar vira Failed com erro
/// pt-BR apontando a seção.
/// </summary>
public sealed class NotificationSlackWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationChannelsOptions> channelsOptions,
    ILogger<NotificationSlackWorker> logger)
    : NotificationChannelWorkerBase(scopeFactory, logger, NotificationChannel.Slack)
{
    protected override Task DispatchAsync(
        PendingDeliveryItem item,
        NotificationDelivery delivery,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var channel = channelsOptions.Value.Slack.Channel;
        if (string.IsNullOrWhiteSpace(channel))
            throw new InvalidOperationException(
                "Canal Slack sem Channel configurado (Notifications:Channels:Slack:Channel).");

        return services.GetRequiredService<ISlackNotificationDispatcher>().SendAsync(
            new SlackNotificationMessage(
                item.DeliveryId, item.NotificationId, item.UserId,
                channel, item.Title, item.Message, item.CreatedAt),
            cancellationToken);
    }
}

using InfraControl.Domain.Enums;

namespace InfraControl.Application.Notifications.Stats.Ports;

/// <summary>Linha crua de delivery para agregação em stats (sem entidade).</summary>
public sealed record NotificationDeliveryStatRow(
    DateTime CreateDateTimeUtc,
    NotificationChannel Channel,
    DeliveryStatus Status);

/// <summary>Porta de leitura das entregas de notificação para gráficos.</summary>
public interface INotificationDeliveryStatsStore
{
    Task<IReadOnlyList<NotificationDeliveryStatRow>> ListSinceAsync(
        DateTime sinceUtc,
        CancellationToken cancellationToken = default);
}

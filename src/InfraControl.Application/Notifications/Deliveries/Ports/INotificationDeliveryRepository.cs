using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Notifications.Deliveries.Ports;

/// <summary>
/// Delivery pendente já com os dados da notificação (F9-T004).
/// </summary>
public sealed record PendingDeliveryItem(
    Guid DeliveryId,
    Guid NotificationId,
    Guid UserId,
    string Title,
    string Message,
    string EventType,
    DateTime CreatedAt,
    string? RecipientEmail,
    Guid? DeviceId = null);

public interface INotificationDeliveryRepository : IRepository<NotificationDelivery>
{
    /// <summary>
    /// Pendentes do canal ordenadas por criação (limite 1–200) — fila de
    /// trabalho do worker do canal (F9-T004; retry em F9-T009).
    /// </summary>
    Task<IReadOnlyCollection<PendingDeliveryItem>> ListPendingByChannelAsync(
        NotificationChannel channel,
        int limit,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

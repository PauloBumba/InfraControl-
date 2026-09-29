using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class NotificationDeliveryRepository(AppDbContext dbContext)
    : INotificationDeliveryRepository
{
    public async Task AddAsync(
        NotificationDelivery delivery, CancellationToken cancellationToken = default)
    {
        await dbContext.NotificationDeliveries.AddAsync(delivery, cancellationToken);
    }

    public Task<NotificationDelivery?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.NotificationDeliveries
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public void Remove(NotificationDelivery delivery)
        => dbContext.NotificationDeliveries.Remove(delivery);

    public async Task<IReadOnlyCollection<PendingDeliveryItem>> ListPendingByChannelAsync(
        NotificationChannel channel,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 200);

        return await dbContext.NotificationDeliveries
            .AsNoTracking()
            .Where(d => d.Channel == channel && d.Status == DeliveryStatus.Pending)
            .OrderBy(d => d.CreateDateTime)
            .Take(take)
            .Join(
                dbContext.Notifications.AsNoTracking(),
                delivery => delivery.NotificationId,
                notification => notification.Id,
                (delivery, notification) => new PendingDeliveryItem(
                    delivery.Id,
                    delivery.NotificationId,
                    delivery.UserId,
                    notification.Title,
                    notification.Message,
                    notification.CreateDateTime))
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

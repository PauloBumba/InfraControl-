using InfraControl.Application.Notifications.Stats.Ports;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// Leituras de notification_deliveries para os gráficos das telas de
/// integração — projeção mínima (3 colunas) na janela pedida.
/// </summary>
public sealed class NotificationDeliveryStatsStore(AppDbContext dbContext)
    : INotificationDeliveryStatsStore
{
    public async Task<IReadOnlyList<NotificationDeliveryStatRow>> ListSinceAsync(
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
        => await dbContext.NotificationDeliveries.AsNoTracking()
            .Where(delivery => delivery.CreateDateTime >= sinceUtc)
            .Select(delivery => new NotificationDeliveryStatRow(
                delivery.CreateDateTime, delivery.Channel, delivery.Status))
            .ToListAsync(cancellationToken);
}

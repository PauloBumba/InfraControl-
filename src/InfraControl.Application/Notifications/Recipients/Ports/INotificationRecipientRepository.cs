using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Notifications.Recipients.Ports;

public interface INotificationRecipientRepository : IRepository<NotificationRecipient>
{
    /// <summary>Destinatários da notificação; 404 se a notificação não existe.</summary>
    Task<PagedResult<NotificationRecipient>> SearchByNotificationIdAsync(
        Guid notificationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Usuários finais únicos da notificação (F9-T002, docs/07): destinatários
    /// diretos + roles resolvidas para usuários ativos, deduplicados —
    /// usuário em múltiplas roles recebe uma única notificação lógica.
    /// 404 se a notificação não existe.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> ResolveUserIdsAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

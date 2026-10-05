using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Notifications.Feed.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F18-T001 — feed do usuário sobre `Notifications` ⋈
/// `NotificationRecipients`: o predicado correlato com `Any()` dá o dedup
/// por construção (destinatário direto E de role na mesma notificação =
/// uma linha só). `roles.Contains` traduz para `IN` no Npgsql.
/// </summary>
public sealed class NotificationFeedRepository(AppDbContext dbContext)
    : INotificationFeedRepository
{
    public Task<Notification?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.Notifications
            .AsNoTracking()
            .SingleOrDefaultAsync(notification => notification.Id == id, cancellationToken);

    public async Task AddAsync(
        Notification entity, CancellationToken cancellationToken = default)
        => await dbContext.Notifications.AddAsync(entity, cancellationToken);

    public void Remove(Notification entity)
        => dbContext.Notifications.Remove(entity);

    public Task<Notification?> GetForUserAsync(
        Guid id,
        Guid userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var roleList = roles.ToArray();

        return dbContext.Notifications
            .AsNoTracking()
            .SingleOrDefaultAsync(notification =>
                notification.Id == id &&
                dbContext.NotificationRecipients.Any(recipient =>
                    recipient.NotificationId == notification.Id &&
                    (recipient.UserId == userId ||
                     (recipient.Role != null && roleList.Contains(recipient.Role)))),
                cancellationToken);
    }

    public async Task<PagedResult<Notification>> SearchForUserAsync(
        Guid userId,
        IReadOnlyCollection<string> roles,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var roleList = roles.ToArray();

        var query = dbContext.Notifications
            .AsNoTracking()
            .Where(notification => dbContext.NotificationRecipients.Any(recipient =>
                recipient.NotificationId == notification.Id &&
                (recipient.UserId == userId ||
                 (recipient.Role != null && roleList.Contains(recipient.Role)))));

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(notification => notification.CreateDateTime)
            // Desempate estável: paginação não pula nem repete itens com
            // o mesmo timestamp.
            .ThenByDescending(notification => notification.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Notification>(items, totalCount, page, pageSize);
    }
}

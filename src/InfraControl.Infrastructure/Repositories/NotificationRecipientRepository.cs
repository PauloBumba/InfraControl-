using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Notifications.Recipients.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class NotificationRecipientRepository(AppDbContext dbContext)
    : INotificationRecipientRepository
{
    public async Task AddAsync(
        NotificationRecipient recipient, CancellationToken cancellationToken = default)
    {
        await dbContext.NotificationRecipients.AddAsync(recipient, cancellationToken);
    }

    public Task<NotificationRecipient?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.NotificationRecipients
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public void Remove(NotificationRecipient recipient)
        => dbContext.NotificationRecipients.Remove(recipient);

    public async Task<PagedResult<NotificationRecipient>> SearchByNotificationIdAsync(
        Guid notificationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Notifications.AsNoTracking()
                .AnyAsync(n => n.Id == notificationId, cancellationToken))
            throw new KeyNotFoundException(
                $"Notificação '{notificationId}' não encontrada.");

        var query = dbContext.NotificationRecipients
            .AsNoTracking()
            .Where(r => r.NotificationId == notificationId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(r => r.Role)
            .ThenBy(r => r.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationRecipient>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyCollection<Guid>> ResolveUserIdsAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Notifications.AsNoTracking()
                .AnyAsync(n => n.Id == notificationId, cancellationToken))
            throw new KeyNotFoundException(
                $"Notificação '{notificationId}' não encontrada.");

        var recipients = await dbContext.NotificationRecipients
            .AsNoTracking()
            .Where(r => r.NotificationId == notificationId)
            .ToListAsync(cancellationToken);

        // Usuários finais únicos: destinatários diretos + roles
        // resolvidas para usuários ativos (dedup — docs/07).
        var userIds = new HashSet<Guid>();

        foreach (var direct in recipients.Where(r => r.UserId is not null))
            userIds.Add(direct.UserId!.Value);

        var roles = recipients
            .Where(r => r.Role is not null)
            .Select(r => Enum.Parse<UserRole>(r.Role!, ignoreCase: true))
            .ToHashSet();

        if (roles.Count > 0)
        {
            var roleUserIds = await dbContext.DomainUsers
                .AsNoTracking()
                .Where(user => user.IsActive
                    && user.RoleAssignments.Any(
                        assignment => roles.Contains(assignment.Role)))
                .Select(user => user.Id)
                .ToListAsync(cancellationToken);

            foreach (var id in roleUserIds)
                userIds.Add(id);
        }

        return userIds.Order().ToArray();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

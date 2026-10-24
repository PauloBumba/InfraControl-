using InfraControl.Domain.Entities;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F42-T001: regras por tipo de evento (uma por EventType, upsert).
/// </summary>
public sealed class NotificationEventRuleRepository(Persistence.AppDbContext dbContext)
    : Application.Notifications.Rules.Ports.INotificationEventRuleRepository
{
    public async Task<IReadOnlyList<NotificationEventRule>> ListAsync(
        CancellationToken cancellationToken)
        => await dbContext.NotificationEventRules
            .OrderBy(r => r.EventType)
            .ToListAsync(cancellationToken);

    public async Task<NotificationEventRule?> GetByEventTypeAsync(
        string eventType, CancellationToken cancellationToken)
        => await dbContext.NotificationEventRules
            .FirstOrDefaultAsync(r => r.EventType == eventType, cancellationToken);

    public async Task SaveAsync(
        NotificationEventRule rule, CancellationToken cancellationToken)
    {
        var existing = await dbContext.NotificationEventRules
            .FirstOrDefaultAsync(r => r.EventType == rule.EventType, cancellationToken);

        if (existing is null)
            await dbContext.NotificationEventRules.AddAsync(rule, cancellationToken);
        else if (!ReferenceEquals(existing, rule))
        {
            existing.Update(rule.EventType, rule.Enabled, rule.Channels);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

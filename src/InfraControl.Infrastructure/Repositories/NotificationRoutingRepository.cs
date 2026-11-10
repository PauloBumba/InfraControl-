using InfraControl.Application.Notifications.Routing.Ports;
using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F39-T003: política singleton + sincronização de regras (add/update/remove
/// por Id) numa única transação de SaveChanges.
/// </summary>
public sealed class NotificationRoutingRepository(Persistence.AppDbContext dbContext)
    : INotificationRoutingRepository
{
    public async Task<NotificationRoutingState> GetStateAsync(
        CancellationToken cancellationToken)
    {
        var policy = await dbContext.NotificationRoutingPolicies
            .FirstOrDefaultAsync(cancellationToken);

        var rules = await dbContext.NotificationRoutingRules
            .OrderBy(r => r.EventType)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return new NotificationRoutingState(policy, rules);
    }

    public async Task SaveStateAsync(
        NotificationRoutingPolicy policy,
        IReadOnlyList<NotificationRoutingRule> rules,
        CancellationToken cancellationToken)
    {
        var existingPolicy = await dbContext.NotificationRoutingPolicies
            .FirstOrDefaultAsync(cancellationToken);

        if (existingPolicy is null)
            await dbContext.NotificationRoutingPolicies.AddAsync(policy, cancellationToken);
        else if (!ReferenceEquals(existingPolicy, policy))
            existingPolicy.Update(policy.Enabled, policy.Defaults);

        var existingRules = await dbContext.NotificationRoutingRules
            .ToListAsync(cancellationToken);

        var incomingIds = rules.Select(r => r.Id).ToHashSet();
        foreach (var stale in existingRules.Where(r => !incomingIds.Contains(r.Id)))
            dbContext.NotificationRoutingRules.Remove(stale);

        foreach (var rule in rules)
        {
            if (existingRules.Any(r => r.Id == rule.Id))
                dbContext.NotificationRoutingRules.Update(rule);
            else
                await dbContext.NotificationRoutingRules.AddAsync(rule, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

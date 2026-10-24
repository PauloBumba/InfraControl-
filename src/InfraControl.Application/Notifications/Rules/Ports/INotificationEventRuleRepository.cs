using InfraControl.Domain.Entities;

namespace InfraControl.Application.Notifications.Rules.Ports;

/// <summary>
/// F42-T001: persistência das regras por tipo de evento. Uma por
/// EventType; sem linha = comportamento atual (fanout global).
/// </summary>
public interface INotificationEventRuleRepository
{
    Task<IReadOnlyList<NotificationEventRule>> ListAsync(
        CancellationToken cancellationToken);

    Task<NotificationEventRule?> GetByEventTypeAsync(
        string eventType, CancellationToken cancellationToken);

    Task SaveAsync(
        NotificationEventRule rule, CancellationToken cancellationToken);
}

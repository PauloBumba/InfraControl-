using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Notifications.Rules.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Infrastructure.Notifications;

/// <summary>
/// F48-T004: seed idempotente das regras dos eventos novos de conclusão
/// (Fase 48) — ON com canais Email,SignalR por padrão. Linha existente é
/// autoridade do operador (admin pode desligar na UI sem o seeder
/// reescrever no boot).
/// </summary>
public sealed class NotificationEventRuleSeeder(
    INotificationEventRuleRepository repository,
    IUnitOfWork unitOfWork)
{
    private static readonly (string EventType, string Channels)[] DefaultRules =
    [
        (nameof(TaskResolvedDomainEvent), "Email,SignalR"),
        (nameof(ProjectStatusChangedDomainEvent), "Email,SignalR"),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (eventType, channels) in DefaultRules)
        {
            var existing = await repository.GetByEventTypeAsync(eventType, cancellationToken);
            if (existing is not null)
                continue;

            await repository.SaveAsync(
                NotificationEventRule.Create(eventType, enabled: true, channels),
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

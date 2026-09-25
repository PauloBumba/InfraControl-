using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Common.Events;
using MediatR;

namespace InfraControl.Application.Common.Events;

/// <summary>
/// ADR-005 (docs/06): todo domain event publicado pelo AppDbContext vira
/// OutboxMessage na mesma transação dos dados de negócio — nunca
/// "DB commit → RabbitMQ" sem garantia. O OutboxDispatcherWorker
/// publica depois no RabbitMQ.
/// </summary>
public sealed class OutboxDispatchHandler<TEvent>(IDomainEventService domainEventService)
    : INotificationHandler<TEvent>
    where TEvent : IDomainEvent
{
    public Task Handle(TEvent notification, CancellationToken cancellationToken)
        => domainEventService.PublishAsync(notification, cancellationToken);
}

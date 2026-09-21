using MediatR;

namespace InfraControl.Domain.Common.Events;

public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    string EventType { get; }
    int Version { get; }
}

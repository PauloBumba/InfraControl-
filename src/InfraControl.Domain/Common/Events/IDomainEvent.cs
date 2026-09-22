using MediatR;

namespace InfraControl.Domain.Common.Events;

public interface IDomainEvent : INotification, InfraControl.BuildingBlocks.Domain.IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    string EventType { get; }
    int Version { get; }
}

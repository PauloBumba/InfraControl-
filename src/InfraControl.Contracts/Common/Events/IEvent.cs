namespace InfraControl.Contracts.Common.Events;

public interface IEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}

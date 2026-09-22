namespace InfraControl.BuildingBlocks.Domain;

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
    string EventType { get; }
}

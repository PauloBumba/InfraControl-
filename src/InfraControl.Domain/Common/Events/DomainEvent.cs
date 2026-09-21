using InfraControl.Domain.Common;

namespace InfraControl.Domain.Common.Events;

public abstract class DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
    public abstract string EventType { get; }
    public virtual int Version => 1;
}

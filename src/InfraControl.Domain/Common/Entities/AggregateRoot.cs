using InfraControl.Domain.Common.Events;

namespace InfraControl.Domain.Common.Entities;

public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot() { }

    protected AggregateRoot(Guid id) : base(id) { }

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Remove pendentes de um tipo ainda não despachados (F33-T004:
    /// Alert.Suppress descarta o AlertCreatedDomainEvent — suprimido não
    /// notifica "Alerta criado").
    /// </summary>
    protected void RemoveDomainEvents<TEvent>() where TEvent : IDomainEvent
        => _domainEvents.RemoveAll(domainEvent => domainEvent is TEvent);

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

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
    /// F33-T004/ADR-059: remove um evento pendente específico — o alerta
    /// Critical que nasce Suppressed (vítima de incidente upstream) não
    /// pode notificar "Alerta Critical criado" (o grupo já recebe o
    /// incidente raiz).
    /// </summary>
    protected void RemoveDomainEvent<TEvent>() where TEvent : IDomainEvent
        => _domainEvents.RemoveAll(domainEvent => domainEvent is TEvent);

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

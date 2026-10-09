using InfraControl.Domain.Common;

namespace InfraControl.Domain.Common.Events;

public abstract class DomainEvent : IDomainEvent
{
    // `init` obrigatório: o OutboxDispatcherWorker desserializa o payload
    // (ADR-005) e o consumer usa o EventId ORIGINAL para idempotência
    // (docs/06) — com `{ get; }` puro o STJ descartava o valor do JSON e
    // gerava um GUID novo no round-trip (OutboxEventContractTests).
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
    public abstract string EventType { get; }
    public virtual int Version => 1;
}

using InfraControl.Domain.Common.Events;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Events;

/// <summary>
/// PDI (Plano de Desenvolvimento Individual) — eventos distribuídos no
/// pipeline do repositório: entity → AppDbContext (ADR-005: mesma
/// transação) → OutboxMessage → OutboxDispatcherWorker → RabbitMQ →
/// NotificationEventConsumer. O consumidor decide destinatário (dono vs
/// admin) e canais (criação = Email+SignalR, edição = SignalR) por evento
/// — regra do usuário 2026-10-04. Os construtores casam 1:1 com as
/// propriedades: o System.Text.Json do outbox exige binding do
/// deserialization constructor por nome.
/// </summary>
public sealed record PdiChangeActor(Guid Id, string Email, string OwnerEmail);

public sealed class PdiCreatedDomainEvent : DomainEvent
{
    public PdiCreatedDomainEvent(
        Guid userId,
        DateOnly targetDate,
        PdiStatus status,
        Guid actorId,
        string actorEmail,
        string ownerEmail)
    {
        UserId = userId;
        TargetDate = targetDate;
        Status = status;
        ActorId = actorId;
        ActorEmail = actorEmail;
        OwnerEmail = ownerEmail;
    }

    public Guid UserId { get; }
    public DateOnly TargetDate { get; }
    public PdiStatus Status { get; }
    public Guid ActorId { get; }
    public string ActorEmail { get; }
    public string OwnerEmail { get; }
    public override string EventType => nameof(PdiCreatedDomainEvent);
}

public sealed class PdiUpdatedDomainEvent : DomainEvent
{
    public PdiUpdatedDomainEvent(
        Guid userId,
        DateOnly targetDate,
        PdiStatus status,
        Guid actorId,
        string actorEmail,
        string ownerEmail)
    {
        UserId = userId;
        TargetDate = targetDate;
        Status = status;
        ActorId = actorId;
        ActorEmail = actorEmail;
        OwnerEmail = ownerEmail;
    }

    public Guid UserId { get; }
    public DateOnly TargetDate { get; }
    public PdiStatus Status { get; }
    public Guid ActorId { get; }
    public string ActorEmail { get; }
    public string OwnerEmail { get; }
    public override string EventType => nameof(PdiUpdatedDomainEvent);
}

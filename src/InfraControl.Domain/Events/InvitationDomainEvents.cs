using InfraControl.Domain.Common.Events;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Events;

public sealed class InvitationCreatedDomainEvent : DomainEvent
{
    public InvitationCreatedDomainEvent(Guid invitationId, string email)
    {
        InvitationId = invitationId;
        Email = email;
    }

    public Guid InvitationId { get; }
    public string Email { get; }
    public override string EventType => nameof(InvitationCreatedDomainEvent);
}

/// <summary>ADR-030 §5 — PUT de roles / cancelamento (CRUD update).</summary>
public sealed class InvitationUpdatedDomainEvent : DomainEvent
{
    public InvitationUpdatedDomainEvent(Guid invitationId, InvitationStatus status)
    {
        InvitationId = invitationId;
        Status = status;
    }

    public Guid InvitationId { get; }
    public InvitationStatus Status { get; }
    public override string EventType => nameof(InvitationUpdatedDomainEvent);
}

public sealed class InvitationAcceptedDomainEvent : DomainEvent
{
    public InvitationAcceptedDomainEvent(Guid invitationId, string email)
    {
        InvitationId = invitationId;
        Email = email;
    }

    public Guid InvitationId { get; }
    public string Email { get; }
    public override string EventType => nameof(InvitationAcceptedDomainEvent);
}

public sealed class InvitationRejectedDomainEvent : DomainEvent
{
    public InvitationRejectedDomainEvent(Guid invitationId, string email)
    {
        InvitationId = invitationId;
        Email = email;
    }

    public Guid InvitationId { get; }
    public string Email { get; }
    public override string EventType => nameof(InvitationRejectedDomainEvent);
}

public sealed class InvitationActivatedDomainEvent : DomainEvent
{
    public InvitationActivatedDomainEvent(Guid invitationId, string email)
    {
        InvitationId = invitationId;
        Email = email;
    }

    public Guid InvitationId { get; }
    public string Email { get; }
    public override string EventType => nameof(InvitationActivatedDomainEvent);
}

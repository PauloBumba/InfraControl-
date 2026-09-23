using InfraControl.Domain.Common.Events;

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

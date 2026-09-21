using InfraControl.Domain.Common.Events;

namespace InfraControl.Domain.Events;

public sealed class UserSessionStartedDomainEvent : DomainEvent
{
    public UserSessionStartedDomainEvent(Guid sessionId, Guid userId) { SessionId = sessionId; UserId = userId; }
    public Guid SessionId { get; }
    public Guid UserId { get; }
    public override string EventType => nameof(UserSessionStartedDomainEvent);
}

public sealed class UserSessionRefreshedDomainEvent : DomainEvent
{
    public UserSessionRefreshedDomainEvent(Guid sessionId, Guid userId) { SessionId = sessionId; UserId = userId; }
    public Guid SessionId { get; }
    public Guid UserId { get; }
    public override string EventType => nameof(UserSessionRefreshedDomainEvent);
}

public sealed class UserSessionRevokedDomainEvent : DomainEvent
{
    public UserSessionRevokedDomainEvent(Guid sessionId, Guid userId) { SessionId = sessionId; UserId = userId; }
    public Guid SessionId { get; }
    public Guid UserId { get; }
    public override string EventType => nameof(UserSessionRevokedDomainEvent);
}

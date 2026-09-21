using InfraControl.Domain.Common.Events;

namespace InfraControl.Domain.Identity.Events;

public sealed class UserCreatedDomainEvent : DomainEvent
{
    public UserCreatedDomainEvent(Guid userId, string email) { UserId = userId; Email = email; }
    public Guid UserId { get; }
    public string Email { get; }
    public override string EventType => nameof(UserCreatedDomainEvent);
}

public sealed class UserRoleAssignedDomainEvent : DomainEvent
{
    public UserRoleAssignedDomainEvent(Guid userId, UserRole role) { UserId = userId; Role = role; }
    public Guid UserId { get; }
    public UserRole Role { get; }
    public override string EventType => nameof(UserRoleAssignedDomainEvent);
}

public sealed class UserRoleRemovedDomainEvent : DomainEvent
{
    public UserRoleRemovedDomainEvent(Guid userId, UserRole role) { UserId = userId; Role = role; }
    public Guid UserId { get; }
    public UserRole Role { get; }
    public override string EventType => nameof(UserRoleRemovedDomainEvent);
}

public sealed class UserDisabledDomainEvent : DomainEvent
{
    public UserDisabledDomainEvent(Guid userId) { UserId = userId; }
    public Guid UserId { get; }
    public override string EventType => nameof(UserDisabledDomainEvent);
}

public sealed class UserEnabledDomainEvent : DomainEvent
{
    public UserEnabledDomainEvent(Guid userId) { UserId = userId; }
    public Guid UserId { get; }
    public override string EventType => nameof(UserEnabledDomainEvent);
}

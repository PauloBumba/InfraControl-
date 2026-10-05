using InfraControl.Domain.Enums;
using InfraControl.Domain.Common.Events;

namespace InfraControl.Domain.Events;

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

public sealed class UserUpdatedDomainEvent : DomainEvent
{
    public UserUpdatedDomainEvent(Guid userId, string displayName, string email)
    {
        UserId = userId;
        DisplayName = displayName;
        Email = email;
    }
    public Guid UserId { get; }
    public string DisplayName { get; }
    public string Email { get; }
    public override string EventType => nameof(UserUpdatedDomainEvent);
}

public sealed class UserDeletedDomainEvent : DomainEvent
{
    public UserDeletedDomainEvent(Guid userId, string email, string displayName)
    {
        UserId = userId;
        Email = email;
        DisplayName = displayName;
    }
    public Guid UserId { get; }
    public string Email { get; }
    public string DisplayName { get; }
    public override string EventType => nameof(UserDeletedDomainEvent);
}

public sealed class UserPasswordChangedDomainEvent : DomainEvent
{
    public UserPasswordChangedDomainEvent(Guid userId, string email)
    {
        UserId = userId;
        Email = email;
    }
    public Guid UserId { get; }
    public string Email { get; }
    public override string EventType => nameof(UserPasswordChangedDomainEvent);
}

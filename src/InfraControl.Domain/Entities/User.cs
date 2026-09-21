using InfraControl.Domain.Enums;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

public sealed class User : AggregateRoot
{
    private readonly List<UserRoleAssignment> _roleAssignments = [];

    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<UserRole> Roles => _roleAssignments.Select(assignment => assignment.Role).ToArray();

    private User() { }

    private User(Guid id, string email, string displayName, UserRole initialRole)
        : base(id)
    {
        Email = NormalizeEmail(email);
        DisplayName = RequireDisplayName(displayName);
        IsActive = true;
        _roleAssignments.Add(new UserRoleAssignment(Id, initialRole));
        RaiseDomainEvent(new UserCreatedDomainEvent(Id, Email));
    }

    public static User Create(string email, string displayName, UserRole initialRole = UserRole.Viewer)
        => new(Guid.NewGuid(), email, displayName, initialRole);

    public static User Create(Guid id, string email, string displayName, UserRole initialRole = UserRole.Viewer)
    {
        if (id == Guid.Empty) throw new ArgumentException("A user id is required.", nameof(id));
        return new(id, email, displayName, initialRole);
    }

    public void AssignRole(UserRole role)
    {
        if (_roleAssignments.All(assignment => assignment.Role != role))
        {
            _roleAssignments.Add(new UserRoleAssignment(Id, role));
            Touch();
            RaiseDomainEvent(new UserRoleAssignedDomainEvent(Id, role));
        }
    }

    public void RemoveRole(UserRole role)
    {
        var assignment = _roleAssignments.SingleOrDefault(current => current.Role == role);
        if (assignment is null) return;
        if (_roleAssignments.Count == 1)
            throw new InvalidOperationException("A user must retain at least one role.");

        _roleAssignments.Remove(assignment);
        Touch();
        RaiseDomainEvent(new UserRoleRemovedDomainEvent(Id, role));
    }

    public void Disable()
    {
        if (!IsActive) return;
        IsActive = false;
        Touch();
        RaiseDomainEvent(new UserDisabledDomainEvent(Id));
    }

    public void Enable()
    {
        if (IsActive) return;
        IsActive = true;
        Touch();
        RaiseDomainEvent(new UserEnabledDomainEvent(Id));
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("A valid email is required.", nameof(email));

        return email.Trim().ToLowerInvariant();
    }

    private static string RequireDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("A display name is required.", nameof(displayName));

        return displayName.Trim();
    }
}

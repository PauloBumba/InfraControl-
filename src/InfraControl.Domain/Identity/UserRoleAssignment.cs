using InfraControl.Domain.Common.Enums;
using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Identity;

public sealed class UserRoleAssignment : Entity
{
    public Guid UserId { get; private set; }
    public UserRole Role { get; private set; }

    private UserRoleAssignment() { }

    internal UserRoleAssignment(Guid userId, UserRole role)
    {
        UserId = userId;
        Role = role;
    }
}

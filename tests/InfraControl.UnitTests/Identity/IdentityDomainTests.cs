using InfraControl.Domain.Enums;
using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.UnitTests.Identity;

public sealed class IdentityDomainTests
{
    [Fact]
    public void User_Create_ShouldNormalizeEmailAssignViewerAndRaiseEvent()
    {
        var user = User.Create("  ADMIN@EXAMPLE.COM ", "Admin", UserRole.Viewer);

        user.Email.Should().Be("admin@example.com");
        user.IsActive.Should().BeTrue();
        user.Roles.Should().ContainSingle().Which.Should().Be(UserRole.Viewer);
        user.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<UserCreatedDomainEvent>();
    }

    [Fact]
    public void User_AssignRole_ShouldRaiseEventOnlyForNewRole()
    {
        var user = User.Create("admin@example.com", "Admin", UserRole.Admin);
        user.ClearDomainEvents();

        user.AssignRole(UserRole.User);
        user.AssignRole(UserRole.User);

        user.Roles.Should().Contain([UserRole.Admin, UserRole.User]);
        user.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<UserRoleAssignedDomainEvent>();
    }

    [Fact]
    public void UserSession_Revoke_ShouldMakeSessionInactiveAndRaiseEvent()
    {
        var session = UserSession.Start(Guid.NewGuid(), "refresh-token-hash", TimeSpan.FromHours(1));
        session.ClearDomainEvents();

        session.Revoke();

        session.IsActive.Should().BeFalse();
        session.RevokedAt.Should().NotBeNull();
        session.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<UserSessionRevokedDomainEvent>();
    }
}

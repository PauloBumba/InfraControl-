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

        user.AssignRole(UserRole.Operator);
        user.AssignRole(UserRole.Operator);

        user.Roles.Should().Contain([UserRole.Admin, UserRole.Operator]);
        user.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<UserRoleAssignedDomainEvent>();
    }

    [Fact]
    public void UserSession_Revoke_ShouldMakeSessionInactiveAndRaiseEvent()
    {
        var session = UserSession.Start(Guid.NewGuid(), "refresh-token-hash", TimeSpan.FromHours(1), TimeSpan.FromMinutes(30));
        session.ClearDomainEvents();

        session.Revoke();

        session.IsActive.Should().BeFalse();
        session.RevokedAt.Should().NotBeNull();
        session.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<UserSessionRevokedDomainEvent>();
    }

    [Fact]
    public void UserSession_Refresh_ShouldNot_Move_The_Absolute_Ceiling()
    {
        // ADR-018: o deslizante estica a cada refresh, o teto não. Sem isso a
        // sessão de quem usa o sistema o tempo todo nunca expira.
        var session = UserSession.Start(
            Guid.NewGuid(), "refresh-token-hash", TimeSpan.FromHours(8), TimeSpan.FromHours(2));
        var ceiling = session.AbsoluteExpiresAt;
        var sliding = session.ExpiresAt;

        session.Refresh("outro-token", TimeSpan.FromHours(8));

        session.ExpiresAt.Should().BeAfter(sliding, "o prazo deslizante anda");
        session.AbsoluteExpiresAt.Should().Be(ceiling, "o teto é o teto");
    }

    [Fact]
    public void UserSession_Should_Reject_A_Ceiling_Longer_Than_The_Sliding_Lifetime()
    {
        var act = () => UserSession.Start(
            Guid.NewGuid(), "hash", TimeSpan.FromMinutes(30), TimeSpan.FromHours(2));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*teto*");
    }
}

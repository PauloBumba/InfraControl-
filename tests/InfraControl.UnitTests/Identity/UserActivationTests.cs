using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Identity;

public sealed class UserActivationTests
{
    [Fact]
    public void CreatePending_ShouldCreateInactiveUserWithDistinctRoles()
    {
        var user = User.CreatePending(
            Guid.NewGuid(),
            " Pending@Example.com ",
            "Pending User",
            new[] { UserRole.User, UserRole.User, UserRole.Viewer });

        user.IsActive.Should().BeFalse("an account created outside an explicit activation step must not be active");
        user.Email.Should().Be("pending@example.com");
        user.DisplayName.Should().Be("Pending User");
        user.Roles.Should().BeEquivalentTo([UserRole.User, UserRole.Viewer]);
    }

    [Fact]
    public void CreatePending_ShouldRejectEmptyId()
    {
        var action = () => User.CreatePending(
            Guid.Empty,
            "pending@example.com",
            "Pending",
            new[] { UserRole.Viewer });

        action.Should().Throw<ArgumentException>().WithParameterName("id");
    }

    [Fact]
    public void CreatePending_ShouldRejectInvalidEmail()
    {
        var action = () => User.CreatePending(
            Guid.NewGuid(),
            "not-an-email",
            "Pending",
            new[] { UserRole.Viewer });

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreatePending_ShouldRejectEmptyRoles()
    {
        var action = () => User.CreatePending(
            Guid.NewGuid(),
            "pending@example.com",
            "Pending",
            Array.Empty<UserRole>());

        action.Should().Throw<ArgumentException>().WithParameterName("roles");
    }

    [Fact]
    public void CreatePending_ThenEnable_ShouldActivateUser()
    {
        var user = User.CreatePending(
            Guid.NewGuid(), "pending@example.com", "Pending", new[] { UserRole.User });
        user.ClearDomainEvents();

        user.Enable();

        user.IsActive.Should().BeTrue();
        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InfraControl.Domain.Events.UserEnabledDomainEvent>();
    }
}

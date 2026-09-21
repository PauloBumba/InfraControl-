using InfraControl.Domain.Enums;
using FluentAssertions;
using InfraControl.Application.Identity.Roles.Commands;
using InfraControl.Application.Identity.Roles.Ports;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Identity;

public sealed class RoleCommandTests
{
    [Fact]
    public async Task AssignCommand_ShouldChangeDomainAndPersistThroughPort()
    {
        var user = User.Create(Guid.NewGuid(), "viewer@example.com", "Viewer", UserRole.Viewer);
        var repository = new FakeRoleRepository(user, activeAdmins: 0);
        var handler = new AssignUserRoleCommandHandler(repository, NullLogger<AssignUserRoleCommandHandler>.Instance);

        await handler.Handle(new AssignUserRoleCommand(user.Id, UserRole.User), CancellationToken.None);

        user.Roles.Should().Contain([UserRole.Viewer, UserRole.User]);
        repository.Saved.Should().BeTrue();
        repository.LastRole.Should().Be(UserRole.User);
    }

    [Fact]
    public async Task RemoveAdminCommand_ShouldRejectRemovingLastActiveAdmin()
    {
        var user = User.Create(Guid.NewGuid(), "admin@example.com", "Admin", UserRole.Admin);
        var repository = new FakeRoleRepository(user, activeAdmins: 1);
        var handler = new RemoveUserRoleCommandHandler(repository, NullLogger<RemoveUserRoleCommandHandler>.Instance);

        var action = () => handler.Handle(new RemoveUserRoleCommand(user.Id, UserRole.Admin), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The last active administrator cannot lose the Admin role.");
        repository.Saved.Should().BeFalse();
    }

    private sealed class FakeRoleRepository(User user, int activeAdmins) : IUserRoleRepository
    {
        public bool Saved { get; private set; }
        public UserRole? LastRole { get; private set; }

        public Task<User?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<User?>(user.Id == userId ? user : null);

        public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(activeAdmins);

        public Task SaveRoleChangeAsync(User user, UserRole role, bool assign, CancellationToken cancellationToken = default)
        {
            Saved = true;
            LastRole = role;
            return Task.CompletedTask;
        }
    }
}

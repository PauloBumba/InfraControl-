using FluentAssertions;
using InfraControl.Application.Identity;
using InfraControl.Domain.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Identity;

public sealed class RoleManagementTests
{
    [Fact]
    public async Task RemoveAdmin_ShouldRejectRemovingTheLastActiveAdministrator()
    {
        var user = User.Create(Guid.NewGuid(), "admin@example.com", "Admin", UserRole.Admin);
        var repository = new FakeRoleRepository(user, activeAdmins: 1);
        var service = new RoleManagementService(repository, NullLogger<RoleManagementService>.Instance);

        var action = () => service.RemoveAsync(user.Id, UserRole.Admin);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The last active administrator cannot lose the Admin role.");
        repository.Saved.Should().BeFalse();
    }

    [Fact]
    public async Task AssignRole_ShouldPersistTheNewRole()
    {
        var user = User.Create(Guid.NewGuid(), "viewer@example.com", "Viewer", UserRole.Viewer);
        var repository = new FakeRoleRepository(user, activeAdmins: 0);
        var service = new RoleManagementService(repository, NullLogger<RoleManagementService>.Instance);

        await service.AssignAsync(user.Id, UserRole.User);

        user.Roles.Should().Contain([UserRole.Viewer, UserRole.User]);
        repository.Saved.Should().BeTrue();
        repository.LastRole.Should().Be(UserRole.User);
    }

    private sealed class FakeRoleRepository(User user, int activeAdmins) : IUserRoleRepository
    {
        public bool Saved { get; private set; }
        public UserRole? LastRole { get; private set; }

        public Task<User?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<User?>(user.Id == userId ? user : null);

        public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(activeAdmins);

        public Task SaveAsync(User user, UserRole role, bool assign, CancellationToken cancellationToken = default)
        {
            Saved = true;
            LastRole = role;
            return Task.CompletedTask;
        }
    }
}

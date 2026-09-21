using InfraControl.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Identity;

public interface IUserRoleRepository
{
    Task<User?> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(User user, UserRole role, bool assign, CancellationToken cancellationToken = default);
}

public interface IRoleManagementService
{
    Task AssignAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default);
}

public sealed class RoleManagementService(
    IUserRoleRepository repository,
    ILogger<RoleManagementService> logger) : IRoleManagementService
{
    public Task AssignAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default)
        => ChangeAsync(userId, role, assign: true, cancellationToken);

    public Task RemoveAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default)
        => ChangeAsync(userId, role, assign: false, cancellationToken);

    private async Task ChangeAsync(Guid userId, UserRole role, bool assign, CancellationToken cancellationToken)
    {
        var user = await repository.GetAsync(userId, cancellationToken)
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");

        if (assign && user.Roles.Contains(role)) return;
        if (!assign && !user.Roles.Contains(role)) return;

        if (!assign && role == UserRole.Admin && user.IsActive && await repository.CountActiveAdminsAsync(cancellationToken) <= 1)
            throw new InvalidOperationException("The last active administrator cannot lose the Admin role.");

        if (assign) user.AssignRole(role);
        else user.RemoveRole(role);

        await repository.SaveAsync(user, role, assign, cancellationToken);
        logger.LogInformation("Role {Role} {Action} for user {UserId}.", role, assign ? "assigned" : "removed", userId);
    }
}

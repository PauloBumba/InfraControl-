using InfraControl.Domain.Enums;

namespace InfraControl.Application.Identity.Administration;

public sealed record AdminUserSummary(
    Guid UserId,
    string Email,
    string DisplayName,
    bool IsActive,
    IReadOnlyCollection<UserRole> Roles,
    DateTime UpdatedAt);

public interface IIdentityAdministrationService
{
    Task<IReadOnlyCollection<AdminUserSummary>> ListUsersAsync(CancellationToken cancellationToken = default);
    Task<bool> SetActiveAsync(Guid userId, bool active, CancellationToken cancellationToken = default);
    Task<bool> AssignRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default);
    Task<bool> RemoveRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default);
}

using InfraControl.Application.Identity.Administration;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Auth;

public sealed class IdentityAdministrationService(AppDbContext dbContext) : IIdentityAdministrationService
{
    public async Task<IReadOnlyCollection<AdminUserSummary>> ListUsersAsync(CancellationToken cancellationToken = default)
        => await dbContext.DomainUsers
            .AsNoTracking()
            .Include(user => user.RoleAssignments)
            .OrderBy(user => user.Email)
            .Select(user => new AdminUserSummary(
                user.Id,
                user.Email,
                user.DisplayName,
                user.IsActive,
                user.RoleAssignments.Select(assignment => assignment.Role).ToArray(),
                user.UpdateDateTime))
            .ToArrayAsync(cancellationToken);

    public async Task<bool> SetActiveAsync(Guid userId, bool active, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.DomainUsers.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null) return false;
        if (active) user.Enable(); else user.Disable();
        if (!active)
        {
            var sessions = await dbContext.UserSessions.Where(session => session.UserId == userId && session.RevokedAt == null).ToListAsync(cancellationToken);
            foreach (var session in sessions) session.Revoke();
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> AssignRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId, cancellationToken);
        if (user is null) return false;
        user.AssignRole(role);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId, cancellationToken);
        if (user is null) return false;
        if (role == UserRole.Admin && user.IsActive && await dbContext.DomainUsers.CountAsync(item => item.IsActive && item.RoleAssignments.Any(assignment => assignment.Role == UserRole.Admin), cancellationToken) <= 1)
            throw new InvalidOperationException("The last active administrator cannot lose the Admin role.");
        user.RemoveRole(role);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
        => dbContext.DomainUsers.Include(user => user.RoleAssignments).FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
}

using InfraControl.Application.Identity.Roles.Ports;
using InfraControl.Domain.Identity;
using InfraControl.Infrastructure.Common.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Identity;

public sealed class IdentityUserRoleRepository(
    AppDbContext dbContext,
    UserManager<InfraControlUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager) : IUserRoleRepository
{
    public async Task<User?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.DomainUsers.SingleOrDefaultAsync(current => current.Id == userId, cancellationToken);
        if (user is not null)
            await dbContext.Entry(user).Collection("_roleAssignments").LoadAsync(cancellationToken);
        return user;
    }

    public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default)
        => dbContext.DomainUsers
            .Where(user => user.IsActive)
            .Where(user => dbContext.UserRoleAssignments.Any(role => role.UserId == user.Id && role.Role == UserRole.Admin))
            .CountAsync(cancellationToken);

    public async Task SaveRoleChangeAsync(User user, UserRole role, bool assign, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var identityUser = await userManager.FindByIdAsync(user.Id.ToString())
                ?? throw new KeyNotFoundException($"Identity user '{user.Id}' was not found.");
            var roleName = role.ToString();

            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                EnsureSucceeded(roleResult, "role");
            }

            var result = assign
                ? await userManager.AddToRoleAsync(identityUser, roleName)
                : await userManager.RemoveFromRoleAsync(identityUser, roleName);
            EnsureSucceeded(result, "role synchronization");

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded) return;
        var errors = string.Join("; ", result.Errors.Select(error => error.Code));
        throw new InvalidOperationException($"Unable to complete {operation}: {errors}");
    }
}

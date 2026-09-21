using InfraControl.Application.Identity;
using InfraControl.Domain.Identity;
using InfraControl.Infrastructure.Common.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Identity;

public sealed class IdentityAdminProvisioner(
    UserManager<InfraControlUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    AppDbContext dbContext) : IAdminIdentityProvisioner
{
    public async Task<bool> HasAdminAsync(CancellationToken cancellationToken = default)
    {
        var admins = await userManager.GetUsersInRoleAsync(UserRole.Admin.ToString());
        return admins.Count > 0;
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        => await userManager.Users.AnyAsync(user => user.Email == email, cancellationToken);

    public async Task ProvisionAsync(User user, string password, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var adminRole = UserRole.Admin.ToString();
            if (!await roleManager.RoleExistsAsync(adminRole))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(adminRole));
                EnsureSucceeded(roleResult, "admin role");
            }

            var identityUser = new InfraControlUser
            {
                Id = user.Id,
                UserName = user.Email,
                Email = user.Email,
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString("N")
            };

            var userResult = await userManager.CreateAsync(identityUser, password);
            EnsureSucceeded(userResult, "admin user");

            var roleAssignmentResult = await userManager.AddToRoleAsync(identityUser, adminRole);
            EnsureSucceeded(roleAssignmentResult, "admin role assignment");

            dbContext.Users.Add(user);
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
        throw new InvalidOperationException($"Unable to create {operation}: {errors}");
    }
}

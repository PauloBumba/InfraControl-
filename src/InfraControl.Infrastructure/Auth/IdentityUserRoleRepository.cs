using InfraControl.Domain.Enums;
using InfraControl.Domain.Authorization;
using InfraControl.Application.Identity.Roles.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InfraControl.Infrastructure.Auth;

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

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => GetAsync(userId, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        => await dbContext.DomainUsers.AddAsync(user, cancellationToken);

    public void Remove(User user)
        => dbContext.DomainUsers.Remove(user);

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
                ?? throw new KeyNotFoundException($"Usuário de identidade '{user.Id}' não encontrado.");
            var roleName = role.ToString();

            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                EnsureSucceeded(roleResult, "criação de função");
            }

            var result = assign
                ? await userManager.AddToRoleAsync(identityUser, roleName)
                : await userManager.RemoveFromRoleAsync(identityUser, roleName);
            EnsureSucceeded(result, "sincronização de função");

            await SynchronizePermissionClaimsAsync(identityUser, user.Roles, cancellationToken);

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
        throw new InvalidOperationException($"Não foi possível concluir {operation}: {errors}");
    }

    private async Task SynchronizePermissionClaimsAsync(
        InfraControlUser identityUser,
        IReadOnlyCollection<UserRole> roles,
        CancellationToken cancellationToken)
    {
        var currentClaims = await userManager.GetClaimsAsync(identityUser);
        var permissionClaims = currentClaims.Where(claim => claim.Type == PermissionClaim.Type).ToList();

        if (permissionClaims.Count > 0)
            EnsureSucceeded(await userManager.RemoveClaimsAsync(identityUser, permissionClaims), "limpeza de claims de permissão");

        var claims = RolePermissions.ForRoles(roles)
            .Select(permission => new Claim(PermissionClaim.Type, permission.ToString()))
            .ToArray();

        if (claims.Length > 0)
            EnsureSucceeded(await userManager.AddClaimsAsync(identityUser, claims), "sincronização de claims de permissão");
    }
}

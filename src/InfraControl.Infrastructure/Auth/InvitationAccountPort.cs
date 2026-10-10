using InfraControl.Application.Identity.Authentication;
using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Auth;

/// <summary>
/// Adaptador da porta de provisionamento de contas por convite.
/// Cria o usuário técnico (Identity, sem senha) e o agregado de domínio INATIVO
/// na etapa de aceite; na ativação define a senha e chama User.Enable().
/// </summary>
public sealed class InvitationAccountPort(
    UserManager<InfraControlUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    AppDbContext dbContext) : IInvitationAccountPort
{
    public async Task<InvitationAccountInfo?> GetAccountInfoAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await dbContext.DomainUsers
            .AsNoTracking()
            .Where(user => user.Email == normalizedEmail)
            .Select(user => new InvitationAccountInfo(user.Id, user.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid> ProvisionInactiveAccountAsync(
        string email,
        IReadOnlyCollection<UserRole> roles,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var existing = await dbContext.DomainUsers
            .Include(user => user.RoleAssignments)
            .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (existing is not null)
        {
            // Conta já existe (ex.: auto-registro inativo): sincroniza roles do
            // convite sem alterar o estado de ativação.
            foreach (var role in roles.Where(role => !existing.Roles.Contains(role)))
                existing.AssignRole(role);

            await dbContext.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var identityUser = new InfraControlUser
        {
            Id = Guid.NewGuid(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(identityUser);
        if (!createResult.Succeeded)
            throw new InvalidOperationException(
                $"Não foi possível provisionar a conta convidada: {string.Join("; ", createResult.Errors.Select(error => error.Description))}");

        var domainUser = User.CreatePending(identityUser.Id, normalizedEmail, normalizedEmail, roles);
        dbContext.DomainUsers.Add(domainUser);

        foreach (var role in domainUser.Roles)
        {
            await EnsureRoleAsync(role, cancellationToken);
            var assignmentResult = await userManager.AddToRoleAsync(identityUser, role.ToString());
            if (!assignmentResult.Succeeded)
                throw new InvalidOperationException(
                    $"Não foi possível atribuir a função {role}: {string.Join("; ", assignmentResult.Errors.Select(error => error.Description))}");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return domainUser.Id;
    }

    public async Task<OperationResult> SetPasswordAndActivateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var identityUser = await userManager.FindByEmailAsync(normalizedEmail);
        var domainUser = await dbContext.DomainUsers
            .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (identityUser is null || domainUser is null)
            return OperationResult.Failure("A conta convidada não foi encontrada.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (await userManager.HasPasswordAsync(identityUser))
        {
            var removeResult = await userManager.RemovePasswordAsync(identityUser);
            if (!removeResult.Succeeded)
                return OperationResult.Failure(removeResult.Errors.Select(error => error.Description).ToArray());
        }

        var addResult = await userManager.AddPasswordAsync(identityUser, password);
        if (!addResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Failure(addResult.Errors.Select(error => error.Description).ToArray());
        }

        domainUser.Enable();
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return OperationResult.Success();
    }

    private async Task EnsureRoleAsync(UserRole role, CancellationToken cancellationToken)
    {
        var roleName = role.ToString();
        if (!await roleManager.RoleExistsAsync(roleName))
            EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(roleName)), "criação de função");
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Não foi possível concluir {operation}: {string.Join(", ", result.Errors.Select(error => error.Description))}");
    }
}

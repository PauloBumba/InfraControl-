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

    public async Task<AdminUserSummary?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.DomainUsers
            .AsNoTracking()
            .Include(item => item.RoleAssignments)
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        return user is null
            ? null
            : new AdminUserSummary(
                user.Id, user.Email, user.DisplayName, user.IsActive,
                user.RoleAssignments.Select(assignment => assignment.Role).ToArray(),
                user.UpdateDateTime);
    }

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
        var user = await LoadUserAsync(userId, cancellationToken);
        if (user is null) return false;
        user.AssignRole(role);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(userId, cancellationToken);
        if (user is null) return false;
        if (role == UserRole.Admin && user.IsActive && await dbContext.DomainUsers.CountAsync(item => item.IsActive && item.RoleAssignments.Any(assignment => assignment.Role == UserRole.Admin), cancellationToken) <= 1)
            throw new InvalidOperationException("O último administrador ativo não pode perder a função Admin.");
        user.RemoveRole(role);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateUserAsync(Guid userId, string displayName, string email, CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(userId, cancellationToken);
        if (user is null) return false;

        // Verificar se o email já está em uso por outro usuário
        var existingUser = await dbContext.DomainUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.Id != userId, cancellationToken);
        if (existingUser is not null)
            throw new InvalidOperationException("Já existe um usuário com este e-mail.");

        // FullName explícito: o PUT do admin não tem o campo, então o valor
        // atual é repassado — o default (null) do domínio apagaria o nome
        // completo a cada edição de admin.
        user.Update(displayName, email, user.FullName);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteUserAsync(Guid userId, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(userId, cancellationToken);
        if (user is null) return false;

        // Guard: usuário não pode se auto-excluir
        if (userId == currentUserId)
            throw new InvalidOperationException("Você não pode excluir seu próprio usuário.");

        // Guard: último admin ativo não pode ser excluído
        if (user.IsActive && user.RoleAssignments.Any(assignment => assignment.Role == UserRole.Admin))
        {
            var adminCount = await dbContext.DomainUsers.CountAsync(
                item => item.IsActive && item.RoleAssignments.Any(assignment => assignment.Role == UserRole.Admin),
                cancellationToken);
            if (adminCount <= 1)
                throw new InvalidOperationException("O último administrador ativo não pode ser excluído.");
        }

        // Revogar todas as sessões do usuário
        var sessions = await dbContext.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
            session.Revoke();

        // Disparar evento de deleção antes de remover
        user.Delete();

        // Remover do banco (hard delete seguindo o padrão do Identity)
        // Nota: A tabela AspNetUsers do Identity deve ser removida via migração ou cascade
        dbContext.DomainUsers.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<User?> LoadUserAsync(Guid userId, CancellationToken cancellationToken)
        => dbContext.DomainUsers.Include(user => user.RoleAssignments).FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
}

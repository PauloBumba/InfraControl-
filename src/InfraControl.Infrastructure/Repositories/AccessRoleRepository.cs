using InfraControl.Application.Common.Security.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>Catálogo de perfis customizados (spec evolução — perfis do admin).</summary>
public sealed class AccessRoleRepository(AppDbContext dbContext)
    : IAccessRoleRepository
{
    public async Task<IReadOnlyList<AccessRole>> ListAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.AccessRoles.AsNoTracking().OrderBy(r => r.Label)
            .ToListAsync(cancellationToken);

    public Task<AccessRole?> FindByKeyAsync(
        string key, CancellationToken cancellationToken = default) =>
        dbContext.AccessRoles.FirstOrDefaultAsync(
            r => r.Key == key, cancellationToken);

    public Task<AccessRole?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        dbContext.AccessRoles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task AddAsync(
        AccessRole entity, CancellationToken cancellationToken = default) =>
        await dbContext.AccessRoles.AddAsync(entity, cancellationToken);

    public void Remove(AccessRole entity) =>
        dbContext.AccessRoles.Remove(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

/// <summary>Atribuições de perfis customizados a usuários.</summary>
public sealed class UserAccessRoleRepository(AppDbContext dbContext)
    : IUserAccessRoleRepository
{
    public async Task<IReadOnlyList<UserAccessRole>> ListByUserAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.UserAccessRoles.AsNoTracking()
            .Where(r => r.UserId == userId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserAccessRole>> ListByRoleKeyAsync(
        string roleKey, CancellationToken cancellationToken = default) =>
        await dbContext.UserAccessRoles.AsNoTracking()
            .Where(r => r.RoleKey == roleKey).ToListAsync(cancellationToken);

    public async Task<int> RemoveByRoleKeyAsync(
        string roleKey, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.UserAccessRoles
            .Where(r => r.RoleKey == roleKey).ToListAsync(cancellationToken);
        dbContext.UserAccessRoles.RemoveRange(rows);
        return rows.Count;
    }

    public async Task<IReadOnlyList<UserAccessRole>> ListByUsersAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
        await dbContext.UserAccessRoles.AsNoTracking()
            .Where(r => userIds.Contains(r.UserId)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> ListRoleKeysByUserAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.UserAccessRoles.AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => r.RoleKey)
            .ToListAsync(cancellationToken);

    public Task<UserAccessRole?> FindAsync(
        Guid userId, string roleKey, CancellationToken cancellationToken = default) =>
        dbContext.UserAccessRoles.FirstOrDefaultAsync(
            r => r.UserId == userId && r.RoleKey == roleKey, cancellationToken);

    public async Task AddAsync(
        UserAccessRole entity, CancellationToken cancellationToken = default) =>
        await dbContext.UserAccessRoles.AddAsync(entity, cancellationToken);

    public void Remove(UserAccessRole entity) =>
        dbContext.UserAccessRoles.Remove(entity);

    public Task<UserAccessRole?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        dbContext.UserAccessRoles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Common.Security.Ports;

/// <summary>Persistência dos grants de acesso dinâmico (ADR-066).</summary>
public interface IAccessPolicyRepository : IRepository<AccessPolicyRule>
{
    Task<IReadOnlyList<AccessPolicyRule>> ListAsync(CancellationToken cancellationToken = default);

    Task<AccessPolicyRule?> FindAsync(
        string roleKey, string moduleKey, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Catálogo de perfis de acesso customizados criados pelo admin.</summary>
public interface IAccessRoleRepository : IRepository<AccessRole>
{
    Task<IReadOnlyList<AccessRole>> ListAsync(CancellationToken cancellationToken = default);
    Task<AccessRole?> FindByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<AccessRole?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(AccessRole entity, CancellationToken cancellationToken = default);
    void Remove(AccessRole entity);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Atribuições de perfis customizados a usuários.</summary>
public interface IUserAccessRoleRepository : IRepository<UserAccessRole>
{
    Task<IReadOnlyList<UserAccessRole>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserAccessRole>> ListByRoleKeyAsync(string roleKey, CancellationToken cancellationToken = default);
    Task<int> RemoveByRoleKeyAsync(string roleKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserAccessRole>> ListByUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListRoleKeysByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserAccessRole?> FindAsync(Guid userId, string roleKey, CancellationToken cancellationToken = default);
    Task AddAsync(UserAccessRole entity, CancellationToken cancellationToken = default);
    void Remove(UserAccessRole entity);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

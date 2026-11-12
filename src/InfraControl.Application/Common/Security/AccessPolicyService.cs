using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Common.Security.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Access;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Common.Security;

// ------------------------------------------------------------------
// DTOs
// ------------------------------------------------------------------

public sealed record AccessRoleGrantDto(
    string Role,
    int Level,
    bool IsOverride);

public sealed record AccessModulePolicyDto(
    string Key,
    string Label,
    IReadOnlyList<AccessRoleGrantDto> Roles);

public sealed record AccessPolicyMatrixDto(
    IReadOnlyList<AccessModulePolicyDto> Modules,
    IReadOnlyList<AccessRoleInfoDto> CustomRoles);

public sealed record AccessRoleInfoDto(
    string Key,
    string Label,
    string? Description);

public sealed record AccessPolicyGrantInput(
    string Role,
    string ModuleKey,
    int? Level);

// ------------------------------------------------------------------
// Queries / Commands — matriz de acesso
// ------------------------------------------------------------------

public sealed record GetAccessPolicyMatrixQuery
    : IQuery<AccessPolicyMatrixDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed class GetAccessPolicyMatrixQueryHandler(IAccessPolicyService service)
    : IRequestHandler<GetAccessPolicyMatrixQuery, AccessPolicyMatrixDto>
{
    public Task<AccessPolicyMatrixDto> Handle(
        GetAccessPolicyMatrixQuery request, CancellationToken cancellationToken) =>
        service.GetMatrixAsync(cancellationToken);
}

public sealed record UpdateAccessPolicyGrantsCommand(
    IReadOnlyList<AccessPolicyGrantInput> Grants)
    : BaseCommand<AccessPolicyMatrixDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
    public string AuditResource => "AccessPolicy";
}

public sealed class UpdateAccessPolicyGrantsCommandHandler(IAccessPolicyService service)
    : IRequestHandler<UpdateAccessPolicyGrantsCommand, AccessPolicyMatrixDto>
{
    public Task<AccessPolicyMatrixDto> Handle(
        UpdateAccessPolicyGrantsCommand request, CancellationToken cancellationToken) =>
        service.ReplaceGrantsAsync(request.Grants, cancellationToken);
}

// ------------------------------------------------------------------
// Perfis customizados (spec evolução — admin cria papel e define o que
// ele pode ver/fazer na matriz role×módulo)
// ------------------------------------------------------------------

public sealed record AccessRoleDto(
    Guid Id,
    string Key,
    string Label,
    string? Description,
    int UserCount);

public sealed record CreateAccessRoleCommand(
    string Key, string Label, string? Description)
    : BaseCommand<AccessRoleDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
    public string AuditResource => "AccessRole";
}

public sealed class CreateAccessRoleCommandHandler(
    IAccessRoleRepository roles,
    IAccessPolicyService policy)
    : IRequestHandler<CreateAccessRoleCommand, AccessRoleDto>
{
    public async Task<AccessRoleDto> Handle(
        CreateAccessRoleCommand request, CancellationToken cancellationToken)
    {
        var key = AccessRole.NormalizeKey(request.Key);
        if (await roles.FindByKeyAsync(key, cancellationToken) is not null)
            throw new InvalidOperationException($"Já existe um perfil com a chave '{key}'.");

        var role = AccessRole.Create(key, request.Label, request.Description);
        await roles.AddAsync(role, cancellationToken);
        await roles.SaveChangesAsync(cancellationToken);
        await policy.InvalidateRolesCacheAsync(cancellationToken);
        return new AccessRoleDto(role.Id, role.Key, role.Label, role.Description, 0);
    }
}

public sealed record UpdateAccessRoleCommand(
    Guid Id, string Label, string? Description)
    : BaseCommand<AccessRoleDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
    public string AuditResource => "AccessRole";
}

public sealed class UpdateAccessRoleCommandHandler(
    IAccessRoleRepository roles,
    IAccessPolicyService policy)
    : IRequestHandler<UpdateAccessRoleCommand, AccessRoleDto>
{
    public async Task<AccessRoleDto> Handle(
        UpdateAccessRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await roles.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Perfil não encontrado.");
        role.Update(request.Label, request.Description);
        await roles.SaveChangesAsync(cancellationToken);
        await policy.InvalidateRolesCacheAsync(cancellationToken);
        return new AccessRoleDto(role.Id, role.Key, role.Label, role.Description, 0);
    }
}

public sealed record DeleteAccessRoleCommand(Guid Id)
    : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
    public string AuditResource => "AccessRole";
}

public sealed class DeleteAccessRoleCommandHandler(
    IAccessRoleRepository roles,
    IUserAccessRoleRepository assignments,
    IAccessPolicyRepository policyRules,
    IAccessPolicyService policy)
    : IRequestHandler<DeleteAccessRoleCommand>
{
    public async Task Handle(
        DeleteAccessRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await roles.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Perfil não encontrado.");

        // Remove atribuições e regras do módulo antes de apagar o perfil —
        // chave órfã viraria "fantasma" na matriz.
        var rules = await policyRules.ListAsync(cancellationToken);
        foreach (var rule in rules.Where(r =>
            r.RoleKey.Equals(role.Key, StringComparison.OrdinalIgnoreCase)))
            policyRules.Remove(rule);

        await assignments.RemoveByRoleKeyAsync(role.Key, cancellationToken);

        roles.Remove(role);
        await roles.SaveChangesAsync(cancellationToken);
        await policy.InvalidateRolesCacheAsync(cancellationToken);
    }
}

public sealed record SearchAccessRolesQuery
    : IQuery<IReadOnlyList<AccessRoleDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed class SearchAccessRolesQueryHandler(
    IAccessRoleRepository roles,
    IUserAccessRoleRepository assignments)
    : IRequestHandler<SearchAccessRolesQuery, IReadOnlyList<AccessRoleDto>>
{
    public async Task<IReadOnlyList<AccessRoleDto>> Handle(
        SearchAccessRolesQuery request, CancellationToken cancellationToken)
    {
        var all = await roles.ListAsync(cancellationToken);
        var result = new List<AccessRoleDto>(all.Count);
        foreach (var role in all)
        {
            var users = await assignments.ListByRoleKeyAsync(role.Key, cancellationToken);
            result.Add(new AccessRoleDto(
                role.Id, role.Key, role.Label, role.Description, users.Count));
        }
        return result;
    }
}

// ------------------------------------------------------------------
// Atribuição de perfis customizados a usuários
// ------------------------------------------------------------------

public sealed record UserAccessRoleDto(string Key, string Label);

public sealed record GetUserAccessRolesQuery(Guid UserId)
    : IQuery<IReadOnlyList<UserAccessRoleDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed class GetUserAccessRolesQueryHandler(
    IUserAccessRoleRepository assignments,
    IAccessRoleRepository roles)
    : IRequestHandler<GetUserAccessRolesQuery, IReadOnlyList<UserAccessRoleDto>>
{
    public async Task<IReadOnlyList<UserAccessRoleDto>> Handle(
        GetUserAccessRolesQuery request, CancellationToken cancellationToken)
    {
        var assigned = await assignments.ListByUserAsync(request.UserId, cancellationToken);
        var catalog = await roles.ListAsync(cancellationToken);
        return assigned
            .Select(a => catalog.FirstOrDefault(r =>
                r.Key.Equals(a.RoleKey, StringComparison.OrdinalIgnoreCase)))
            .Where(r => r is not null)
            .Select(r => new UserAccessRoleDto(r!.Key, r.Label))
            .ToArray();
    }
}

public sealed record SetUserAccessRolesCommand(Guid UserId, IReadOnlyList<string> RoleKeys)
    : BaseCommand<IReadOnlyList<UserAccessRoleDto>>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
    public string AuditResource => "UserAccessRole";
}

public sealed class SetUserAccessRolesCommandHandler(
    IUserAccessRoleRepository assignments,
    IAccessRoleRepository roles,
    IAccessPolicyService policy)
    : IRequestHandler<SetUserAccessRolesCommand, IReadOnlyList<UserAccessRoleDto>>
{
    public async Task<IReadOnlyList<UserAccessRoleDto>> Handle(
        SetUserAccessRolesCommand request, CancellationToken cancellationToken)
    {
        var catalog = await roles.ListAsync(cancellationToken);
        var wanted = (request.RoleKeys ?? [])
            .Select(AccessRole.NormalizeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var key in wanted)
        {
            if (catalog.All(r => !r.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Perfil '{key}' não existe.", nameof(request));
        }

        var current = await assignments.ListByUserAsync(request.UserId, cancellationToken);
        foreach (var existing in current)
        {
            if (!wanted.Contains(existing.RoleKey, StringComparer.OrdinalIgnoreCase))
                assignments.Remove(existing);
        }
        foreach (var key in wanted)
        {
            if (current.All(e => !e.RoleKey.Equals(key, StringComparison.OrdinalIgnoreCase)))
                await assignments.AddAsync(
                    UserAccessRole.Assign(request.UserId, key), cancellationToken);
        }

        await assignments.SaveChangesAsync(cancellationToken);
        await policy.InvalidateRolesCacheAsync(cancellationToken);

        return wanted
            .Select(key => catalog.First(r => r.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
            .Select(r => new UserAccessRoleDto(r.Key, r.Label))
            .ToArray();
    }
}

// ------------------------------------------------------------------
// Serviço de resolução (usado pelo AuthorizationBehavior, handler HTTP
// e middleware — ADR-066)
// ------------------------------------------------------------------

public interface IAccessPolicyService
{
    /// <summary>Nível efetivo do papel no módulo (grant ou default ADR-016).</summary>
    Task<AccessLevel> GetEffectiveLevelAsync(
        string roleKey, string moduleKey, CancellationToken cancellationToken = default);

    /// <summary>Algum papel do usuário atinge o nível exigido no módulo.</summary>
    Task<bool> HasAccessAsync(
        IReadOnlyCollection<string> roleNames,
        AccessLevel required,
        string moduleKey,
        CancellationToken cancellationToken = default);

    /// <summary>Matriz módulos × papéis (embutidos + perfis customizados).</summary>
    Task<AccessPolicyMatrixDto> GetMatrixAsync(CancellationToken cancellationToken = default);

    /// <summary>Substitui o conjunto de overrides (level null = volta ao default).</summary>
    Task<AccessPolicyMatrixDto> ReplaceGrantsAsync(
        IReadOnlyList<AccessPolicyGrantInput> grants,
        CancellationToken cancellationToken = default);

    /// <summary>Invalida caches de papéis/regras (após CRUD de perfil ou atribuição).</summary>
    Task InvalidateRolesCacheAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolve a matriz papel×módulo com cache curto via <see cref="ICacheService"/>.
/// Papéis embutidos sem regra = defaults do ADR-016 (Viewer→Read,
/// Operator→Operate, Admin→Administer). Perfis customizados sem regra =
/// <see cref="AccessLevel.None"/> (opt-in explícito pelo admin).
/// </summary>
public sealed class AccessPolicyService(
    IAccessPolicyRepository repository,
    IAccessRoleRepository customRoles,
    ICacheService cache,
    ILogger<AccessPolicyService> logger) : IAccessPolicyService
{
    // v2: a entidade AccessPolicyRule não faz round-trip no System.Text.Json
    // (ctor privado + setters privados) — cachear DTO em vez do agregado.
    private const string CacheKey = "access-policy:rules:v2";
    private const string RolesCacheKey = "access-policy:roles";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    /// <summary>Shape serializável da regra para o cache (evita envenenar
    /// o fallback/Redis com JSON que não volta para a entidade).</summary>
    private sealed record CachedAccessRule(
        string RoleKey, string ModuleKey, AccessLevel Level);

    private static readonly string[] BuiltInRoles =
        [nameof(UserRole.Viewer), nameof(UserRole.Operator), nameof(UserRole.Admin)];

    public async Task<AccessLevel> GetEffectiveLevelAsync(
        string roleKey, string moduleKey, CancellationToken cancellationToken = default)
    {
        var rules = await LoadRulesAsync(cancellationToken);
        var rule = rules.FirstOrDefault(r =>
            r.RoleKey.Equals(roleKey, StringComparison.OrdinalIgnoreCase) &&
            r.ModuleKey.Equals(moduleKey, StringComparison.OrdinalIgnoreCase));
        return rule?.Level ?? DefaultLevel(roleKey);
    }

    public async Task<bool> HasAccessAsync(
        IReadOnlyCollection<string> roleNames,
        AccessLevel required,
        string moduleKey,
        CancellationToken cancellationToken = default)
    {
        var rules = await LoadRulesAsync(cancellationToken);
        var needed = (int)required;

        foreach (var name in roleNames)
        {
            var rule = rules.FirstOrDefault(r =>
                r.RoleKey.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                r.ModuleKey.Equals(moduleKey, StringComparison.OrdinalIgnoreCase));
            var level = (int)(rule?.Level ?? DefaultLevel(name));
            if (level >= needed)
                return true;
        }

        return false;
    }

    public async Task<AccessPolicyMatrixDto> GetMatrixAsync(
        CancellationToken cancellationToken = default)
    {
        var rules = await LoadRulesAsync(cancellationToken);
        var roleKeys = await LoadRoleKeysAsync(cancellationToken);
        var custom = await customRoles.ListAsync(cancellationToken);
        return BuildMatrix(rules, roleKeys, custom);
    }

    public async Task<AccessPolicyMatrixDto> ReplaceGrantsAsync(
        IReadOnlyList<AccessPolicyGrantInput> grants,
        CancellationToken cancellationToken = default)
    {
        var knownRoles = await LoadRoleKeysAsync(cancellationToken);

        foreach (var grant in grants)
        {
            var roleKey = (grant.Role ?? string.Empty).Trim();
            if (roleKey.Length == 0 ||
                knownRoles.All(r => !r.Equals(roleKey, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Papel '{grant.Role}' desconhecido.", nameof(grants));

            if (string.IsNullOrWhiteSpace(grant.ModuleKey) ||
                AccessModuleCatalog.Modules.All(m =>
                    !m.Key.Equals(grant.ModuleKey.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException(
                    $"Módulo '{grant.ModuleKey}' desconhecido.", nameof(grants));

            if (grant.Level is { } level && !Enum.IsDefined((AccessLevel)level))
                throw new ArgumentException(
                    $"Nível '{level}' inválido para o módulo '{grant.ModuleKey}'.",
                    nameof(grants));

            // Salvaguarda ADR-066: impede o admin de se trancar fora da
            // própria administração de acesso.
            if (roleKey.Equals(nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase) &&
                grant.ModuleKey.Trim().Equals("admin", StringComparison.OrdinalIgnoreCase) &&
                grant.Level is { } l && (AccessLevel)l < AccessLevel.Administer)
                throw new ArgumentException(
                    "O Admin não pode ser rebaixado abaixo de Administrar no módulo Administração.",
                    nameof(grants));
        }

        var existing = await repository.ListAsync(cancellationToken);

        var seen = new HashSet<(string Role, string Module)>();
        foreach (var grant in grants)
        {
            var roleKey = grant.Role.Trim().ToLowerInvariant();
            var moduleKey = grant.ModuleKey.Trim().ToLowerInvariant();
            seen.Add((roleKey, moduleKey));

            var current = await repository.FindAsync(roleKey, moduleKey, cancellationToken);
            if (grant.Level is null)
            {
                if (current is not null)
                    repository.Remove(current);
                continue;
            }

            var level = (AccessLevel)grant.Level.Value;
            if (current is null)
            {
                await repository.AddAsync(
                    AccessPolicyRule.Create(roleKey, moduleKey, level), cancellationToken);
            }
            else
            {
                current.Update(level);
            }
        }

        // Remove overrides de células ausentes do payload (PUT = replace).
        foreach (var rule in existing)
        {
            if (!seen.Contains((
                rule.RoleKey.Trim().ToLowerInvariant(), rule.ModuleKey)))
                repository.Remove(rule);
        }

        await repository.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKey, cancellationToken);

        logger.LogInformation(
            "AccessPolicy: {Count} grants substituídos.", grants.Count);
        var custom = await customRoles.ListAsync(cancellationToken);
        return BuildMatrix(
            await LoadRulesAsync(cancellationToken),
            await LoadRoleKeysAsync(cancellationToken),
            custom);
    }

    private async Task<IReadOnlyList<AccessPolicyRule>> LoadRulesAsync(
        CancellationToken cancellationToken)
    {
        var cached = await cache.GetAsync<List<CachedAccessRule>>(CacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached
                .Select(r => AccessPolicyRule.Create(r.RoleKey, r.ModuleKey, r.Level))
                .ToList();
        }

        var rules = (await repository.ListAsync(cancellationToken)).ToList();
        await cache.SetAsync(
            CacheKey,
            rules.Select(r => new CachedAccessRule(r.RoleKey, r.ModuleKey, r.Level)).ToList(),
            CacheTtl,
            cancellationToken);
        return rules;
    }

    /// <summary>Papéis conhecidos: 3 embutidos + perfis customizados do admin.</summary>
    private async Task<IReadOnlyList<string>> LoadRoleKeysAsync(
        CancellationToken cancellationToken)
    {
        var cached = await cache.GetAsync<List<string>>(RolesCacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var custom = (await customRoles.ListAsync(cancellationToken))
            .Select(r => r.Key).ToList();
        var roles = BuiltInRoles.Concat(custom).ToList();
        await cache.SetAsync(RolesCacheKey, roles, CacheTtl, cancellationToken);
        return roles;
    }

    /// <summary>Invalida o cache de papéis após CRUD de perfil/atribuição.</summary>
    public async Task InvalidateRolesCacheAsync(CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(RolesCacheKey, cancellationToken);
        await cache.RemoveAsync(CacheKey, cancellationToken);
    }

    private static AccessPolicyMatrixDto BuildMatrix(
        IReadOnlyList<AccessPolicyRule> rules,
        IReadOnlyList<string> roleKeys,
        IReadOnlyList<AccessRole>? customRoles = null)
    {
        var customKeys = roleKeys
            .Where(k => !BuiltInRoles.Contains(k, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        // Mantém ordem estável: embutidos primeiro, customizados por chave.
        var orderedRoles = BuiltInRoles
            .Concat(customKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        var modules = AccessModuleCatalog.Modules.Select(module =>
        {
            var roleGrants = orderedRoles.Select(roleKey =>
            {
                var rule = rules.FirstOrDefault(r =>
                    r.RoleKey.Equals(roleKey, StringComparison.OrdinalIgnoreCase) &&
                    r.ModuleKey.Equals(module.Key, StringComparison.OrdinalIgnoreCase));
                return new AccessRoleGrantDto(
                    roleKey,
                    (int)(rule?.Level ?? DefaultLevel(roleKey)),
                    rule is not null);
            }).ToArray();

            return new AccessModulePolicyDto(module.Key, module.Label, roleGrants);
        }).ToArray();

        var customRoleInfos = (customRoles ?? [])
            .Select(r => new AccessRoleInfoDto(r.Key, r.Label, r.Description))
            .ToArray();

        return new AccessPolicyMatrixDto(modules, customRoleInfos);
    }

    /// <summary>Default ADR-016 p/ embutidos; customizado sem regra = None.</summary>
    public static AccessLevel DefaultLevel(string roleKey)
    {
        if (Enum.TryParse<UserRole>(roleKey, ignoreCase: true, out var role) &&
            Enum.IsDefined(role))
        {
            return role switch
            {
                UserRole.Admin => AccessLevel.Administer,
                UserRole.Operator => AccessLevel.Operate,
                _ => AccessLevel.Read,
            };
        }
        return AccessLevel.None;
    }

    /// <summary>Compat: overload por enum (testes legados).</summary>
    public static AccessLevel DefaultLevel(UserRole role) => DefaultLevel(role.ToString());
}

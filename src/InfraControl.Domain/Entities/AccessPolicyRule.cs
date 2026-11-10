using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Grant de acesso dinâmico (ADR-066, estendido p/ perfis customizados):
/// nível de um papel em um módulo do sistema. <see cref="RoleKey"/> é a
/// chave estável do papel — nome do <see cref="UserRole"/> embutido
/// ("Viewer"/"Operator"/"Admin") ou <see cref="AccessRole.Key"/> de um
/// perfil criado pelo admin. Tabela esparsa — ausência de linha = default
/// histórico do ADR-016 para os papéis embutidos; perfil customizado sem
/// regra = sem acesso a nenhum módulo (opt-in explícito).
/// </summary>
public sealed class AccessPolicyRule : Entity
{
    public const int ModuleKeyMaxLength = 64;
    public const int RoleKeyMaxLength = 40;

    /// <summary>Chave do papel: enum embutido ou AccessRole.Key.</summary>
    public string RoleKey { get; private set; } = string.Empty;
    public string ModuleKey { get; private set; } = string.Empty;
    public AccessLevel Level { get; private set; }

    private AccessPolicyRule() { }

    public static AccessPolicyRule Create(
        string roleKey, string moduleKey, AccessLevel level)
    {
        var normalizedRole = (roleKey ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedRole.Length == 0 || normalizedRole.Length > RoleKeyMaxLength)
            throw new ArgumentException(
                $"A chave do papel deve ter entre 1 e {RoleKeyMaxLength} caracteres.",
                nameof(roleKey));
        if (!Enum.IsDefined(level))
            throw new ArgumentOutOfRangeException(nameof(level), level, "Nível de acesso inválido.");

        var normalized = (moduleKey ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0 || normalized.Length > ModuleKeyMaxLength)
            throw new ArgumentException(
                $"A chave do módulo deve ter entre 1 e {ModuleKeyMaxLength} caracteres.",
                nameof(moduleKey));

        return new AccessPolicyRule
        {
            RoleKey = normalizedRole,
            ModuleKey = normalized,
            Level = level,
        };
    }

    /// <summary>Compat: cria regra a partir de papel embutido.</summary>
    public static AccessPolicyRule Create(
        UserRole role, string moduleKey, AccessLevel level) =>
        Create(role.ToString(), moduleKey, level);

    public void Update(AccessLevel level)
    {
        if (!Enum.IsDefined(level))
            throw new ArgumentOutOfRangeException(nameof(level), level, "Nível de acesso inválido.");
        Level = level;
    }
}

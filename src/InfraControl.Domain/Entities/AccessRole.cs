using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Perfil de acesso customizado (spec evolução — "admin adiciona novo
/// perfil e escolhe o que ele pode ver ou fazer"): papel nomeado que o
/// admin cria e atribui a usuários. A chave é estável e imutável; o
/// rótulo é cosmético. O nível por módulo vem de
/// <see cref="AccessPolicyRule"/> usando esta chave como papel —
/// perfis customizados aparecem como colunas extras na matriz do
/// <see cref="AccessModuleCatalog"/>. Perfis embutidos (Viewer,
/// Operator, Admin) NÃO são entidades — são constantes do ADR-016.
/// </summary>
public sealed class AccessRole : Entity
{
    public const int KeyMaxLength = 40;
    public const int LabelMaxLength = 120;
    public const int DescriptionMaxLength = 500;

    /// <summary>Chave estável usada em claims, regras e atribuições (slug).</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>Nome de exibição na UI.</summary>
    public string Label { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    private AccessRole() { }

    public static AccessRole Create(string key, string label, string? description)
    {
        var normalizedKey = NormalizeKey(key);
        var normalizedLabel = (label ?? string.Empty).Trim();
        if (normalizedLabel.Length == 0 || normalizedLabel.Length > LabelMaxLength)
            throw new ArgumentException(
                $"O nome do perfil deve ter entre 1 e {LabelMaxLength} caracteres.",
                nameof(label));

        return new AccessRole
        {
            Key = normalizedKey,
            Label = normalizedLabel,
            Description = NormalizeOptional(description, DescriptionMaxLength, nameof(description)),
        };
    }

    public void Update(string label, string? description)
    {
        var normalizedLabel = (label ?? string.Empty).Trim();
        if (normalizedLabel.Length == 0 || normalizedLabel.Length > LabelMaxLength)
            throw new ArgumentException(
                $"O nome do perfil deve ter entre 1 e {LabelMaxLength} caracteres.",
                nameof(label));

        Label = normalizedLabel;
        Description = NormalizeOptional(description, DescriptionMaxLength, nameof(description));
    }

    /// <summary>Valida formato de chave: slug minúsculo [a-z0-9-], sem colisão com os 3 perfis embutidos.</summary>
    public static string NormalizeKey(string? key)
    {
        var normalized = (key ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0 || normalized.Length > KeyMaxLength ||
            normalized.Any(c => !(char.IsLetterOrDigit(c) || c == '-')))
            throw new ArgumentException(
                $"A chave do perfil deve ser um slug de até {KeyMaxLength} caracteres " +
                "(letras, números e hífen).", nameof(key));

        foreach (var builtin in new[]
        {
            nameof(UserRole.Viewer).ToLowerInvariant(),
            nameof(UserRole.Operator).ToLowerInvariant(),
            nameof(UserRole.Admin).ToLowerInvariant(),
        })
        {
            if (normalized.Equals(builtin, StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals(char.ToUpperInvariant(builtin[0]) + builtin[1..], StringComparison.Ordinal))
                throw new ArgumentException(
                    $"A chave '{normalized}' é reservada ao perfil embutido.", nameof(key));
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int max, string param)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > max)
            throw new ArgumentException($"Valor excede {max} caracteres.", param);
        return trimmed;
    }
}

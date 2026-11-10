namespace InfraControl.Domain.Enums;

/// <summary>
/// Nível de acesso dinâmico por módulo (ADR-066). Espelha a hierarquia de
/// <see cref="RoleAccess"/> (Read=1, Operate=2, Administer=3) e adiciona
/// <see cref="None"/>=0 para revogação total — inclusive leitura.
/// </summary>
public enum AccessLevel
{
    None = 0,
    Read = 1,
    Operate = 2,
    Administer = 3,
}

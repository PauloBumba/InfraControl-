using InfraControl.Domain.Enums;

namespace InfraControl.Application.AI.Tools;

/// <summary>
/// Nível de acesso exigido por uma tool (ADR-040 · docs/34 §41) —
/// espelha a hierarquia canônica <see cref="RoleAccess"/> (ADR-016):
/// a MESMA fonte usada pelas policies HTTP e pelo
/// <c>IAuthorizedRequest</c> do pipeline MediatR.
/// </summary>
public enum AiToolAccess
{
    /// <summary>Qualquer role autenticada (equivale a [Authorize]).</summary>
    Read,

    /// <summary>Operação do dia a dia: Operator e Admin.</summary>
    Operate,

    /// <summary>Administração: somente Admin.</summary>
    Administer,
}

/// <summary>Conversão do nível para o conjunto de roles exigido.</summary>
public static class AiToolAccessExtensions
{
    public static IReadOnlyCollection<string> RequiredRoles(this AiToolAccess access) =>
        access switch
        {
            AiToolAccess.Read => RoleAccess.Read,
            AiToolAccess.Operate => RoleAccess.Operate,
            AiToolAccess.Administer => RoleAccess.Administer,
            _ => RoleAccess.Read,
        };
}

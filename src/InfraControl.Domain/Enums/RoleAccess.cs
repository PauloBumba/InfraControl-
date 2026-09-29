using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Enums;

/// <summary>
/// Hierarquia canônica de acesso por role (ADR-016). A role é a fonte única da
/// verdade: não existem permissões, apenas três níveis.
/// <list type="bullet">
/// <item><see cref="Read"/> — qualquer role do sistema (equivale a <c>[Authorize]</c>).</item>
/// <item><see cref="Operate"/> — operação do dia a dia (Operator e Admin).</item>
/// <item><see cref="Administer"/> — administração (somente Admin).</item>
/// </list>
/// É o mesmo conjunto usado pelas policies HTTP
/// (<c>InfraControl.Api.Authorization.AuthorizationPolicies</c>) e pela segunda
/// camada do pipeline MediatR (<c>IAuthorizedRequest.RequiredRoles</c>), para
/// que os dois pontos de checagem não divirjam.
/// </summary>
public static class RoleAccess
{
    /// <summary>Nível de leitura: todas as roles cadastradas.</summary>
    public static string[] Read => [.. Enum.GetNames<UserRole>()];

    /// <summary>Nível de operação: <c>Operator</c> e <c>Admin</c>.</summary>
    public static string[] Operate => [UserRole.Operator.ToString(), UserRole.Admin.ToString()];

    /// <summary>Nível de administração: somente <c>Admin</c>.</summary>
    public static string[] Administer => [UserRole.Admin.ToString()];
}

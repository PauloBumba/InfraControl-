using InfraControl.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace InfraControl.Api.Authorization;

/// <summary>
/// Policies HTTP de autorização (ADR-016). A role é a fonte única da verdade:
/// não existem policies por permissão. Três níveis canônicos:
/// <list type="bullet">
/// <item><b>Ler</b> — <c>[Authorize]</c> sem policy: qualquer role autenticada.</item>
/// <item><b>Operar</b> — policy <see cref="Operate"/>: Operator e Admin.</item>
/// <item><b>Administrar</b> — policy <see cref="Administer"/>: somente Admin.</item>
/// </list>
/// Os nomes são constantes (e não literais) porque <c>[Authorize]</c> exige
/// argumento de compilação. A mesma hierarquia é aplicada na camada MediatR
/// por <c>IAuthorizedRequest.RequiredRoles</c>.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Operação do dia a dia: <c>Operator</c> e <c>Admin</c>.</summary>
    public const string Operate = "Operate";

    /// <summary>Administração: somente <c>Admin</c>.</summary>
    public const string Administer = "Administer";

    public static void AddRolePolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(Operate, policy => policy.RequireRole(RoleAccess.Operate));
        options.AddPolicy(Administer, policy => policy.RequireRole(RoleAccess.Administer));
    }
}

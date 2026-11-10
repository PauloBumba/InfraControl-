using InfraControl.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace InfraControl.Api.Authorization;

/// <summary>
/// Policies HTTP de autorização (ADR-016, estendido pelo ADR-066): os nomes
/// continuam os mesmos, mas a decisão deixou de ser <c>RequireRole</c>
/// estático — cada policy exige o <see cref="AccessLevel"/> correspondente
/// na matriz role×módulo (<see cref="DynamicAccessHandler"/>), com fallback
/// para a hierarquia legada quando a rota não resolve um módulo catalogado.
/// Leitura é <c>[Authorize]</c> simples + <c>AccessPolicyMiddleware</c>.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Operação do dia a dia: nível Operate por módulo.</summary>
    public const string Operate = "Operate";

    /// <summary>Administração: nível Administer por módulo.</summary>
    public const string Administer = "Administer";

    public static void AddRolePolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(Operate, policy =>
            policy.AddRequirements(new AccessRequirement(AccessLevel.Operate)));
        options.AddPolicy(Administer, policy =>
            policy.AddRequirements(new AccessRequirement(AccessLevel.Administer)));
    }
}

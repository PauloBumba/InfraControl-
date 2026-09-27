using InfraControl.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace InfraControl.Api.Authorization;

/// <summary>
/// Policies HTTP nomeadas por permissão (F6-T003/achado): os controllers
/// usam <c>[Authorize(Policy = "InventoryRead")]</c> etc., mas só existiam
/// claims de permissão (emitidos no login) — a policy nomeada faltava e a
/// requisição HTTP falharia em runtime. Escopo de claims continua no
/// pipeline MediatR (AuthorizationBehavior).
/// </summary>
public static class PermissionPolicies
{
    public static void AddPermissionPolicies(this AuthorizationOptions options)
    {
        foreach (Permission permission in Enum.GetValues<Permission>())
        {
            options.AddPolicy(permission.ToString(), policy =>
                policy.RequireClaim(
                    PermissionClaim.Type, permission.ToString()));
        }
    }
}

using System.Security.Claims;
using InfraControl.Application.Common.Security;
using InfraControl.Domain.Access;
using InfraControl.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace InfraControl.Api.Authorization;

/// <summary>
/// Requirement dinâmico de acesso (ADR-066): o nível exigido é checado
/// contra a matriz role×módulo persistida, resolvendo o módulo pelo
/// segmento de rota <c>/api/v1/{modulo}</c>. Módulo fora do catálogo →
/// fallback para a checagem legada de roles do ADR-016.
/// </summary>
public sealed class AccessRequirement(AccessLevel level) : IAuthorizationRequirement
{
    public AccessLevel Level { get; } = level;
}

public sealed class DynamicAccessHandler(IAccessPolicyService accessPolicy)
    : AuthorizationHandler<AccessRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, AccessRequirement requirement)
    {
        // Contexto não-HTTP (testes, gRPC, SignalR): fallback legado —
        // a matriz dinâmica só é resolvida quando há rota /api/v1/{modulo}.
        if (context.Resource is not HttpContext http)
        {
            SucceedLegacy(context, requirement);
            return;
        }

        var segment = http.Request.Path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .SkipWhile(s => !s.Equals("v1", StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .FirstOrDefault();

        var moduleKey = AccessModuleCatalog.FromRouteSegment(segment);
        var roles = context.User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value).ToArray();

        if (moduleKey is null)
        {
            SucceedLegacy(context, requirement, roles);
            return;
        }

        var allowed = await accessPolicy.HasAccessAsync(
            roles, requirement.Level, moduleKey, http.RequestAborted);
        if (allowed) context.Succeed(requirement);
        else context.Fail();
    }

    /// <summary>
    /// Fallback legado (ADR-016): Operate = Operator|Admin,
    /// Administer = Admin — nunca abre além do comportamento anterior.
    /// </summary>
    private static void SucceedLegacy(
        AuthorizationHandlerContext context, AccessRequirement requirement,
        IReadOnlyCollection<string>? roles = null)
    {
        roles ??= context.User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value).ToArray();
        var ok = requirement.Level switch
        {
            AccessLevel.Administer => roles.Any(r =>
                r.Equals(UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase)),
            AccessLevel.Operate => roles.Any(r =>
                r.Equals(UserRole.Operator.ToString(), StringComparison.OrdinalIgnoreCase) ||
                r.Equals(UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase)),
            _ => roles.Count > 0,
        };
        if (ok) context.Succeed(requirement);
        else context.Fail();
    }
}

using System.Security.Claims;
using InfraControl.Application.Common.Security;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Domain.Access;
using InfraControl.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace InfraControl.Api.Middleware;

/// <summary>
/// ADR-066 — aplica o nível Read da matriz role×módulo a endpoints com
/// <c>[Authorize]</c> simples (sem policy Operate/Administer). Roda depois
/// de <c>UseAuthorization</c>: usuário autenticado + módulo catalogado com
/// grant <see cref="AccessLevel.None"/> → 403. Módulo desconhecido ou
/// não autenticado → segue o fluxo normal.
/// O corpo é o envelope <c>ApiResponse</c> (F0-T023 — nunca <c>{success}</c> cru).
/// </summary>
public sealed class AccessPolicyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context, IAccessPolicyService accessPolicy)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var segment = context.Request.Path.Value?
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .SkipWhile(s => !s.Equals("v1", StringComparison.OrdinalIgnoreCase))
                .Skip(1)
                .FirstOrDefault();
            var moduleKey = AccessModuleCatalog.FromRouteSegment(segment);

            if (moduleKey is not null)
            {
                var roles = user.FindAll(ClaimTypes.Role)
                    .Select(c => c.Value).ToArray();
                var allowed = await accessPolicy.HasAccessAsync(
                    roles, AccessLevel.Read, moduleKey, context.RequestAborted);
                if (!allowed)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(
                        ApiResponse<object>.Fail(
                        [
                            $"Acesso negado ao módulo '{moduleKey}' (nível Read)."
                        ]),
                        context.RequestAborted);
                    return;
                }
            }
        }

        await next(context);
    }
}

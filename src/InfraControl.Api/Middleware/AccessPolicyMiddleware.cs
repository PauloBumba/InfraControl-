using System.Security.Claims;
using System.Text.Json;
using InfraControl.Application.Common.Security;
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
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        success = false,
                        message = $"Acesso negado ao módulo '{moduleKey}'.",
                    }));
                    return;
                }
            }
        }

        await next(context);
    }
}

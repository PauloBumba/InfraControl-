using System.Security.Claims;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Identity.Sessions;
using InfraControl.BuildingBlocks.Application.Response;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InfraControl.Api.Middleware;

/// <summary>
/// F1-T016(b) / ADR-018: o cookie é auto-contido, então sem isto revogar
/// sessão não tirava ninguém de lugar e a sessão não tinha teto. Aqui a sessão
/// que o cookie diz ser é conferida contra a <c>UserSessions</c> a cada request
/// autenticado.
///
/// Fica depois de <c>UseAuthentication</c> (precisa do principal) e antes de
/// <c>UseAuthorization</c> (a sessão morre antes de qualquer decisão de
/// autorização ou lógica de endpoint).
///
/// Recusa é 401 + cookie apagado: o <c>401</c> global do frontend derruba a
/// sessão local e o cookie deletado evita o loop de 401 a cada navegação.
/// A mensagem é a mesma para revogada, expirada, inexistente ou de outro
/// usuário — informar qual seria dar ao portador do cookie um oráculo sobre a
/// sessão. O motivo vai para o log.
/// </summary>
public sealed class ActiveSessionMiddleware(
    RequestDelegate next,
    IOptions<SessionLifetimeOptions> options,
    ILogger<ActiveSessionMiddleware> logger)
{
    /// <summary>Claim que diz QUAL sessão é — sem ela o cookie não tem autoridade.</summary>
    public const string SessionIdClaim = "sid";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var (sessionId, userId) = ReadIdentity(context.User);

        if (sessionId is null || userId is null)
        {
            // Cookie emitido antes da ADR-018: não tem sid. Fail-closed — deixá-lo
            // passar manteria exatamente o buraco que a task fecha. Custo: um
            // re-login (o mesmo que o rename User→Operator já custou).
            logger.LogWarning("Authenticated cookie without a session id claim; forcing re-login.");
            await RejectAsync(context);
            return;
        }

        var check = await ResolveAsync(context, sessionId.Value, userId.Value, context.RequestAborted);
        if (!check.IsActive)
        {
            logger.LogWarning(
                "Session {SessionId} rejected for user {UserId}: {Status}",
                sessionId, userId, check.Status);
            await RejectAsync(context);
            return;
        }

        await next(context);
    }

    private async Task<ActiveSessionCheck> ResolveAsync(
        HttpContext context, Guid sessionId, Guid userId, CancellationToken cancellationToken)
    {
        // Resolvido POR REQUEST (não no construtor): IActiveSessionReader e
        // ICacheService são escopados — injetá-los no construtor do middleware
        // (singleton) os capturaria no root provider: o host com ValidateScopes
        // (Development) não sobe, e em produção o AppDbContext ficaria preso
        // para sempre na instância do middleware (partilhado entre requests
        // concorrentes). Descoberto pelo e2e de pipeline completo.
        var services = context.RequestServices;
        var cache = services.GetRequiredService<ICacheService>();
        var sessionReader = services.GetRequiredService<IActiveSessionReader>();

        // ADR-020: o cache do check é o distribuído (Redis primário, fallback
        // local — ADR-010), não mais o IMemoryCache da instância. Quem revoga
        // grava "Revoked" na mesma chave, então a outra instância lê o estado
        // negativo na hora; sem isto a revogação só pegaria no CheckInterval.
        var key = ActiveSessionCache.Key(sessionId);
        var cached = await cache.GetAsync<ActiveSessionCheck>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var check = await sessionReader.CheckAsync(sessionId, userId, cancellationToken);

        // até sessão recusada entra no cache: sem isso, um cookie morto vira uma
        // consulta ao banco por request enquanto o usuário navega
        await cache.SetAsync(key, check, options.Value.CheckInterval, cancellationToken);
        return check;
    }

    private static (Guid? SessionId, Guid? UserId) ReadIdentity(ClaimsPrincipal principal)
    {
        var sessionClaim = principal.FindFirst(SessionIdClaim)?.Value;
        var userClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? principal.FindFirst("sub")?.Value;

        return (Guid.TryParse(sessionClaim, out var sessionId) ? sessionId : null,
                Guid.TryParse(userClaim, out var userId) ? userId : null);
    }

    private static async Task RejectAsync(HttpContext context)
    {
        // apaga o cookie no navegador: sem isso o frontend reenvia o mesmo
        // cookie morto a cada request e o usuário fica preso em 401
        await context.SignOutAsync(IdentityConstants.ApplicationScheme);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(
            ApiResponse<object>.Fail(["A sessão expirou ou foi encerrada. Entre novamente."]));
    }
}

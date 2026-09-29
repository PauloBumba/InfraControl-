using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Identity.Authentication;
using InfraControl.Application.Identity.Sessions;
using InfraControl.Domain.Common;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.Auth;

/// <summary>
/// F1-T016b/ADR-020: auto-serviço de sessões. Cada revogação faz DUAS
/// escritas — a entidade no banco e o check "Revoked" no cache compartilhado.
/// A primeira é a fonte da verdade; a segunda derruba o cookie da outra
/// instância na hora, sem esperar o CheckInterval (era o que a ADR-018 deixou
/// pendente). Gravar o negativo em vez de apagar a chave é de propósito: apagar
/// deixaria o fallback local de outra instância servindo o positivo velho.
/// </summary>
public sealed class SessionService(
    AppDbContext dbContext,
    ICacheService cache,
    IOptions<SessionLifetimeOptions> options,
    IAuditWriter auditWriter) : ISessionService
{
    public async Task<IReadOnlyList<UserSessionItem>> ListAsync(
        Guid userId, Guid? currentSessionId, CancellationToken cancellationToken = default)
    {
        var now = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        return await dbContext.UserSessions
            .AsNoTracking()
            .Where(item => item.UserId == userId
                && item.RevokedAt == null
                && item.ExpiresAt > now
                && item.AbsoluteExpiresAt > now)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new UserSessionItem(
                item.Id,
                item.CreatedAt,
                item.LastSeenAt,
                item.ExpiresAt,
                item.AbsoluteExpiresAt,
                currentSessionId != null && item.Id == currentSessionId))
            .ToListAsync(cancellationToken);
    }

    public async Task<OperationResult> RevokeAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await dbContext.UserSessions
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null || session.UserId != userId)
            return OperationResult.Failure("Sessão não encontrada.");

        session.Revoke();
        // F1-T016c: revogação é evento de segurança — audit na mesma transação.
        await SecurityAudit.WriteAsync(auditWriter, dbContext, "SessionRevoked", "Session", userId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync([session.Id], cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult> RevokeOthersAsync(
        Guid userId, Guid? currentSessionId, CancellationToken cancellationToken = default)
    {
        // Sem saber qual é a atual, "as outras" seria tudo — inclusive esta.
        if (currentSessionId is null || currentSessionId == Guid.Empty)
            return OperationResult.Failure("Sessão atual não identificada.");

        var sessions = await dbContext.UserSessions
            .Where(item => item.UserId == userId
                && item.Id != currentSessionId
                && item.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions) session.Revoke();
        // F1-T016c: revogação em massa é evento de segurança — audit na mesma transação.
        await SecurityAudit.WriteAsync(auditWriter, dbContext, "SessionsRevoked", "Session", userId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(sessions.Select(session => session.Id), cancellationToken);
        return OperationResult.Success();
    }

    private async Task InvalidateAsync(IEnumerable<Guid> sessionIds, CancellationToken cancellationToken)
    {
        var revoked = new ActiveSessionCheck(ActiveSessionStatus.Revoked);
        foreach (var sessionId in sessionIds)
        {
            await cache.SetAsync(
                ActiveSessionCache.Key(sessionId), revoked, options.Value.CheckInterval, cancellationToken);
        }
    }
}

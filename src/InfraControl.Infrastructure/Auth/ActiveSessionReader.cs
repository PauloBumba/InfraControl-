using InfraControl.Application.Identity.Sessions;
using InfraControl.Domain.Common;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Auth;

/// <summary>
/// ADR-018: o leitor que dá autoridade ao cookie. É a única consulta que
/// decide se uma sessão auto-contida ainda vale — antes disso a tabela
/// UserSessions só era lida no refresh/logout, e revogar sessão não derrubava
/// cookie nenhum.
///
/// A distinção entre "expirou no deslizante" e "bateu o teto" é para log e
/// métrica: o cliente recebe 401 igual nos dois casos.
/// </summary>
public sealed class ActiveSessionReader(AppDbContext dbContext) : IActiveSessionReader
{
    public async Task<ActiveSessionCheck> CheckAsync(
        Guid sessionId, Guid userId, CancellationToken cancellationToken = default)
    {
        // projeção mínima: só o que decide. Trazer o registro inteiro custa
        // bytes à toa a cada request e não é usado por ninguém.
        var session = await dbContext.UserSessions
            .AsNoTracking()
            .Where(item => item.Id == sessionId)
            .Select(item => new { item.UserId, item.RevokedAt, item.ExpiresAt, item.AbsoluteExpiresAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
            return new ActiveSessionCheck(ActiveSessionStatus.NotFound);

        // o sid vem do cookie: conferir a dona impede que um cookie aponte
        // para a sessão de outra pessoa
        if (session.UserId != userId)
            return new ActiveSessionCheck(ActiveSessionStatus.WrongUser);

        if (session.RevokedAt is not null)
            return new ActiveSessionCheck(ActiveSessionStatus.Revoked);

        // mesmo relógio do domínio (AppTimeProvider), para o teste poder andar no tempo
        var now = AppTimeProvider.Current.GetUtcNow().UtcDateTime;

        return new ActiveSessionCheck(
            session.AbsoluteExpiresAt <= now ? ActiveSessionStatus.AbsoluteExpired
            : session.ExpiresAt <= now ? ActiveSessionStatus.SlidingExpired
            : ActiveSessionStatus.Active);
    }
}

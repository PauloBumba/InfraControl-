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
    /// <summary>
    /// F1-T016b/ADR-020: o "visto por último" da lista de sessões anda no
    /// máximo uma vez por este intervalo. Sem isto o dashboard, que não chama
    /// refresh, mostraria a hora do login como última atividade — a lista
    /// ficaria inútil para reconhecer dispositivo.
    /// </summary>
    private static readonly TimeSpan LastSeenTouchInterval = TimeSpan.FromMinutes(5);

    public async Task<ActiveSessionCheck> CheckAsync(
        Guid sessionId, Guid userId, CancellationToken cancellationToken = default)
    {
        // projeção mínima: só o que decide. Trazer o registro inteiro custa
        // bytes à toa a cada request e não é usado por ninguém.
        var session = await dbContext.UserSessions
            .AsNoTracking()
            .Where(item => item.Id == sessionId)
            .Select(item => new { item.UserId, item.RevokedAt, item.ExpiresAt, item.AbsoluteExpiresAt, item.LastSeenAt })
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

        var status = session.AbsoluteExpiresAt <= now ? ActiveSessionStatus.AbsoluteExpired
            : session.ExpiresAt <= now ? ActiveSessionStatus.SlidingExpired
            : ActiveSessionStatus.Active;

        // ADR-020: só sessão ATIVA é tocada (morta não ganha vida nova na
        // tela), e só fora do throttle — embutido no round trip que o cache
        // miss já faz, sem change tracker (ExecuteUpdate é SQL direto).
        var cutoff = now - LastSeenTouchInterval;
        if (status == ActiveSessionStatus.Active && session.LastSeenAt <= cutoff)
        {
            await dbContext.UserSessions
                .Where(item => item.Id == sessionId && item.LastSeenAt <= cutoff)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(item => item.LastSeenAt, now), cancellationToken);
        }

        return new ActiveSessionCheck(status);
    }
}

namespace InfraControl.Application.Identity.Sessions;

/// <summary>
/// ADR-018: por que a sessão do cookie não é mais considerada válida só porque
/// as claims estão lá. O motivo existe para log e para métrica — a resposta ao
/// cliente é a mesma (401) em todos os casos, para não informar qual era a
/// sessão.
/// </summary>
public enum ActiveSessionStatus
{
    Active,

    /// <summary>Nenhuma sessão com esse id — cookie forjado ou sessão já removida.</summary>
    NotFound,

    Revoked,
    SlidingExpired,
    AbsoluteExpired,

    /// <summary>A sessão existe mas é de outro usuário: o cookie não pode escolher a sessão alheia.</summary>
    WrongUser
}

public sealed record ActiveSessionCheck(ActiveSessionStatus Status)
{
    public static readonly ActiveSessionCheck Active = new(ActiveSessionStatus.Active);

    public bool IsActive => Status == ActiveSessionStatus.Active;
}

/// <summary>
/// Consulta o estado de UMA sessão. O <c>userId</c> acompanha o id de propósito:
/// o <c>sid</c> vem do cookie, então o servidor confere a dona.
/// </summary>
public interface IActiveSessionReader
{
    Task<ActiveSessionCheck> CheckAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);
}

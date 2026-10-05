using InfraControl.Application.Identity.Authentication;

namespace InfraControl.Application.Identity.Sessions;

/// <summary>
/// F1-T016b/ADR-020: auto-serviço de sessões — o usuário lista e revoga as
/// PRÓPRIAS sessões. Revogar é SEMPRE duas escritas: a entidade no banco
/// (fonte da verdade) e o check <see cref="ActiveSessionStatus.Revoked"/> no
/// cache compartilhado. Sem a segunda, o cookie de outra instância
/// sobreviveria até o <c>CheckInterval</c> — a "janela imediata" é isto.
/// </summary>
public interface ISessionService
{
    /// <summary>Sessões ativas do usuário, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<UserSessionItem>> ListAsync(
        Guid userId, Guid? currentSessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoga UMA sessão do usuário. Inexistente e de outra pessoa respondem
    /// a MESMA mensagem — o cliente não vira oráculo do sid.
    /// </summary>
    Task<OperationResult> RevokeAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// ADR-030 §2 — GET /auth/sessions/{id}: a própria sessão ativa.
    /// Alheia/revogada/expirada = null (o controller responde o MESMO 404 do
    /// revoke — o cliente não vira oráculo do sid).
    /// </summary>
    Task<UserSessionItem?> GetAsync(
        Guid userId, Guid sessionId, Guid? currentSessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Revoga todas as sessões do usuário menos a atual ("sair de todos os outros").</summary>
    Task<OperationResult> RevokeOthersAsync(
        Guid userId, Guid? currentSessionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Linha da tela de sessões. <c>IsCurrent</c> marca a sessão do cookie atual
/// (o sid que o middleware valida a cada request).
/// </summary>
public sealed record UserSessionItem(
    Guid Id,
    DateTime CreatedAt,
    DateTime LastSeenAt,
    DateTime ExpiresAt,
    DateTime AbsoluteExpiresAt,
    bool IsCurrent);

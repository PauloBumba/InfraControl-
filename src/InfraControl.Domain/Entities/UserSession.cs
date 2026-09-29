using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

public sealed class UserSession : AggregateRoot
{
    public Guid UserId { get; private set; }
    public string RefreshTokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime LastSeenAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime AbsoluteExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    // Dois prazos (ADR-018): o deslizante é esticado a cada Refresh, o teto
    // não. Sem o teto, uma sessão ativa nunca expira sozinha.
    public bool IsActive
    {
        get
        {
            var now = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
            return RevokedAt is null && ExpiresAt > now && AbsoluteExpiresAt > now;
        }
    }

    private UserSession() { }

    private UserSession(Guid id, Guid userId, string refreshTokenHash, DateTime expiresAt, DateTime absoluteExpiresAt)
        : base(id)
    {
        if (userId == Guid.Empty) throw new ArgumentException("O id do usuário é obrigatório.", nameof(userId));
        if (string.IsNullOrWhiteSpace(refreshTokenHash)) throw new ArgumentException("O hash do refresh token é obrigatório.", nameof(refreshTokenHash));
        var now = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        if (expiresAt <= now) throw new ArgumentException("A expiração da sessão deve estar no futuro.", nameof(expiresAt));
        if (absoluteExpiresAt <= now) throw new ArgumentException("A expiração absoluta da sessão deve estar no futuro.", nameof(absoluteExpiresAt));
        if (absoluteExpiresAt > expiresAt) throw new ArgumentException("O teto da sessão não pode ser maior que o prazo deslizante.", nameof(absoluteExpiresAt));

        UserId = userId;
        RefreshTokenHash = refreshTokenHash;
        CreatedAt = now;
        LastSeenAt = now;
        ExpiresAt = expiresAt;
        AbsoluteExpiresAt = absoluteExpiresAt;
        RaiseDomainEvent(new UserSessionStartedDomainEvent(Id, UserId));
    }

    public static UserSession Start(Guid userId, string refreshTokenHash, TimeSpan lifetime, TimeSpan absoluteLifetime)
    {
        if (absoluteLifetime > lifetime)
            throw new ArgumentException("O teto da sessão não pode ser maior que o prazo deslizante.", nameof(absoluteLifetime));

        var now = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        return new UserSession(
            Guid.NewGuid(), userId, refreshTokenHash, now.Add(lifetime), now.Add(absoluteLifetime));
    }

    public void Refresh(string refreshTokenHash, TimeSpan lifetime)
    {
        if (!IsActive) throw new InvalidOperationException("A sessão não está ativa.");
        if (string.IsNullOrWhiteSpace(refreshTokenHash)) throw new ArgumentException("O hash do refresh token é obrigatório.", nameof(refreshTokenHash));

        RefreshTokenHash = refreshTokenHash;
        LastSeenAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        ExpiresAt = LastSeenAt.Add(lifetime);
        Touch();
        RaiseDomainEvent(new UserSessionRefreshedDomainEvent(Id, UserId));
    }

    public void Revoke()
    {
        if (RevokedAt is not null) return;
        RevokedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        Touch();
        RaiseDomainEvent(new UserSessionRevokedDomainEvent(Id, UserId));
    }
}

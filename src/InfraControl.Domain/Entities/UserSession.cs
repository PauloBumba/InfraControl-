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
    public DateTime? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > AppTimeProvider.Current.GetUtcNow().UtcDateTime;

    private UserSession() { }

    private UserSession(Guid id, Guid userId, string refreshTokenHash, DateTime expiresAt)
        : base(id)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user id is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(refreshTokenHash)) throw new ArgumentException("A refresh token hash is required.", nameof(refreshTokenHash));
        if (expiresAt <= AppTimeProvider.Current.GetUtcNow().UtcDateTime) throw new ArgumentException("Session expiration must be in the future.", nameof(expiresAt));

        UserId = userId;
        RefreshTokenHash = refreshTokenHash;
        CreatedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        LastSeenAt = CreatedAt;
        ExpiresAt = expiresAt;
        RaiseDomainEvent(new UserSessionStartedDomainEvent(Id, UserId));
    }

    public static UserSession Start(Guid userId, string refreshTokenHash, TimeSpan lifetime)
        => new(Guid.NewGuid(), userId, refreshTokenHash, AppTimeProvider.Current.GetUtcNow().UtcDateTime.Add(lifetime));

    public void Refresh(string refreshTokenHash, TimeSpan lifetime)
    {
        if (!IsActive) throw new InvalidOperationException("The session is not active.");
        if (string.IsNullOrWhiteSpace(refreshTokenHash)) throw new ArgumentException("A refresh token hash is required.", nameof(refreshTokenHash));

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

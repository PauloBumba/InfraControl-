namespace InfraControl.Application.Common.Interfaces;

public enum RateLimitCategory
{
    Default,
    Login,
    PasswordReset,
    Webhook,
    Ingestion
}

public interface IRateLimitRequest
{
    RateLimitCategory Category { get; }
    string? RateLimitKey { get; }
}

public interface IRateLimiter
{
    /// <summary>
    /// F35-T002: além do booleano, devolve a janela restante quando o
    /// limite estoura (Retry-After do HTTP 429 — o limiter conhece o TTL).
    /// </summary>
    Task<(bool Acquired, TimeSpan? RetryAfter)> TryAcquireAsync(
        string key,
        RateLimitCategory category,
        CancellationToken cancellationToken = default);
}

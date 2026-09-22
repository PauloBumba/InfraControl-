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
    Task<bool> TryAcquireAsync(
        string key,
        RateLimitCategory category,
        CancellationToken cancellationToken = default);
}

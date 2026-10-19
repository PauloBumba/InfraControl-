namespace InfraControl.BuildingBlocks.Application;

/// <summary>
/// F35-T002 (caça-bugs backend F2): rate limit estourado — HTTP 429
/// Too Many Requests com Retry-After, nunca 409 (que era o casamento
/// errado de InvalidOperationException no GlobalExceptionHandler).
/// </summary>
public sealed class RateLimitExceededException(
    string message,
    TimeSpan? retryAfter = null) : Exception(message)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

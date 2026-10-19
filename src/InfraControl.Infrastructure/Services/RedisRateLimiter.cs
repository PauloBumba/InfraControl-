using InfraControl.Application.Common.Interfaces;
using StackExchange.Redis;

namespace InfraControl.Infrastructure.Services;

public sealed class RedisRateLimiter(IConnectionMultiplexer redis) : IRateLimiter
{
    private static readonly IReadOnlyDictionary<RateLimitCategory, (int Limit, int TtlSeconds)> Limits =
        new Dictionary<RateLimitCategory, (int, int)>
        {
            [RateLimitCategory.Default] = (30, 60),
            [RateLimitCategory.Login] = (5, 60),
            [RateLimitCategory.PasswordReset] = (3, 3600),
            [RateLimitCategory.Webhook] = (120, 60),
            [RateLimitCategory.Ingestion] = (1000, 60)
        };

    private const string IncrementScript = "local count = redis.call('INCR', KEYS[1]); if count == 1 then redis.call('EXPIRE', KEYS[1], ARGV[1]); end; return count;";

    public async Task<(bool Acquired, TimeSpan? RetryAfter)> TryAcquireAsync(
        string key,
        RateLimitCategory category,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (limit, ttlSeconds) = Limits[category];
        var redisKey = $"infracontrol:ratelimit:{category}:{key}";
        var current = (long)await redis.GetDatabase().ScriptEvaluateAsync(
            IncrementScript,
            new[] { new RedisKey(redisKey) },
            new[] { (RedisValue)ttlSeconds.ToString() });
        // F35-T002: janela restante para Retry-After quando rejeitado.
        return current <= limit
            ? (true, null)
            : (false, TimeSpan.FromSeconds(ttlSeconds));
    }
}

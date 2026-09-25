using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading.RateLimiting;
using InfraControl.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace InfraControl.Api.RateLimiting;

/// <summary>
/// ADR-011 / docs/08 — camada HTTP do rate limiting integrada ao ASP.NET Core
/// Rate Limiter (RateLimiterOptions.GlobalLimiter). Estado distribuído no
/// Redis (INCR + EXPIRE por categoria e partição: usuário autenticado ou IP);
/// com o Redis indisponível, a janela cai para memória local sem lançar
/// exceção. A camada MediatR (RateLimitingBehavior + RedisRateLimiter)
/// permanece como segunda barreira nas operações de aplicação.
/// </summary>
public sealed class DistributedHttpRateLimiter : PartitionedRateLimiter<HttpContext>
{
    private const string IncrementScript =
        "local count = redis.call('INCR', KEYS[1]); if count == 1 then redis.call('EXPIRE', KEYS[1], ARGV[1]); end; return count;";

    private readonly IConnectionMultiplexer _redis;
    private readonly HttpRateLimitOptions _options;
    private readonly ILogger<DistributedHttpRateLimiter> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, MemoryWindow> _fallback = new();

    public DistributedHttpRateLimiter(
        IConnectionMultiplexer redis,
        IOptions<HttpRateLimitOptions> options,
        ILogger<DistributedHttpRateLimiter> logger,
        TimeProvider? timeProvider = null)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(
        HttpContext resource,
        int permitCount,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.Enabled)
            return ValueTask.FromResult<RateLimitLease>(new WindowLease(true));

        var category = resource.GetEndpoint()
            ?.Metadata.GetMetadata<HttpRateLimitAttribute>()?.Category
            ?? RateLimitCategory.Default;
        var rule = _options.GetRule(category);
        var partition = GetPartition(resource);
        var key = $"infracontrol:ratelimit:http:{category}:{partition}";

        return new ValueTask<RateLimitLease>(
            AcquireFromRedisAsync(key, rule, partition, cancellationToken));
    }

    protected override RateLimitLease AttemptAcquireCore(HttpContext resource, int permitCount)
    {
        if (!_options.Enabled)
            return new WindowLease(true);

        var category = resource.GetEndpoint()
            ?.Metadata.GetMetadata<HttpRateLimitAttribute>()?.Category
            ?? RateLimitCategory.Default;
        var rule = _options.GetRule(category);
        var partition = GetPartition(resource);
        var key = $"infracontrol:ratelimit:http:{category}:{partition}";

        try
        {
            var current = (long)_redis.GetDatabase().ScriptEvaluate(
                IncrementScript,
                [new RedisKey(key)],
                [(RedisValue)rule.WindowSeconds.ToString()]);
            return CreateLease(current, rule);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Redis indisponível no rate limit HTTP; usando janela em memória. Partição: {RateLimitPartition}",
                partition);
            return AcquireFromMemory(key, rule);
        }
    }

    private async Task<RateLimitLease> AcquireFromRedisAsync(
        string key,
        HttpRateLimitRule rule,
        string partition,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = (long)await _redis.GetDatabase().ScriptEvaluateAsync(
                IncrementScript,
                [new RedisKey(key)],
                [(RedisValue)rule.WindowSeconds.ToString()]);

            return CreateLease(current, rule);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Redis indisponível no rate limit HTTP; usando janela em memória. Partição: {RateLimitPartition}",
                partition);
            return AcquireFromMemory(key, rule);
        }
    }

    public override RateLimiterStatistics? GetStatistics(HttpContext partition) => null;

    private static WindowLease CreateLease(long current, HttpRateLimitRule rule)
        => current <= rule.PermitLimit
            ? new WindowLease(true)
            : new WindowLease(false, TimeSpan.FromSeconds(rule.WindowSeconds));

    private RateLimitLease AcquireFromMemory(string key, HttpRateLimitRule rule)
    {
        var window = TimeSpan.FromSeconds(rule.WindowSeconds);
        var now = _timeProvider.GetUtcNow();
        var state = _fallback.GetOrAdd(key, _ => new MemoryWindow(now));

        lock (state)
        {
            if (now - state.WindowStart >= window)
            {
                state.WindowStart = now;
                state.Count = 0;
            }

            state.Count++;
            return state.Count <= rule.PermitLimit
                ? new WindowLease(true)
                : new WindowLease(false, window);
        }
    }

    private static string GetPartition(HttpContext context)
    {
        var subject = context.User?.FindFirst("sub")?.Value
            ?? context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(subject))
            return $"user:{subject}";

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ip}";
    }

    private sealed class MemoryWindow(DateTimeOffset windowStart)
    {
        public DateTimeOffset WindowStart { get; set; } = windowStart;
        public long Count { get; set; }
    }

    private sealed class WindowLease : RateLimitLease
    {
        private readonly IReadOnlyDictionary<string, object?> _metadata;

        public WindowLease(bool isAcquired, TimeSpan? retryAfter = null)
        {
            IsAcquired = isAcquired;
            _metadata = retryAfter is { } retry
                ? new Dictionary<string, object?> { [MetadataName.RetryAfter.Name] = retry }
                : new Dictionary<string, object?>();
        }

        public override bool IsAcquired { get; }
        public override IEnumerable<string> MetadataNames => _metadata.Keys;

        public override bool TryGetMetadata(string metadataName, out object? metadata)
            => _metadata.TryGetValue(metadataName, out metadata);
    }
}

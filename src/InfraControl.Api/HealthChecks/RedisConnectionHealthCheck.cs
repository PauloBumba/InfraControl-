using StackExchange.Redis;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InfraControl.Api.HealthChecks;

public sealed class RedisConnectionHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy("Redis primary is reachable.");
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy("Redis primary is unavailable.", exception);
        }
    }
}

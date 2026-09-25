using System.Text.Json;
using InfraControl.Application.Common.Interfaces;
using StackExchange.Redis;

namespace InfraControl.Infrastructure.Caching;

public class RedisCacheService : ICacheService, IIdempotencyStore, IRedisCacheStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;

    public RedisCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _database = redis.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = await _database.StringGetAsync(key);
        if (value.IsNullOrEmpty)
            return default;

        return JsonSerializer.Deserialize<T>(value.ToString());
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value);

        if (expiry.HasValue)
        {
            await _database.StringSetAsync(key, json, expiry.Value);
        }
        else
        {
            await _database.StringSetAsync(key, json);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _database.KeyDeleteAsync(key);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _database.KeyExistsAsync(key);
    }

    public async Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        return await _database.StringSetAsync(key, "1", expiry, When.NotExists);
    }
}

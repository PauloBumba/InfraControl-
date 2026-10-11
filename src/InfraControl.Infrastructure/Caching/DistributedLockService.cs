using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace InfraControl.Infrastructure.Caching;

/// <summary>
/// Serviço de lock distribuído usando Redis (ADR-044).
/// Previne execução duplicada de workers em múltiplas instâncias.
/// </summary>
public interface IDistributedLockService
{
    /// <summary>
    /// Tenta adquirir um lock distribuído.
    /// </summary>
    /// <param name="key">Chave única do lock (ex: "worker:cleanup").</param>
    /// <param name="expiry">Tempo de expiração do lock.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Disposable que libera o lock ao ser disposed, ou null se não conseguiu adquirir.</returns>
    Task<IDisposable?> TryAcquireLockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementação de lock distribuído usando Redis SET NX (SET if Not eXists).
/// </summary>
public class DistributedLockService : IDistributedLockService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<DistributedLockService> _logger;

    public DistributedLockService(
        IConnectionMultiplexer redis,
        ILogger<DistributedLockService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<IDisposable?> TryAcquireLockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var lockKey = $"lock:{key}";
        var lockValue = Guid.NewGuid().ToString();

        // SET NX (SET if Not eXists) com expiração
        var acquired = await db.StringSetAsync(lockKey, lockValue, expiry, When.NotExists);

        if (!acquired)
        {
            _logger.LogDebug("Lock não adquirido para {Key} (já está em uso)", key);
            return null;
        }

        _logger.LogDebug("Lock adquirido para {Key} com expiração de {Expiry}s", key, expiry.TotalSeconds);

        // Retorna disposable que libera o lock
        return new RedisLock(db, lockKey, lockValue, _logger);
    }

    private sealed class RedisLock : IDisposable
    {
        private readonly IDatabase _db;
        private readonly string _key;
        private readonly string _value;
        private readonly ILogger _logger;
        private bool _disposed;

        public RedisLock(IDatabase db, string key, string value, ILogger logger)
        {
            _db = db;
            _key = key;
            _value = value;
            _logger = logger;
        }

        public void Dispose()
        {
            if (_disposed) return;

            // Libera o lock apenas se ainda pertence a este valor (Lua script para atomicidade)
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            _db.ScriptEvaluate(script, new RedisKey[] { _key }, new RedisValue[] { _value });

            _logger.LogDebug("Lock liberado para {Key}", _key);
            _disposed = true;
        }
    }
}

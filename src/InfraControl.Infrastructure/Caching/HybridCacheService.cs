using System.Text.Json;
using InfraControl.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Caching;

/// <summary>
/// ADR-010 / docs/08 — cache híbrido: Redis é o cache distribuído primário;
/// em falha do Redis ou miss, o fallback em memória cobre a leitura e a
/// gravação sem propagar exceção. A ausência em ambos os níveis vira miss
/// para o handler, que consulta o PostgreSQL (fonte da verdade).
/// </summary>
public sealed class HybridCacheService(
    IRedisCacheStore redis,
    MemoryFallbackStore fallback,
    ILogger<HybridCacheService> logger) : ICacheService
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await redis.GetAsync<T>(key, cancellationToken);
            if (value is not null)
                return value;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Redis indisponível para leitura de cache; usando fallback em memória. Chave: {CacheKey}",
                key);
        }

        var json = fallback.Get(key);
        return json is null ? default : JsonSerializer.Deserialize<T>(json);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        // Grava sempre no fallback para que a leitura sobreviva a uma falha
        // subsequente do Redis dentro do mesmo TTL.
        fallback.Set(key, JsonSerializer.Serialize(value), expiry);

        try
        {
            await redis.SetAsync(key, value, expiry, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Redis indisponível para gravação de cache; valor mantido no fallback em memória. Chave: {CacheKey}",
                key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        fallback.Remove(key);

        try
        {
            await redis.RemoveAsync(key, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Redis indisponível para remoção de cache; entrada já removida do fallback. Chave: {CacheKey}",
                key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await redis.ExistsAsync(key, cancellationToken))
                return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Redis indisponível para verificação de cache; usando fallback em memória. Chave: {CacheKey}",
                key);
        }

        return fallback.Exists(key);
    }

    public async Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        try
        {
            return await redis.TryAcquireAsync(key, expiry, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Redis indisponível para aquisição; usando janela em memória (single-instance). Chave: {CacheKey}",
                key);
            return fallback.TryAcquire(key, expiry);
        }
    }
}

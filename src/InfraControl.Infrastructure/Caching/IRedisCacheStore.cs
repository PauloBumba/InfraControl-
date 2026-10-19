namespace InfraControl.Infrastructure.Caching;

/// <summary>
/// Porta estreita do Redis usada pelo cache híbrido (ADR-010): isola o
/// backend distribuído para que o fallback em memória possa ser exercitado
/// em teste sem um servidor Redis real.
/// </summary>
public interface IRedisCacheStore
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);

    Task TagAsync(string tag, string key, TimeSpan? expiry, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<string>> GetTagMembersAsync(string tag, CancellationToken cancellationToken = default);
    Task RemoveTagAsync(string tag, CancellationToken cancellationToken = default);
}

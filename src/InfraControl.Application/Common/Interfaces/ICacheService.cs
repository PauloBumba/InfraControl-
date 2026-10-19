namespace InfraControl.Application.Common.Interfaces;

public interface ICacheService : InfraControl.BuildingBlocks.Infrastructure.ICacheService
{
    Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexa <paramref name="key"/> sob a tag de invalidação
    /// (CacheKeyBuilder.TagIndexKey) — chamado pelo CachingBehavior após o
    /// Set quando a query declara CacheTags.
    /// </summary>
    Task TagAsync(string tag, string key, TimeSpan? duration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove todas as chaves indexadas sob a tag e o próprio índice — o que
    /// CacheInvalidationBehavior chama com as tags de CacheKeysToInvalidate.
    /// </summary>
    Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default);
}

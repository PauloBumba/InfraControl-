namespace InfraControl.BuildingBlocks.Application;

/// <summary>
/// Base para queries cacheáveis. Queries que não devem ser cacheadas podem
/// continuar implementando apenas IQuery&lt;TResponse&gt;.
/// </summary>
public abstract record BaseQuery<TResponse> : IQuery<TResponse>, ICacheableQuery
{
    public virtual string CacheKey => GetType().Name;
    public virtual TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}

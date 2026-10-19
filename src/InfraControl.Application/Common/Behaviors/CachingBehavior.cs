using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Common.Behaviors;

public sealed class CachingBehavior<TRequest, TResponse>(
    ICacheService cache,
    ICurrentUserContext currentUser,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IQuery<TResponse> || request is not ICacheableQuery cacheable)
            return await next();

        // Chave padronizada pelo BuildingBlocks: usuário + roles + hash dos
        // parâmetros — evita colisão de cache entre usuários e filtros distintos.
        var key = $"infracontrol:{CacheKeyBuilder.Build(cacheable.CacheKey, request, currentUser)}";
        var cached = await cache.GetAsync<TResponse>(key, cancellationToken);
        if (cached is not null)
        {
            logger.LogDebug("Cache hit for {CacheKey}", key);
            return cached;
        }

        var response = await next();
        await cache.SetAsync(key, response, cacheable.CacheDuration ?? TimeSpan.FromMinutes(5), cancellationToken);
        foreach (var tag in cacheable.CacheTags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
        {
            await cache.TagAsync(
                tag,
                key,
                cacheable.CacheDuration ?? TimeSpan.FromMinutes(5),
                cancellationToken);
        }
        logger.LogDebug("Cached response for {CacheKey}", key);
        return response;
    }
}

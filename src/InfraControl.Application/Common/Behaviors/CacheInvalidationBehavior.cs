using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Common.Behaviors;

public sealed class CacheInvalidationBehavior<TRequest, TResponse>(
    ICacheService cache,
    ILogger<CacheInvalidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();
        // ICacheInvalidatingCommand (BuildingBlocks) é a interface que todo
        // BaseCommand já implementa — ICacheInvalidatingRequest é só um
        // marcador derivado que quase nenhum command declara. Checar a base
        // é o que faz a invalidação por tag funcionar de verdade.
        if (request is not ICacheInvalidatingCommand invalidatingRequest)
            return response;

        // F1: os valores de CacheKeysToInvalidate são TAGS (não chaves
        // brutas) — a chave gravada pelo CachingBehavior embute usuário,
        // roles e hash de params e nunca casaria com um valor declarado do
        // lado da mutation. A tag (tagidx:{tag}) é o índice que resolve
        // todas as variantes; ver docs/08 e CacheKeyBuilder.TagIndexKey.
        foreach (var tag in invalidatingRequest.CacheKeysToInvalidate.Where(tag => !string.IsNullOrWhiteSpace(tag)))
        {
            await cache.InvalidateTagAsync(tag, cancellationToken);
            logger.LogDebug("Cache tag {CacheTag} invalidated after {RequestName}.", tag, typeof(TRequest).Name);
        }

        return response;
    }
}

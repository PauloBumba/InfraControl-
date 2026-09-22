using InfraControl.Application.Common.Interfaces;
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
        if (request is not ICacheInvalidatingRequest invalidatingRequest)
            return response;

        foreach (var key in invalidatingRequest.CacheKeysToInvalidate.Where(key => !string.IsNullOrWhiteSpace(key)))
        {
            await cache.RemoveAsync(key, cancellationToken);
            logger.LogDebug("Cache key {CacheKey} invalidated after {RequestName}.", key, typeof(TRequest).Name);
        }

        return response;
    }
}

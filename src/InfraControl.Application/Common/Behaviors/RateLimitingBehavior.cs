using InfraControl.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Common.Behaviors;

public sealed class RateLimitingBehavior<TRequest, TResponse>(
    IRateLimiter limiter,
    ILogger<RateLimitingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IRateLimitRequest rateLimited)
            return await next();

        var key = rateLimited.RateLimitKey ?? typeof(TRequest).Name;
        if (!await limiter.TryAcquireAsync(key, rateLimited.Category, cancellationToken))
        {
            logger.LogWarning("Rate limit exceeded for {RequestName} with key {RateLimitKey}", typeof(TRequest).Name, key);
            throw new InvalidOperationException("Limite de requisições excedido.");
        }

        return await next();
    }
}

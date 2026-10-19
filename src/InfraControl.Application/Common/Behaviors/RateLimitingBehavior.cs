using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Application;
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
        var (acquired, retryAfter) = await limiter.TryAcquireAsync(key, rateLimited.Category, cancellationToken);
        if (!acquired)
        {
            // F35-T002 (caça-bugs backend F2): 429 Too Many Requests com
            // Retry-After — nunca InvalidOperationException (que casava
            // como 409 no GlobalExceptionHandler).
            logger.LogWarning("Rate limit exceeded for {RequestName} with key {RateLimitKey}", typeof(TRequest).Name, key);
            throw new RateLimitExceededException("Limite de requisições excedido.", retryAfter);
        }

        return await next();
    }
}

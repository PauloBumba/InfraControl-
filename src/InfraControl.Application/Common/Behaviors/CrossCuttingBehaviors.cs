using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Common.Behaviors;

public sealed class CorrelationIdBehavior<TRequest, TResponse>(ILogger<CorrelationIdBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["Request"] = typeof(TRequest).Name
        });

        return await next();
    }
}

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName}.", requestName);
        try
        {
            var response = await next();
            logger.LogInformation("Handled {RequestName}.", requestName);
            return response;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed handling {RequestName}.", requestName);
            throw;
        }
    }
}

public sealed class PerformanceBehavior<TRequest, TResponse>(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await next();
        stopwatch.Stop();

        logger.LogInformation(
            "Request {RequestName} completed in {ElapsedMilliseconds} ms.",
            typeof(TRequest).Name,
            stopwatch.ElapsedMilliseconds);

        return response;
    }
}

public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IValidatableRequest validatable)
        {
            var errors = validatable.Validate();
            if (errors.Count > 0)
                throw new RequestValidationException(errors);
        }

        return await next();
    }
}

public sealed class IdempotencyBehavior<TRequest, TResponse>(IIdempotencyStore store)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IIdempotentRequest idempotentRequest)
            return await next();

        if (string.IsNullOrWhiteSpace(idempotentRequest.IdempotencyKey))
            throw new ArgumentException("An idempotency key is required.", nameof(idempotentRequest.IdempotencyKey));

        var duration = idempotentRequest.IdempotencyDuration ?? TimeSpan.FromMinutes(5);
        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(idempotentRequest.IdempotencyDuration));

        var acquired = await store.TryAcquireAsync(
            $"infracontrol:idempotency:{idempotentRequest.IdempotencyKey}",
            duration,
            cancellationToken);

        if (!acquired)
            throw new DuplicateRequestException(idempotentRequest.IdempotencyKey);

        return await next();
    }
}

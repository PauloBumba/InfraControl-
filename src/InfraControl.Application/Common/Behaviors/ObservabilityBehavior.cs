using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Common.Behaviors;

public sealed class ObservabilityBehavior<TRequest, TResponse>(
    ILogger<ObservabilityBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly ActivitySource ActivitySource = new("InfraControl.Application");

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity(typeof(TRequest).Name, ActivityKind.Internal);
        activity?.SetTag("infracontrol.request", typeof(TRequest).FullName);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await next();
            activity?.SetTag("infracontrol.outcome", "success");
            return response;
        }
        catch (Exception exception)
        {
            activity?.SetTag("infracontrol.outcome", "failure");
            activity?.SetTag("error.type", exception.GetType().FullName);
            activity?.SetTag("error.message", exception.Message);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            activity?.SetTag("infracontrol.duration_ms", stopwatch.Elapsed.TotalMilliseconds);
            logger.LogDebug("{RequestName} completed in {ElapsedMs} ms", typeof(TRequest).Name, stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}

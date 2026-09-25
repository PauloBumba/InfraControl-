using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using InfraControl.BuildingBlocks.Application.Response;

namespace InfraControl.Api.RateLimiting;

/// <summary>
/// ADR-011 / docs/08 — wiring da camada HTTP do rate limiting, extraído do
/// Program.cs para ser exercitável por teste de pipeline com host real.
/// </summary>
public static class RateLimitingExtensions
{
    public static IServiceCollection AddHttpRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<HttpRateLimitOptions>(
            configuration.GetSection(HttpRateLimitOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                    response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

                // F0-T023: corpo de erro sempre no envelope ApiResponse.
                response.ContentType = "application/json";
                await response.WriteAsJsonAsync(
                    ApiResponse<object>.Fail(["Limite de requisições excedido."]),
                    cancellationToken);
            };
        });

        services.AddOptions<RateLimiterOptions>()
            .Configure<IConnectionMultiplexer, IOptions<HttpRateLimitOptions>, ILoggerFactory>(
                (options, redis, rateLimitOptions, loggerFactory) =>
                {
                    options.GlobalLimiter = new DistributedHttpRateLimiter(
                        redis,
                        rateLimitOptions,
                        loggerFactory.CreateLogger<DistributedHttpRateLimiter>());
                });

        return services;
    }
}

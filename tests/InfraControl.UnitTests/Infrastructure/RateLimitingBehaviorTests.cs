using FluentAssertions;
using InfraControl.Application.Common.Behaviors;
using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Infrastructure;

/// <summary>
/// F35-T002 (caça-bugs backend F2): rate limit estourado vira
/// RateLimitExceededException (→ HTTP 429 + Retry-After no
/// GlobalExceptionHandler), nunca InvalidOperationException (→ 409).
/// </summary>
public sealed class RateLimitingBehaviorTests
{
    private sealed record SampleRequest : IRateLimitRequest
    {
        public RateLimitCategory Category => RateLimitCategory.Login;
        public string? RateLimitKey => "login:test";
    }

    private sealed class FixedRateLimiter(bool acquired, TimeSpan? retryAfter = null) : IRateLimiter
    {
        public Task<(bool Acquired, TimeSpan? RetryAfter)> TryAcquireAsync(
            string key,
            RateLimitCategory category,
            CancellationToken cancellationToken = default)
            => Task.FromResult((acquired, retryAfter));
    }

    private static Task<object> Next() => Task.FromResult<object>(new object());

    [Fact]
    public async Task Handle_ShouldThrowRateLimitExceededException_WhenLimiterRejects()
    {
        var behavior = new RateLimitingBehavior<SampleRequest, object>(
            new FixedRateLimiter(acquired: false, retryAfter: TimeSpan.FromSeconds(60)),
            NullLogger<RateLimitingBehavior<SampleRequest, object>>.Instance);

        var action = () => behavior.Handle(new SampleRequest(), Next, CancellationToken.None);

        var ex = await action.Should().ThrowAsync<RateLimitExceededException>();
        ex.Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(60), "a janela do limiter vira Retry-After do 429");
        ex.Which.Message.Should().Be("Limite de requisições excedido.");
    }

    [Fact]
    public async Task Handle_ShouldPassThrough_WhenRequestIsNotRateLimited()
    {
        var behavior = new RateLimitingBehavior<object, object>(
            new FixedRateLimiter(acquired: false),
            NullLogger<RateLimitingBehavior<object, object>>.Instance);

        var result = await behavior.Handle(new object(), Next, CancellationToken.None);

        result.Should().NotBeNull("sem IRateLimitRequest o pipeline não deve bloquear");
    }

    [Fact]
    public async Task Handle_ShouldCallNext_WhenLimiterAllows()
    {
        var behavior = new RateLimitingBehavior<SampleRequest, object>(
            new FixedRateLimiter(acquired: true),
            NullLogger<RateLimitingBehavior<SampleRequest, object>>.Instance);

        var result = await behavior.Handle(new SampleRequest(), Next, CancellationToken.None);

        result.Should().NotBeNull();
    }
}

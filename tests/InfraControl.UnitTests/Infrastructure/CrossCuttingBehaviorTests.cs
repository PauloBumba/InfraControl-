using FluentAssertions;
using InfraControl.Application.Common.Behaviors;
using InfraControl.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Infrastructure;

public sealed class CrossCuttingBehaviorTests
{
    [Fact]
    public async Task IdempotencyBehavior_ShouldExecuteOnlyWhenKeyIsAcquired()
    {
        var store = new FakeIdempotencyStore(acquired: false);
        var behavior = new IdempotencyBehavior<TestRequest, string>(store);
        var executed = false;

        var action = () => behavior.Handle(
            new TestRequest("same-key"),
            () =>
            {
                executed = true;
                return Task.FromResult("handled");
            },
            CancellationToken.None);

        await action.Should().ThrowAsync<DuplicateRequestException>();
        executed.Should().BeFalse();
    }

    [Fact]
    public async Task IdempotencyBehavior_ShouldExecuteAfterAcquiringKey()
    {
        var store = new FakeIdempotencyStore(acquired: true);
        var behavior = new IdempotencyBehavior<TestRequest, string>(store);

        var result = await behavior.Handle(
            new TestRequest("new-key"),
            () => Task.FromResult("handled"),
            CancellationToken.None);

        result.Should().Be("handled");
        store.LastKey.Should().Be("infracontrol:idempotency:new-key");
    }

    [Fact]
    public async Task ValidationBehavior_ShouldRejectInvalidRequestBeforeHandler()
    {
        var behavior = new ValidationBehavior<InvalidRequest, string>();
        var executed = false;

        var action = () => behavior.Handle(
            new InvalidRequest(),
            () =>
            {
                executed = true;
                return Task.FromResult("handled");
            },
            CancellationToken.None);

        await action.Should().ThrowAsync<RequestValidationException>();
        executed.Should().BeFalse();
    }

    private sealed record TestRequest(string IdempotencyKey) : IIdempotentRequest;

    private sealed record InvalidRequest : IValidatableRequest
    {
        public IReadOnlyCollection<string> Validate() => ["invalid"];
    }

    private sealed class FakeIdempotencyStore(bool acquired) : IIdempotencyStore
    {
        public string? LastKey { get; private set; }

        public Task<bool> TryAcquireAsync(string key, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            LastKey = key;
            return Task.FromResult(acquired);
        }
    }
}

using FluentAssertions;
using InfraControl.Application.Common.Behaviors;
using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Application;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Infrastructure;

public sealed class MissingBehaviorTests
{
    [Fact]
    public async Task AuthorizationBehavior_ShouldRejectMissingPermission()
    {
        var behavior = new AuthorizationBehavior<AuthorizedRequest, string>(
            new FakeCurrentUser(true, ["InventoryRead"]));

        var action = () => behavior.Handle(
            new AuthorizedRequest(["InventoryManage"]),
            () => Task.FromResult("handled"),
            CancellationToken.None);

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CachingBehavior_ShouldReturnCachedQueryWithoutHandler()
    {
        var cache = new FakeCache("cached");
        var behavior = new CachingBehavior<CachedQuery, string>(
            cache,
            NullLogger<CachingBehavior<CachedQuery, string>>.Instance);
        var executed = false;

        var result = await behavior.Handle(
            new CachedQuery(),
            () =>
            {
                executed = true;
                return Task.FromResult("handler");
            },
            CancellationToken.None);

        result.Should().Be("cached");
        executed.Should().BeFalse();
    }

    [Fact]
    public async Task TransactionBehavior_ShouldCommitCommandsOnly()
    {
        var unitOfWork = new FakeUnitOfWork();
        var behavior = new TransactionBehavior<CommandRequest, string>(
            unitOfWork,
            NullLogger<TransactionBehavior<CommandRequest, string>>.Instance);

        await behavior.Handle(new CommandRequest(), () => Task.FromResult("ok"), CancellationToken.None);

        unitOfWork.SaveCalls.Should().Be(1);
    }

    private sealed record AuthorizedRequest(IReadOnlyCollection<string> RequiredPermissions) : IAuthorizedRequest;
    private sealed record CachedQuery : IQuery<string>, ICacheableQuery
    {
        public string CacheKey => "unit-test";
        public TimeSpan? CacheDuration => TimeSpan.FromMinutes(1);
    }
    private sealed record CommandRequest : ICommand<string>;

    private sealed class FakeCurrentUser(bool authenticated, IReadOnlyCollection<string> permissions) : ICurrentUserContext
    {
        public Guid? UserId => Guid.NewGuid();
        public string? UserName => "test@example.com";
        public bool IsAuthenticated => authenticated;
        public IReadOnlyCollection<string> Roles => [];
        public IReadOnlyCollection<string> Permissions => permissions;
    }

    private sealed class FakeCache(string? value) : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(value is T typed ? typed : default);
        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(value is not null);
        public Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
        public void Dispose() { }
    }
}

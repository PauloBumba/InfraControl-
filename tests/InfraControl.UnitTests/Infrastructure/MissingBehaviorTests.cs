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
            new FakeCurrentUser(true));

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
            new FakeCurrentUser(true),
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
    public async Task CachingBehavior_ShouldIsolateCacheEntriesByUser()
    {
        var cache = new RecordingCache();
        var query = new CachedQuery();
        var behaviorForUserA = new CachingBehavior<CachedQuery, string>(
            cache,
            new FakeCurrentUser(true),
            NullLogger<CachingBehavior<CachedQuery, string>>.Instance);
        var behaviorForUserB = new CachingBehavior<CachedQuery, string>(
            cache,
            new FakeCurrentUser(true),
            NullLogger<CachingBehavior<CachedQuery, string>>.Instance);

        var first = await behaviorForUserA.Handle(
            query, () => Task.FromResult("from-user-a"), CancellationToken.None);

        var secondForUserA = await behaviorForUserA.Handle(
            query,
            () => Task.FromException<string>(
                new InvalidOperationException("o mesmo usuário deve acertar o cache")),
            CancellationToken.None);

        var firstForUserB = await behaviorForUserB.Handle(
            query, () => Task.FromResult("from-user-b"), CancellationToken.None);

        first.Should().Be("from-user-a");
        secondForUserA.Should().Be("from-user-a",
            "a chave de cache deve ser estável para o mesmo usuário e parâmetros");
        firstForUserB.Should().Be("from-user-b",
            "usuários diferentes não podem herdar o cache um do outro (CacheKeyBuilder)");
        cache.Store.Keys.Should().HaveCount(2);
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

    private sealed record AuthorizedRequest(IReadOnlyCollection<string> RequiredRoles) : IAuthorizedRequest;
    private sealed record CachedQuery : IQuery<string>, ICacheableQuery
    {
        public string CacheKey => "unit-test";
        public TimeSpan? CacheDuration => TimeSpan.FromMinutes(1);
    }
    private sealed record CommandRequest : ICommand<string>;

    private sealed class FakeCurrentUser(bool authenticated) : ICurrentUserContext
    {
        private readonly Guid? _userId = Guid.NewGuid();
        public Guid? UserId => _userId;
        public string? UserName => "test@example.com";
        public bool IsAuthenticated => authenticated;
        public IReadOnlyCollection<string> Roles => [];
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

    private sealed class RecordingCache : ICacheService
    {
        public Dictionary<string, object?> Store { get; } = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.TryGetValue(key, out var value) && value is T typed ? typed : default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            Store[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            Store.Remove(key);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.ContainsKey(key));

        public Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
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

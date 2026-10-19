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
    public async Task CachingBehavior_ShouldRegisterDeclaredTagsOnCache()
    {
        var cache = new RecordingCache();
        var behavior = new CachingBehavior<TaggedQuery, string>(
            cache,
            new FakeCurrentUser(true),
            NullLogger<CachingBehavior<TaggedQuery, string>>.Instance);

        await behavior.Handle(
            new TaggedQuery("sw-1"), () => Task.FromResult("portas"), CancellationToken.None);

        cache.Tags.Should().ContainKey("switchPorts:sw-1");
        cache.Tags["switchPorts:sw-1"].Should().ContainSingle();
    }

    [Fact]
    public async Task CacheInvalidationBehavior_ShouldInvalidateDeclaredTagsAndForceRequery()
    {
        // F1: fim a fim — a query cacheada sob a tag é invalidada pelo
        // comando e a leitura seguinte executa o handler de novo.
        var cache = new RecordingCache();
        var user = new FakeCurrentUser(true);
        var queryBehavior = new CachingBehavior<TaggedQuery, string>(
            cache, user, NullLogger<CachingBehavior<TaggedQuery, string>>.Instance);
        var invalidationBehavior = new CacheInvalidationBehavior<InvalidateSwitchPortsCommand, string>(
            cache, NullLogger<CacheInvalidationBehavior<InvalidateSwitchPortsCommand, string>>.Instance);
        var handlerRuns = 0;

        Task<string> Handler()
        {
            handlerRuns++;
            return Task.FromResult($"portas-{handlerRuns}");
        }

        var first = await queryBehavior.Handle(new TaggedQuery("sw-1"), Handler, CancellationToken.None);
        var cached = await queryBehavior.Handle(new TaggedQuery("sw-1"), Handler, CancellationToken.None);

        first.Should().Be("portas-1");
        cached.Should().Be("portas-1", "sem invalidação a query deve servir o cache");
        handlerRuns.Should().Be(1);

        await invalidationBehavior.Handle(
            new InvalidateSwitchPortsCommand("sw-1"), () => Task.FromResult("ok"), CancellationToken.None);

        cache.Tags.Should().NotContainKey("switchPorts:sw-1",
            "a invalidação deve remover o índice de tag");

        var afterInvalidation = await queryBehavior.Handle(new TaggedQuery("sw-1"), Handler, CancellationToken.None);

        afterInvalidation.Should().Be("portas-2",
            "após invalidar a tag a query não pode servir a entrada antiga");
        handlerRuns.Should().Be(2);
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
        public IReadOnlyCollection<string> CacheTags => [];
    }

    private sealed record TaggedQuery(string Id) : IQuery<string>, ICacheableQuery
    {
        public string CacheKey => $"switchPorts:{Id}";
        public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
        public IReadOnlyCollection<string> CacheTags => [$"switchPorts:{Id}"];
    }

    private sealed record InvalidateSwitchPortsCommand(string Id) : ICacheInvalidatingRequest
    {
        public IReadOnlyCollection<string> CacheKeysToInvalidate => [$"switchPorts:{Id}"];
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
        public Task TagAsync(string tag, string key, TimeSpan? duration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default) => Task.CompletedTask;
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

        public Dictionary<string, HashSet<string>> Tags { get; } = new();

        public Task TagAsync(string tag, string key, TimeSpan? duration, CancellationToken cancellationToken = default)
        {
            if (!Tags.TryGetValue(tag, out var members))
                Tags[tag] = members = [];
            members.Add(key);
            return Task.CompletedTask;
        }

        public Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            if (Tags.TryGetValue(tag, out var members))
            {
                foreach (var key in members)
                    Store.Remove(key);
                Tags.Remove(tag);
            }
            return Task.CompletedTask;
        }
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

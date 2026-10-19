using FluentAssertions;
using InfraControl.Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace InfraControl.UnitTests.Infrastructure;

/// <summary>
/// ADR-010 / docs/08 — cache híbrido: Redis primário, fallback em memória,
/// falha do Redis nunca sobe para o caller.
/// </summary>
public sealed class HybridCacheServiceTests
{
    [Fact]
    public async Task GetAsync_WithRedisAvailable_UsesRedis()
    {
        var redis = new FakeRedisStore();
        var service = CreateService(redis, out _);

        await service.SetAsync("q:rota", "valor", TimeSpan.FromMinutes(1));
        var result = await service.GetAsync<string>("q:rota");

        result.Should().Be("valor");
        redis.Calls.Should().Contain("Set");
        redis.Calls.Should().Contain("Get");
    }

    [Fact]
    public async Task SetAndGet_WhenRedisIsDown_DegradeToMemoryWithoutThrowing()
    {
        var redis = new FakeRedisStore { Down = true };
        var service = CreateService(redis, out var logger);

        var setAction = () => service.SetAsync("q:rota", "valor", TimeSpan.FromMinutes(1));
        await setAction.Should().NotThrowAsync("a indisponibilidade do Redis não pode quebrar a gravação");

        var result = await service.GetAsync<string>("q:rota");

        result.Should().Be("valor", "o fallback em memória deve atender a leitura");
        logger.Warnings.Should().NotBeEmpty("a degradação deve ficar visível no log");
    }

    [Fact]
    public async Task GetAsync_WhenRedisMissesAfterFlush_ReadsFromFallback()
    {
        var redis = new FakeRedisStore();
        var service = CreateService(redis, out _);

        await service.SetAsync("q:rota", "valor", TimeSpan.FromMinutes(1));
        redis.Store.Clear(); // simula flush/reinício do Redis mantendo TTL da memória

        var result = await service.GetAsync<string>("q:rota");

        result.Should().Be("valor", "Redis → Fallback → (miss) → PostgreSQL, conforme docs/08");
    }

    [Fact]
    public async Task RemoveAsync_WhenRedisIsDown_DoesNotThrowAndClearsFallback()
    {
        var redis = new FakeRedisStore { Down = true };
        var service = CreateService(redis, out _);
        await service.SetAsync("q:rota", "valor", TimeSpan.FromMinutes(1));

        var removeAction = () => service.RemoveAsync("q:rota");
        await removeAction.Should().NotThrowAsync();

        var result = await service.GetAsync<string>("q:rota");
        result.Should().BeNull("a remoção deve valer também para o fallback");
    }

    [Fact]
    public async Task ExistsAsync_WhenRedisIsDown_FallsBackToMemory()
    {
        var redis = new FakeRedisStore { Down = true };
        var service = CreateService(redis, out _);
        await service.SetAsync("q:rota", "valor", TimeSpan.FromMinutes(1));

        var exists = await service.ExistsAsync("q:rota");

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenRedisIsDown_EnforcesWindowInMemory()
    {
        var redis = new FakeRedisStore { Down = true };
        var service = CreateService(redis, out var logger);

        var first = await service.TryAcquireAsync("inbox:evt-1", TimeSpan.FromMinutes(1));
        var second = await service.TryAcquireAsync("inbox:evt-1", TimeSpan.FromMinutes(1));

        first.Should().BeTrue("a primeira aquisição deve ser concedida");
        second.Should().BeFalse("a segunda na mesma janela deve ser negada");
        logger.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAsync_WhenRedisIsDownAndNothingCached_ReturnsDefaultWithoutThrowing()
    {
        var redis = new FakeRedisStore { Down = true };
        var service = CreateService(redis, out _);

        var result = await service.GetAsync<string>("q:ausente");

        result.Should().BeNull();
    }

    [Fact]
    public async Task InvalidateTagAsync_RemovesMemberKeysFromRedisAndFallback()
    {
        var redis = new FakeRedisStore();
        var service = CreateService(redis, out _);
        await service.SetAsync("infracontrol:q:user-a", "valor-a", TimeSpan.FromMinutes(5));
        await service.SetAsync("infracontrol:q:user-b", "valor-b", TimeSpan.FromMinutes(5));
        await service.TagAsync("switchPorts:1", "infracontrol:q:user-a", TimeSpan.FromMinutes(5));
        await service.TagAsync("switchPorts:1", "infracontrol:q:user-b", TimeSpan.FromMinutes(5));

        await service.InvalidateTagAsync("switchPorts:1");

        (await service.GetAsync<string>("infracontrol:q:user-a")).Should().BeNull();
        (await service.GetAsync<string>("infracontrol:q:user-b")).Should().BeNull();
        redis.TagStore.Should().NotContainKey("switchPorts:1");
    }

    [Fact]
    public async Task InvalidateTagAsync_WhenRedisIsDown_InvalidatesFallbackOnlyWithoutThrowing()
    {
        var redis = new FakeRedisStore { Down = true };
        var service = CreateService(redis, out var logger);
        await service.SetAsync("infracontrol:q:user-a", "valor-a", TimeSpan.FromMinutes(5));
        await service.TagAsync("switchPorts:1", "infracontrol:q:user-a", TimeSpan.FromMinutes(5));

        var invalidate = () => service.InvalidateTagAsync("switchPorts:1");
        await invalidate.Should().NotThrowAsync();

        (await service.GetAsync<string>("infracontrol:q:user-a")).Should().BeNull();
        logger.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InvalidateTagAsync_WithKeysOnlyInFallback_RemovesThem()
    {
        // Redis caiu no Set (fallback guardou) e voltou sem o índice — a
        // união dos dois índices precisa cobrir o caso, senão fica órfão.
        var redis = new FakeRedisStore { Down = true };
        var service = CreateService(redis, out _);
        await service.SetAsync("infracontrol:q:user-a", "valor-a", TimeSpan.FromMinutes(5));
        await service.TagAsync("switchPorts:1", "infracontrol:q:user-a", TimeSpan.FromMinutes(5));
        redis.Down = false; // Redis volta sem índice de tag

        await service.InvalidateTagAsync("switchPorts:1");

        redis.Store.Should().NotContainKey("infracontrol:q:user-a");
        (await service.GetAsync<string>("infracontrol:q:user-a")).Should().BeNull();
    }

    private static HybridCacheService CreateService(
        FakeRedisStore redis, out ListLogger logger)
    {
        logger = new ListLogger();
        return new HybridCacheService(redis, new MemoryFallbackStore(TimeProvider.System), logger);
    }

    private sealed class FakeRedisStore : IRedisCacheStore
    {
        public Dictionary<string, object?> Store { get; } = new();
        public bool Down { get; set; }
        public List<string> Calls { get; } = [];

        private void Touch(string call)
        {
            Calls.Add(call);
            if (Down)
                throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, CommandFlags.None, "redis indisponível", null, CommandStatus.Unknown);
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            Touch("Get");
            return Task.FromResult(Store.TryGetValue(key, out var value) && value is T typed ? typed : default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        {
            Touch("Set");
            Store[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            Touch("Remove");
            Store.Remove(key);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            Touch("Exists");
            return Task.FromResult(Store.ContainsKey(key));
        }

        public Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            Touch("Acquire");
            if (Store.ContainsKey(key))
                return Task.FromResult(false);
            Store[key] = 1;
            return Task.FromResult(true);
        }

        public Dictionary<string, HashSet<string>> TagStore { get; } = new();

        public Task TagAsync(string tag, string key, TimeSpan? expiry, CancellationToken cancellationToken = default)
        {
            Touch("Tag");
            if (!TagStore.TryGetValue(tag, out var members))
                TagStore[tag] = members = [];
            members.Add(key);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<string>> GetTagMembersAsync(string tag, CancellationToken cancellationToken = default)
        {
            Touch("GetTagMembers");
            return Task.FromResult<IReadOnlyCollection<string>>(
                TagStore.TryGetValue(tag, out var members) ? members.ToArray() : []);
        }

        public Task RemoveTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            Touch("RemoveTag");
            TagStore.Remove(tag);
            return Task.CompletedTask;
        }
    }

    private sealed class ListLogger : ILogger<HybridCacheService>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
                Warnings.Add(formatter(state, exception));
        }
    }
}

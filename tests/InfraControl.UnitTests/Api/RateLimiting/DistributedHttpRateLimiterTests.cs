using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using FluentAssertions;
using InfraControl.Api.RateLimiting;
using InfraControl.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace InfraControl.UnitTests.Api.RateLimiting;

/// <summary>
/// ADR-011 / docs/08 — camada HTTP do rate limiting: ASP.NET Core Rate Limiter
/// com estado distribuído no Redis e fallback em memória.
/// </summary>
public sealed class DistributedHttpRateLimiterTests
{
    [Fact]
    public async Task AcquireAsync_AllowsUpToLimitAndThenDenies_ForDefaultCategory()
    {
        var (limiter, _) = CreateLimiter(rules: new()
        {
            [nameof(RateLimitCategory.Default)] = new HttpRateLimitRule(PermitLimit: 3, WindowSeconds: 60)
        });
        var context = CreateContext(userId: "user-1");

        var leases = new List<RateLimitLease>();
        for (var i = 0; i < 4; i++)
            leases.Add(await limiter.AcquireAsync(context, 1));

        leases.Take(3).Should().OnlyContain(lease => lease.IsAcquired);
        leases[3].IsAcquired.Should().BeFalse();
        leases[3].TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter)
            .Should().BeTrue("a negação deve informar Retry-After");
        retryAfter.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task AcquireAsync_UsesEndpointCategoryRule_WhenAttributeIsPresent()
    {
        var (limiter, _) = CreateLimiter(rules: new()
        {
            [nameof(RateLimitCategory.Default)] = new HttpRateLimitRule(PermitLimit: 10, WindowSeconds: 60),
            [nameof(RateLimitCategory.Login)] = new HttpRateLimitRule(PermitLimit: 2, WindowSeconds: 60)
        });
        var loginContext = CreateContext(userId: "user-1", category: RateLimitCategory.Login);

        var first = await limiter.AcquireAsync(loginContext, 1);
        var second = await limiter.AcquireAsync(loginContext, 1);
        var third = await limiter.AcquireAsync(loginContext, 1);

        first.IsAcquired.Should().BeTrue();
        second.IsAcquired.Should().BeTrue();
        third.IsAcquired.Should().BeFalse("a categoria Login tem limite próprio (2/60s)");
    }

    [Fact]
    public async Task AcquireAsync_PartitionsByUser_EvenOnSameIp()
    {
        var (limiter, _) = CreateLimiter(rules: new()
        {
            [nameof(RateLimitCategory.Default)] = new HttpRateLimitRule(PermitLimit: 1, WindowSeconds: 60)
        });
        var userA = CreateContext(userId: "user-a");
        var userB = CreateContext(userId: "user-b");

        var firstForA = await limiter.AcquireAsync(userA, 1);
        var secondForA = await limiter.AcquireAsync(userA, 1);
        var firstForB = await limiter.AcquireAsync(userB, 1);

        firstForA.IsAcquired.Should().BeTrue();
        secondForA.IsAcquired.Should().BeFalse("usuário A esgotou a própria cota");
        firstForB.IsAcquired.Should().BeTrue("usuário B tem partição própria");
    }

    [Fact]
    public async Task AcquireAsync_PartitionsAnonymousByIp()
    {
        var (limiter, _) = CreateLimiter(rules: new()
        {
            [nameof(RateLimitCategory.Default)] = new HttpRateLimitRule(PermitLimit: 1, WindowSeconds: 60)
        });
        var ip1 = CreateContext(userId: null, ip: "192.0.2.10");
        var ip2 = CreateContext(userId: null, ip: "192.0.2.11");

        var first = await limiter.AcquireAsync(ip1, 1);
        var second = await limiter.AcquireAsync(ip1, 1);
        var otherIp = await limiter.AcquireAsync(ip2, 1);

        first.IsAcquired.Should().BeTrue();
        second.IsAcquired.Should().BeFalse();
        otherIp.IsAcquired.Should().BeTrue("IPs diferentes não compartilham cota");
    }

    [Fact]
    public async Task AcquireAsync_WhenRedisIsDown_FallsBackToMemoryWindowWithoutThrowing()
    {
        var (limiter, _) = CreateLimiter(
            redisDown: true,
            rules: new()
            {
                [nameof(RateLimitCategory.Default)] = new HttpRateLimitRule(PermitLimit: 2, WindowSeconds: 60)
            });
        var context = CreateContext(userId: "user-1");

        var acquireAction = async () =>
        {
            var leases = new List<RateLimitLease>();
            for (var i = 0; i < 3; i++)
                leases.Add(await limiter.AcquireAsync(context, 1));
            return leases;
        };

        var leases = await acquireAction();
        leases.Should().HaveCount(3, "a falha do Redis não pode propagar exceção");
        leases.Take(2).Should().OnlyContain(lease => lease.IsAcquired);
        leases[2].IsAcquired.Should().BeFalse("o fallback em memória deve manter a janela");
    }

    [Fact]
    public async Task AcquireAsync_WhenDisabled_AlwaysAllows()
    {
        var (limiter, _) = CreateLimiter(enabled: false, redisDown: true, rules: new()
        {
            [nameof(RateLimitCategory.Default)] = new HttpRateLimitRule(PermitLimit: 1, WindowSeconds: 60)
        });
        var context = CreateContext(userId: "user-1");

        var first = await limiter.AcquireAsync(context, 1);
        var second = await limiter.AcquireAsync(context, 1);

        first.IsAcquired.Should().BeTrue();
        second.IsAcquired.Should().BeTrue("RateLimit:Enabled=false desativa a camada HTTP");
    }

    private static (DistributedHttpRateLimiter Limiter, Dictionary<string, long> Counters) CreateLimiter(
        bool enabled = true,
        bool redisDown = false,
        Dictionary<string, HttpRateLimitRule>? rules = null)
    {
        var counters = new Dictionary<string, long>();

        var database = new Mock<IDatabase>();
        database
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Returns((string _, RedisKey[] keys, RedisValue[] _, CommandFlags _) =>
            {
                if (redisDown)
                    throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, CommandFlags.None, "redis indisponível", null, CommandStatus.Unknown);

                var key = keys[0].ToString();
                counters.TryGetValue(key, out var current);
                counters[key] = ++current;
                return Task.FromResult(RedisResult.Create(current, ResultType.Integer));
            });

        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer
            .Setup(connection => connection.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);

        var options = new HttpRateLimitOptions { Enabled = enabled };
        if (rules is not null)
            options.Rules = rules;

        var limiter = new DistributedHttpRateLimiter(
            multiplexer.Object,
            Options.Create(options),
            NullLogger<DistributedHttpRateLimiter>.Instance);

        return (limiter, counters);
    }

    private static DefaultHttpContext CreateContext(
        string? userId,
        string ip = "192.0.2.10",
        RateLimitCategory? category = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);

        if (userId is not null)
        {
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim("sub", userId)], authenticationType: "test"));
        }

        if (category is not null)
        {
            context.SetEndpoint(new Endpoint(
                requestDelegate: null,
                new EndpointMetadataCollection(new HttpRateLimitAttribute(category.Value)),
                displayName: "test"));
        }

        return context;
    }
}

using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Integrations.Unifi.Wifi;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using Moq;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F18-T006: query do /wifi — cache hit devolve o DTO gravado sem sondar
/// o Controller, cache miss sonda e grava com TTL de 60s, captura o
/// instante da sondagem e leitura = qualquer papel autenticado
/// (RoleAccess.Read). Nada é persistido além do cache curto (regra 8).
/// </summary>
public sealed class GetUnifiWifiQueryTests
{
    [Fact]
    public async Task Handle_WhenCacheMiss_ShouldProbeControllerAndStoreWith60sTtl()
    {
        var ssids = new[]
        {
            new UnifiSsidDto("Casa-5G", "wpapsk", Enabled: true, Vlan: 40, Bands: "5 GHz", IsGuest: false),
        };
        var aps = new[]
        {
            new UnifiAccessPointDto(
                "f0:9f:c2:11:22:33", "AP-Sala", "U7PG2", "192.168.1.11",
                Connected: true, UptimeSeconds: 86400, CpuPercent: 12, MemPercent: 41,
                RxBytes: 9000000, TxBytes: 4500000, Clients: 6,
                Radios: [new UnifiRadioDto("5g", 44, 80, 23, 35)]),
        };

        var client = new Mock<IUnifiApiClient>();
        client.Setup(c => c.ListSsidsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ssids);
        client.Setup(c => c.ListAccessPointsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(aps);

        var cache = new FakeCacheService();
        var handler = new GetUnifiWifiQueryHandler(client.Object, cache);

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var result = await handler.Handle(
            new GetUnifiWifiQuery(), CancellationToken.None);
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        result.Ssids.Should().BeEquivalentTo(ssids);
        result.AccessPoints.Should().BeEquivalentTo(aps);
        (result.CapturedAtUtc >= before && result.CapturedAtUtc <= after)
            .Should().BeTrue("CapturedAtUtc é o instante da sondagem");

        client.Verify(c => c.ListSsidsAsync(It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.ListAccessPointsAsync(It.IsAny<CancellationToken>()), Times.Once);
        cache.LastSetKey.Should().Be(GetUnifiWifiQueryHandler.CacheKey);
        cache.LastSetExpiration.Should().Be(
            GetUnifiWifiQueryHandler.CacheTtl, "TTL curto documentado na task");
        GetUnifiWifiQueryHandler.CacheTtl.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task Handle_WhenCacheHit_ShouldReturnCachedAndSkipController()
    {
        var cached = new UnifiWifiDto(
            Array.Empty<UnifiSsidDto>(),
            Array.Empty<UnifiAccessPointDto>(),
            DateTimeOffset.UtcNow.AddMinutes(-1));

        var cache = new FakeCacheService();
        await cache.SetAsync(GetUnifiWifiQueryHandler.CacheKey, cached);

        var client = new Mock<IUnifiApiClient>();
        var handler = new GetUnifiWifiQueryHandler(client.Object, cache);

        var result = await handler.Handle(
            new GetUnifiWifiQuery(), CancellationToken.None);

        result.Should().BeSameAs(cached, "cache hit devolve o DTO gravado");
        client.Verify(c => c.ListSsidsAsync(It.IsAny<CancellationToken>()), Times.Never);
        client.Verify(c => c.ListAccessPointsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Query_ShouldAllowAnyAuthenticatedRoleToRead()
    {
        var query = new GetUnifiWifiQuery();

        query.Should().BeAssignableTo<IAuthorizedRequest>();
        // leitura de dashboard: qualquer papel autenticado (espelho do Insights)
        query.RequiredRoles.Should().BeEquivalentTo(RoleAccess.Read);
    }

    private sealed class FakeCacheService : ICacheService
    {
        private readonly Dictionary<string, object?> _store = new();

        public string? LastSetKey { get; private set; }

        public TimeSpan? LastSetExpiration { get; private set; }

        public Task<T?> GetAsync<T>(
            string key, CancellationToken cancellationToken = default)
            => Task.FromResult(
                _store.TryGetValue(key, out var value) && value is T typed
                    ? typed
                    : default);

        public Task SetAsync<T>(
            string key, T value, TimeSpan? expiration = null,
            CancellationToken cancellationToken = default)
        {
            _store[key] = value;
            LastSetKey = key;
            LastSetExpiration = expiration;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.ContainsKey(key));

        public Task<bool> TryAcquireAsync(
            string key, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            if (_store.ContainsKey(key))
                return Task.FromResult(false);
            _store[key] = true;
            return Task.FromResult(true);
        }
    }
}

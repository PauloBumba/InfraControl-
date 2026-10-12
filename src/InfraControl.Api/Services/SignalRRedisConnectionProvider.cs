using InfraControl.Infrastructure.Caching;
using StackExchange.Redis;

namespace InfraControl.Api.Services;

public sealed class SignalRRedisConnectionProvider(string connectionString) : IDisposable
{
    private readonly RedisConnectionProvider _provider =
        RedisConnectionFactory.CreateProvider(connectionString);

    public ConnectionMultiplexer Connection => _provider.Connection;

    public void Dispose() => _provider.Dispose();
}

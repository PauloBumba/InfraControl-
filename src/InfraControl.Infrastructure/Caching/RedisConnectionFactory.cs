using StackExchange.Redis;

namespace InfraControl.Infrastructure.Caching;

public static class RedisConnectionFactory
{
    public static RedisConnectionProvider CreateProvider(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return new RedisConnectionProvider(connectionString);
    }

    public static ConfigurationOptions CreateOptions(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        return options;
    }
}

public sealed class RedisConnectionProvider : IDisposable
{
    private readonly Lazy<RedisConnections> _connections;

    public RedisConnectionProvider(string connectionString)
    {
        _connections = new Lazy<RedisConnections>(
            () => Connect(connectionString),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ConnectionMultiplexer Connection => _connections.Value.Primary;

    public void Dispose()
    {
        if (!_connections.IsValueCreated)
        {
            return;
        }

        var connections = _connections.Value;
        connections.Primary.Dispose();
        connections.Sentinel?.Dispose();
    }

    private static RedisConnections Connect(string connectionString)
    {
        var options = RedisConnectionFactory.CreateOptions(connectionString);
        if (string.IsNullOrWhiteSpace(options.ServiceName))
        {
            return new RedisConnections(ConnectionMultiplexer.Connect(options), null);
        }

        var sentinelOptions = options.Clone();
        sentinelOptions.User = options.SentinelUser;
        sentinelOptions.Password = options.SentinelPassword;

        var sentinel = ConnectionMultiplexer.SentinelConnect(sentinelOptions);
        try
        {
            var primary = sentinel.GetSentinelMasterConnection(options);
            return new RedisConnections(primary, sentinel);
        }
        catch
        {
            sentinel.Dispose();
            throw;
        }
    }

    private sealed record RedisConnections(
        ConnectionMultiplexer Primary,
        ConnectionMultiplexer? Sentinel);
}

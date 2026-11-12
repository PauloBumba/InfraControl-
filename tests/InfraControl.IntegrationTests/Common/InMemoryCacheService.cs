using InfraControl.Application.Common.Interfaces;

namespace InfraControl.IntegrationTests.Common;

/// <summary>
/// ICacheService em memória p/ o host de teste — sem Redis. Cobre o
/// AccessPolicyService (ADR-066) e qualquer behavior que resolva cache.
/// </summary>
public sealed class InMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, object?> _store = new();
    private readonly Dictionary<string, HashSet<string>> _tags = new();
    private readonly object _gate = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(
                _store.TryGetValue(key, out var value) && value is T typed ? typed : default);
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        lock (_gate) _store[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_gate) _store.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_gate) return Task.FromResult(_store.ContainsKey(key));
    }

    public Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task TagAsync(string tag, string key, TimeSpan? duration = null, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_tags.TryGetValue(tag, out var members))
                _tags[tag] = members = [];
            members.Add(key);
        }
        return Task.CompletedTask;
    }

    public Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_tags.TryGetValue(tag, out var members))
            {
                foreach (var key in members) _store.Remove(key);
                _tags.Remove(tag);
            }
        }
        return Task.CompletedTask;
    }
}

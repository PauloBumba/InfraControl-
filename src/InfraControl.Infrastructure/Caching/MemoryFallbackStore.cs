using System.Collections.Concurrent;

namespace InfraControl.Infrastructure.Caching;

/// <summary>
/// Fallback de cache em memória do ADR-010: instância única, com expiração,
/// usada quando o Redis está indisponível. Não substitui o PostgreSQL —
/// apenas mantém operações normais de leitura funcionando em modo degradado.
/// </summary>
public sealed class MemoryFallbackStore(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new();
    private readonly object _acquireGate = new();

    public void Set(string key, string value, TimeSpan? expiry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        DateTimeOffset? expiresAt = expiry is { } ttl ? timeProvider.GetUtcNow() + ttl : null;
        _entries[key] = new Entry(value, expiresAt);
    }

    public string? Get(string key)
    {
        if (!_entries.TryGetValue(key, out var entry))
            return null;

        if (IsExpired(entry))
        {
            _entries.TryRemove(new KeyValuePair<string, Entry>(key, entry));
            return null;
        }

        return entry.Value;
    }

    public void Remove(string key)
    {
        _entries.TryRemove(key, out _);
    }

    public bool Exists(string key) => Get(key) is not null;

    /// <summary>Janela best-effort single-instance (modo degradado).</summary>
    public bool TryAcquire(string key, TimeSpan expiry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var now = timeProvider.GetUtcNow();

        lock (_acquireGate)
        {
            if (_entries.TryGetValue(key, out var entry) && !IsExpired(entry))
                return false;

            _entries[key] = new Entry("1", now + expiry);
            return true;
        }
    }

    private bool IsExpired(Entry entry)
        => entry.ExpiresAt is { } expiresAt && expiresAt <= timeProvider.GetUtcNow();

    private sealed record Entry(string Value, DateTimeOffset? ExpiresAt);
}

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IPGeoLocator.Services;

/// <summary>
/// Thread-safe caching service with automatic expiration and size limits
/// </summary>
public class CacheService<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, CacheEntry<TValue>> _cache = new();
    private readonly int _maxCacheSize;
    private readonly TimeSpan _defaultExpiration;
    private readonly SemaphoreSlim _cleanupLock = new(1, 1);

    public CacheService(int maxCacheSize = 1000, TimeSpan? defaultExpiration = null)
    {
        _maxCacheSize = maxCacheSize;
        _defaultExpiration = defaultExpiration ?? TimeSpan.FromMinutes(15);
    }

    public async Task<TValue?> GetOrAddAsync(
        TKey key,
        Func<Task<TValue?>> valueFactory,
        TimeSpan? expiration = null)
    {
        var expirationTime = expiration ?? _defaultExpiration;
        if (_cache.TryGetValue(key, out var entry))
        {
            if (DateTime.UtcNow - entry.Timestamp < expirationTime)
            {
                return entry.Value;
            }
            _cache.TryRemove(key, out _);
        }
        var newValue = await valueFactory().ConfigureAwait(false);
        if (_cache.Count >= _maxCacheSize)
        {
            await CleanupExpiredEntriesAsync(expirationTime).ConfigureAwait(false);
        }
        var newEntry = new CacheEntry<TValue>(newValue, DateTime.UtcNow);
        _cache.TryAdd(key, newEntry);
        return newValue;
    }

    public bool TryGet(TKey key, out TValue? value, TimeSpan? expiration = null)
    {
        var expirationTime = expiration ?? _defaultExpiration;
        if (_cache.TryGetValue(key, out var entry))
        {
            if (DateTime.UtcNow - entry.Timestamp < expirationTime)
            {
                value = entry.Value;
                return true;
            }
            _cache.TryRemove(key, out _);
        }
        value = default;
        return false;
    }

    public void Set(TKey key, TValue value)
    {
        var entry = new CacheEntry<TValue>(value, DateTime.UtcNow);
        _cache.AddOrUpdate(key, entry, (_, _) => entry);
    }

    public bool Remove(TKey key)
    {
        return _cache.TryRemove(key, out _);
    }

    public void Clear()
    {
        _cache.Clear();
    }

    public int Count => _cache.Count;

    private async Task CleanupExpiredEntriesAsync(TimeSpan expiration)
    {
        if (!await _cleanupLock.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }
        try
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _cache
                .Where(kvp => now - kvp.Value.Timestamp > expiration)
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }
            if (_cache.Count >= _maxCacheSize)
            {
                var oldestKeys = _cache
                    .OrderBy(kvp => kvp.Value.Timestamp)
                    .Take(_cache.Count - (_maxCacheSize * 3 / 4))
                    .Select(kvp => kvp.Key)
                    .ToList();
                foreach (var key in oldestKeys)
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }
        finally
        {
            _cleanupLock.Release();
        }
    }
    private record CacheEntry<T>(T? Value, DateTime Timestamp);
}

public class DisposableCacheService<TKey, TValue> : CacheService<TKey, TValue>
    where TKey : notnull
    where TValue : class, IDisposable
{
    public DisposableCacheService(int maxCacheSize = 1000, TimeSpan? defaultExpiration = null)
        : base(maxCacheSize, defaultExpiration)
    {
    }
    public new void Clear()
    {
        base.Clear();
    }
}

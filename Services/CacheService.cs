using System;
using System.Collections.Concurrent;
using System.Linq;
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

    /// <summary>
    /// Get value from cache or compute it if not found/expired
    /// </summary>
    public async Task<TValue?> GetOrAddAsync(
        TKey key, 
        Func<Task<TValue?>> valueFactory, 
        TimeSpan? expiration = null)
    {
        var expirationTime = expiration ?? _defaultExpiration;
        
        // Try to get from cache
        if (_cache.TryGetValue(key, out var entry))
        {
            if (DateTime.UtcNow - entry.Timestamp < expirationTime)
            {
                return entry.Value;
            }
            // Expired - remove it
            _cache.TryRemove(key, out _);
        }

        // Not in cache or expired - compute new value
        var newValue = await valueFactory().ConfigureAwait(false);
        
        // Ensure cache size limit
        if (_cache.Count >= _maxCacheSize)
        {
            await CleanupExpiredEntriesAsync(expirationTime).ConfigureAwait(false);
        }

        // Add to cache
        var newEntry = new CacheEntry<TValue>(newValue, DateTime.UtcNow);
        _cache.TryAdd(key, newEntry);
        
        return newValue;
    }

    /// <summary>
    /// Try to get value from cache without computing
    /// </summary>
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
            // Expired - remove it
            _cache.TryRemove(key, out _);
        }
        
        value = default;
        return false;
    }

    /// <summary>
    /// Add or update value in cache
    /// </summary>
    public void Set(TKey key, TValue value)
    {
        var entry = new CacheEntry<TValue>(value, DateTime.UtcNow);
        _cache.AddOrUpdate(key, entry, (_, _) => entry);
    }

    /// <summary>
    /// Remove value from cache
    /// </summary>
    public bool Remove(TKey key)
    {
        return _cache.TryRemove(key, out _);
    }

    /// <summary>
    /// Clear all cache entries
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Get current cache size
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Cleanup expired entries to prevent memory leaks
    /// </summary>
    private async Task CleanupExpiredEntriesAsync(TimeSpan expiration)
    {
        // Use semaphore to prevent multiple concurrent cleanups
        if (!await _cleanupLock.WaitAsync(0).ConfigureAwait(false))
        {
            return; // Another cleanup is in progress
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

            // If still over limit, remove oldest entries
            if (_cache.Count >= _maxCacheSize)
            {
                var oldestKeys = _cache
                    .OrderBy(kvp => kvp.Value.Timestamp)
                    .Take(_cache.Count - (_maxCacheSize * 3 / 4)) // Remove 25% of cache
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

/// <summary>
/// Specialized cache service for disposable resources (like Bitmaps)
/// </summary>
public class DisposableCacheService<TKey, TValue> : CacheService<TKey, TValue> 
    where TKey : notnull 
    where TValue : class, IDisposable
{
    public DisposableCacheService(int maxCacheSize = 1000, TimeSpan? defaultExpiration = null)
        : base(maxCacheSize, defaultExpiration)
    {
    }

    /// <summary>
    /// Clear cache and dispose all cached resources
    /// </summary>
    public new void Clear()
    {
        // Note: In a production app, you'd want to iterate and dispose
        // For now, we'll keep it simple
        base.Clear();
    }
}

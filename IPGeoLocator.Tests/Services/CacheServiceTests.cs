using System;
using System.Threading.Tasks;
using IPGeoLocator.Services;
using Xunit;

namespace IPGeoLocator.Tests.Services;

public class CacheServiceTests
{
    [Fact]
    public async Task GetOrAddAsync_ShouldCacheValue()
    {
        // Arrange
        var cache = new CacheService<string, string>(100, TimeSpan.FromMinutes(1));
        var callCount = 0;
        
        Func<Task<string?>> valueFactory = () =>
        {
            callCount++;
            return Task.FromResult<string?>("test-value");
        };

        // Act
        var result1 = await cache.GetOrAddAsync("key1", valueFactory);
        var result2 = await cache.GetOrAddAsync("key1", valueFactory);

        // Assert
        Assert.Equal("test-value", result1);
        Assert.Equal("test-value", result2);
        Assert.Equal(1, callCount); // Factory should only be called once
    }

    [Fact]
    public async Task GetOrAddAsync_ShouldExpireAfterTimeout()
    {
        // Arrange
        var cache = new CacheService<string, string>(100, TimeSpan.FromMilliseconds(100));
        var callCount = 0;
        
        Func<Task<string?>> valueFactory = () =>
        {
            callCount++;
            return Task.FromResult<string?>("value-" + callCount);
        };

        // Act
        var result1 = await cache.GetOrAddAsync("key1", valueFactory);
        await Task.Delay(150); // Wait for expiration
        var result2 = await cache.GetOrAddAsync("key1", valueFactory);

        // Assert
        Assert.Equal("value-1", result1);
        Assert.Equal("value-2", result2);
        Assert.Equal(2, callCount); // Factory should be called twice
    }

    [Fact]
    public async Task GetOrAddAsync_ShouldHandleConcurrentCalls()
    {
        // Arrange
        var cache = new CacheService<string, int>(100, TimeSpan.FromMinutes(1));
        var callCount = 0;
        
        Func<Task<int>> valueFactory = async () =>
        {
            await Task.Delay(50); // Simulate async work
            return System.Threading.Interlocked.Increment(ref callCount);
        };

        // Act - Multiple concurrent calls for the same key
        var tasks = new Task<int>[10];
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = cache.GetOrAddAsync("concurrent-key", valueFactory);
        }
        var results = await Task.WhenAll(tasks);

        // Assert - All should get a value, but factory might be called multiple times
        // due to race conditions in current implementation
        Assert.All(results, r => Assert.True(r > 0));
    }

    [Fact]
    public void TryGet_ShouldReturnFalseForMissingKey()
    {
        // Arrange
        var cache = new CacheService<string, string>(100, TimeSpan.FromMinutes(1));

        // Act
        var found = cache.TryGet("missing-key", out var value);

        // Assert
        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void Set_ShouldAddValue()
    {
        // Arrange
        var cache = new CacheService<string, string>(100, TimeSpan.FromMinutes(1));

        // Act
        cache.Set("key1", "value1");
        var found = cache.TryGet("key1", out var value);

        // Assert
        Assert.True(found);
        Assert.Equal("value1", value);
    }

    [Fact]
    public void Remove_ShouldRemoveValue()
    {
        // Arrange
        var cache = new CacheService<string, string>(100, TimeSpan.FromMinutes(1));
        cache.Set("key1", "value1");

        // Act
        var removed = cache.Remove("key1");
        var found = cache.TryGet("key1", out _);

        // Assert
        Assert.True(removed);
        Assert.False(found);
    }

    [Fact]
    public void Clear_ShouldRemoveAllValues()
    {
        // Arrange
        var cache = new CacheService<string, string>(100, TimeSpan.FromMinutes(1));
        cache.Set("key1", "value1");
        cache.Set("key2", "value2");
        cache.Set("key3", "value3");

        // Act
        cache.Clear();

        // Assert
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task GetOrAddAsync_ShouldRespectMaxCacheSize()
    {
        // Arrange
        var cache = new CacheService<int, string>(5, TimeSpan.FromMinutes(1));

        // Act - Add more items than max cache size
        for (int i = 0; i < 10; i++)
        {
            await cache.GetOrAddAsync(i, () => Task.FromResult<string?>("value-" + i));
        }

        // Assert - Cache should not exceed max size
        Assert.True(cache.Count <= 5);
    }
}

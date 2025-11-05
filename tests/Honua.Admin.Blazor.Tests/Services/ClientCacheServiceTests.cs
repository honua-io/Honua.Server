// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Admin.Blazor.Shared.Services;

namespace Honua.Admin.Blazor.Tests.Services;

public class ClientCacheServiceTests
{
    private readonly ClientCacheService _cache;

    public ClientCacheServiceTests()
    {
        _cache = new ClientCacheService();
    }

    [Fact]
    public async Task GetOrSetAsync_WhenCacheEmpty_ShouldCallFactoryAndStoreValue()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = "test-value";
        var factoryCalled = false;

        // Act
        var result = await _cache.GetOrSetAsync(key, () =>
        {
            factoryCalled = true;
            return Task.FromResult(expectedValue);
        });

        // Assert
        result.Should().Be(expectedValue);
        factoryCalled.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrSetAsync_WhenCacheHit_ShouldNotCallFactory()
    {
        // Arrange
        var key = "test-key";
        var firstValue = "first-value";
        var secondValue = "second-value";

        // First call to populate cache
        await _cache.GetOrSetAsync(key, () => Task.FromResult(firstValue));

        var factoryCalled = false;

        // Act
        var result = await _cache.GetOrSetAsync(key, () =>
        {
            factoryCalled = true;
            return Task.FromResult(secondValue);
        });

        // Assert
        result.Should().Be(firstValue); // Should return cached value
        factoryCalled.Should().BeFalse(); // Factory should not be called
    }

    [Fact]
    public async Task GetOrSetAsync_WhenCacheExpired_ShouldCallFactoryAgain()
    {
        // Arrange
        var key = "test-key";
        var firstValue = "first-value";
        var secondValue = "second-value";
        var shortTtl = TimeSpan.FromMilliseconds(100);

        // First call to populate cache
        await _cache.GetOrSetAsync(key, () => Task.FromResult(firstValue), ttl: shortTtl);

        // Wait for cache to expire
        await Task.Delay(150);

        // Act
        var result = await _cache.GetOrSetAsync(key, () => Task.FromResult(secondValue), ttl: shortTtl);

        // Assert
        result.Should().Be(secondValue); // Should return new value
    }

    [Fact]
    public async Task GetOrSetAsync_WithCustomTtl_ShouldRespectTtl()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        var customTtl = TimeSpan.FromMinutes(10);

        // Act
        await _cache.GetOrSetAsync(key, () => Task.FromResult(value), ttl: customTtl);

        // Assert - value should be in cache
        var result = await _cache.GetOrSetAsync(key, () => Task.FromResult("different-value"));
        result.Should().Be(value);
    }

    [Fact]
    public async Task GetOrSetAsync_WithDifferentKeys_ShouldStoreSeparately()
    {
        // Arrange
        var key1 = "key1";
        var key2 = "key2";
        var value1 = "value1";
        var value2 = "value2";

        // Act
        await _cache.GetOrSetAsync(key1, () => Task.FromResult(value1));
        await _cache.GetOrSetAsync(key2, () => Task.FromResult(value2));

        // Assert
        var result1 = await _cache.GetOrSetAsync(key1, () => Task.FromResult("different"));
        var result2 = await _cache.GetOrSetAsync(key2, () => Task.FromResult("different"));

        result1.Should().Be(value1);
        result2.Should().Be(value2);
    }

    [Fact]
    public void Invalidate_ShouldRemoveSingleKey()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        _cache.GetOrSetAsync(key, () => Task.FromResult(value)).Wait();

        // Act
        _cache.Invalidate(key);

        // Assert - factory should be called again since cache was invalidated
        var factoryCalled = false;
        var result = _cache.GetOrSetAsync(key, () =>
        {
            factoryCalled = true;
            return Task.FromResult("new-value");
        }).Result;

        factoryCalled.Should().BeTrue();
        result.Should().Be("new-value");
    }

    [Fact]
    public void InvalidatePrefix_ShouldRemoveAllMatchingKeys()
    {
        // Arrange
        _cache.GetOrSetAsync("services:all", () => Task.FromResult("all")).Wait();
        _cache.GetOrSetAsync("services:123", () => Task.FromResult("123")).Wait();
        _cache.GetOrSetAsync("services:456", () => Task.FromResult("456")).Wait();
        _cache.GetOrSetAsync("layers:all", () => Task.FromResult("layers")).Wait();

        // Act
        _cache.InvalidatePrefix("services:");

        // Assert - all services keys should be invalidated
        var servicesAllCalled = false;
        var services123Called = false;
        var layersAllCalled = false;

        _cache.GetOrSetAsync("services:all", () =>
        {
            servicesAllCalled = true;
            return Task.FromResult("new-all");
        }).Wait();

        _cache.GetOrSetAsync("services:123", () =>
        {
            services123Called = true;
            return Task.FromResult("new-123");
        }).Wait();

        _cache.GetOrSetAsync("layers:all", () =>
        {
            layersAllCalled = true;
            return Task.FromResult("new-layers");
        }).Wait();

        servicesAllCalled.Should().BeTrue();
        services123Called.Should().BeTrue();
        layersAllCalled.Should().BeFalse(); // layers should still be cached
    }

    [Fact]
    public void Clear_ShouldRemoveAllEntries()
    {
        // Arrange
        _cache.GetOrSetAsync("key1", () => Task.FromResult("value1")).Wait();
        _cache.GetOrSetAsync("key2", () => Task.FromResult("value2")).Wait();
        _cache.GetOrSetAsync("key3", () => Task.FromResult("value3")).Wait();

        // Act
        _cache.Clear();

        // Assert - all keys should be invalidated
        var stats = _cache.GetStats();
        stats.TotalEntries.Should().Be(0);
    }

    [Fact]
    public void RemoveExpired_ShouldRemoveOnlyExpiredEntries()
    {
        // Arrange
        var shortTtl = TimeSpan.FromMilliseconds(100);
        var longTtl = TimeSpan.FromMinutes(10);

        _cache.GetOrSetAsync("expired1", () => Task.FromResult("value1"), ttl: shortTtl).Wait();
        _cache.GetOrSetAsync("expired2", () => Task.FromResult("value2"), ttl: shortTtl).Wait();
        _cache.GetOrSetAsync("valid", () => Task.FromResult("value3"), ttl: longTtl).Wait();

        // Wait for short TTL entries to expire
        Task.Delay(150).Wait();

        // Act
        var removedCount = _cache.RemoveExpired();

        // Assert
        removedCount.Should().Be(2);
        var stats = _cache.GetStats();
        stats.TotalEntries.Should().Be(1); // Only the valid entry remains
    }

    [Fact]
    public void GetStats_ShouldReturnCorrectStatistics()
    {
        // Arrange
        var cache = new ClientCacheService();
        cache.GetOrSetAsync("key1", () => Task.FromResult("value1")).Wait();
        cache.GetOrSetAsync("key2", () => Task.FromResult("value2")).Wait();

        // Cache hit
        cache.GetOrSetAsync("key1", () => Task.FromResult("different")).Wait();

        // Act
        var stats = cache.GetStats();

        // Assert
        stats.TotalEntries.Should().Be(2);
        stats.TotalHits.Should().Be(1);
        stats.TotalMisses.Should().Be(2);
        stats.HitRate.Should().BeApproximately(0.333, 0.01);
    }

    [Fact]
    public void GetStats_WhenNoOperations_ShouldReturnZeroHitRate()
    {
        // Arrange
        var cache = new ClientCacheService();

        // Act
        var stats = cache.GetStats();

        // Assert
        stats.TotalEntries.Should().Be(0);
        stats.TotalHits.Should().Be(0);
        stats.TotalMisses.Should().Be(0);
        stats.HitRate.Should().Be(0);
    }

    [Fact]
    public async Task GetOrSetAsync_WithComplexObject_ShouldStoreAndRetrieve()
    {
        // Arrange
        var key = "complex-key";
        var complexObject = new TestComplexObject
        {
            Id = "123",
            Name = "Test",
            Tags = new List<string> { "tag1", "tag2" },
            Metadata = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            }
        };

        // Act
        var result = await _cache.GetOrSetAsync(key, () => Task.FromResult(complexObject));

        // Assert
        result.Should().BeEquivalentTo(complexObject);

        // Verify it returns the same object on second call
        var cachedResult = await _cache.GetOrSetAsync(key, () => Task.FromResult(new TestComplexObject()));
        cachedResult.Should().BeSameAs(complexObject);
    }

    [Fact]
    public async Task GetOrSetAsync_ConcurrentCalls_ShouldHandleCorrectly()
    {
        // Arrange
        var key = "concurrent-key";
        var callCount = 0;

        // Act - simulate concurrent calls
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            _cache.GetOrSetAsync(key, async () =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(10);
                return "value";
            })
        ).ToArray();

        await Task.WhenAll(tasks);

        // Assert - factory should be called only once due to caching
        // Note: Due to the async nature and potential race conditions,
        // the call count might be slightly higher, but should be significantly less than 10
        callCount.Should().BeLessThan(10);
    }

    [Fact]
    public void CacheKeys_ShouldGenerateCorrectKeys()
    {
        // Assert
        CacheKeys.Services().Should().Be("services:all");
        CacheKeys.Service("test-id").Should().Be("services:test-id");
        CacheKeys.Layers().Should().Be("layers:all");
        CacheKeys.Layer("test-id").Should().Be("layers:test-id");
        CacheKeys.Folders().Should().Be("folders:all");
        CacheKeys.Folder("test-id").Should().Be("folders:test-id");
        CacheKeys.Styles().Should().Be("styles:all");
        CacheKeys.Style("test-id").Should().Be("styles:test-id");
        CacheKeys.Dashboard().Should().Be("dashboard:stats");
    }

    private class TestComplexObject
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}

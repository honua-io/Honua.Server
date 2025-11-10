// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Caching;

public class QueryResultCacheServiceTests
{
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<QueryResultCacheService>> _mockLogger;
    private readonly Mock<IOptions<CacheOptions>> _mockOptions;
    private readonly QueryResultCacheService _service;

    public QueryResultCacheServiceTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<QueryResultCacheService>>();
        _mockOptions = new Mock<IOptions<CacheOptions>>();

        var options = new CacheOptions
        {
            Enabled = true,
            DefaultTtlMinutes = 10
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        _service = new QueryResultCacheService(
            _memoryCache,
            _mockLogger.Object,
            _mockOptions.Object);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenNotCached_ExecutesFactory()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = "test-value";
        var factoryCalled = false;

        Task<string> Factory()
        {
            factoryCalled = true;
            return Task.FromResult(expectedValue);
        }

        // Act
        var result = await _service.GetOrCreateAsync(key, Factory);

        // Assert
        result.Should().Be(expectedValue);
        factoryCalled.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenCached_ReturnsFromCache()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = "cached-value";
        var factoryCallCount = 0;

        Task<string> Factory()
        {
            factoryCallCount++;
            return Task.FromResult(expectedValue);
        }

        // Act - First call should cache
        await _service.GetOrCreateAsync(key, Factory);
        // Second call should use cache
        var result = await _service.GetOrCreateAsync(key, Factory);

        // Assert
        result.Should().Be(expectedValue);
        factoryCallCount.Should().Be(1); // Factory should only be called once
    }

    [Fact]
    public async Task GetOrCreateAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var key = "test-key";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Task<string> Factory(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult("value");
        }

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.GetOrCreateAsync(key, () => Factory(cts.Token), cts.Token));
    }

    [Fact]
    public async Task InvalidateAsync_RemovesCachedEntry()
    {
        // Arrange
        var key = "test-key";
        var factoryCallCount = 0;

        Task<string> Factory()
        {
            factoryCallCount++;
            return Task.FromResult($"value-{factoryCallCount}");
        }

        // Act - Cache the value
        var result1 = await _service.GetOrCreateAsync(key, Factory);

        // Invalidate the cache
        await _service.InvalidateAsync(key);

        // Get again - should call factory
        var result2 = await _service.GetOrCreateAsync(key, Factory);

        // Assert
        result1.Should().Be("value-1");
        result2.Should().Be("value-2");
        factoryCallCount.Should().Be(2);
    }

    [Fact]
    public async Task InvalidatePatternAsync_RemovesMatchingEntries()
    {
        // Arrange
        var factoryCallCount = 0;

        Task<string> Factory()
        {
            factoryCallCount++;
            return Task.FromResult($"value-{factoryCallCount}");
        }

        // Cache multiple entries with similar keys
        await _service.GetOrCreateAsync("feature:1", Factory);
        await _service.GetOrCreateAsync("feature:2", Factory);
        await _service.GetOrCreateAsync("other:1", Factory);

        // Act - Invalidate pattern
        await _service.InvalidatePatternAsync("feature:*");

        // Try to get again
        await _service.GetOrCreateAsync("feature:1", Factory);
        await _service.GetOrCreateAsync("other:1", Factory);

        // Assert
        // feature:1 should have been invalidated and called factory again
        // other:1 should still be cached
        factoryCallCount.Should().BeGreaterThan(3);
    }

    [Fact]
    public void GetCacheStatistics_ReturnsStatistics()
    {
        // Act
        var stats = _service.GetCacheStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.Should().ContainKey("TotalEntries").Or.ContainKey("CacheSize");
    }
}

public class CacheOptions
{
    public bool Enabled { get; set; }
    public int DefaultTtlMinutes { get; set; }
}

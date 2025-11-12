// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Caching;
using Honua.Server.Core.Observability;
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
    private readonly Mock<ICacheMetrics> _mockMetrics;
    private readonly Mock<IOptions<QueryResultCacheOptions>> _mockOptions;
    private readonly QueryResultCacheService _service;

    public QueryResultCacheServiceTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<QueryResultCacheService>>();
        _mockMetrics = new Mock<ICacheMetrics>();
        _mockOptions = new Mock<IOptions<QueryResultCacheOptions>>();

        var options = new QueryResultCacheOptions
        {
            DefaultExpiration = TimeSpan.FromMinutes(10),
            UseDistributedCache = false
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        _service = new QueryResultCacheService(
            null, // no distributed cache
            _memoryCache,
            _mockLogger.Object,
            _mockMetrics.Object,
            _mockOptions.Object);
    }

    [Fact]
    public async Task GetOrSetAsync_WhenNotCached_ExecutesFactory()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = "test-value";
        var factoryCalled = false;

        Task<string> Factory(CancellationToken ct)
        {
            factoryCalled = true;
            return Task.FromResult(expectedValue);
        }

        // Act
        var result = await _service.GetOrSetAsync(key, Factory);

        // Assert
        result.Should().Be(expectedValue);
        factoryCalled.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrSetAsync_WhenCached_ReturnsFromCache()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = "cached-value";
        var factoryCallCount = 0;

        Task<string> Factory(CancellationToken ct)
        {
            factoryCallCount++;
            return Task.FromResult(expectedValue);
        }

        // Act - First call should cache
        await _service.GetOrSetAsync(key, Factory);
        // Second call should use cache
        var result = await _service.GetOrSetAsync(key, Factory);

        // Assert
        result.Should().Be(expectedValue);
        factoryCallCount.Should().Be(1); // Factory should only be called once
    }

    [Fact]
    public async Task GetOrSetAsync_WithCancellation_PropagatesCancellation()
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
            _service.GetOrSetAsync(key, Factory, null, cts.Token));
    }

    [Fact]
    public async Task RemoveAsync_RemovesCachedEntry()
    {
        // Arrange
        var key = "test-key";
        var factoryCallCount = 0;

        Task<string> Factory(CancellationToken ct)
        {
            factoryCallCount++;
            return Task.FromResult($"value-{factoryCallCount}");
        }

        // Act - Cache the value
        var result1 = await _service.GetOrSetAsync(key, Factory);

        // Remove the cache
        await _service.RemoveAsync(key);

        // Get again - should call factory
        var result2 = await _service.GetOrSetAsync(key, Factory);

        // Assert
        result1.Should().Be("value-1");
        result2.Should().Be("value-2");
        factoryCallCount.Should().Be(2);
    }

    [Fact]
    public async Task RemoveByPatternAsync_LogsWarning()
    {
        // Arrange
        var factoryCallCount = 0;

        Task<string> Factory(CancellationToken ct)
        {
            factoryCallCount++;
            return Task.FromResult($"value-{factoryCallCount}");
        }

        // Cache multiple entries with similar keys
        await _service.GetOrSetAsync("feature:1", Factory);
        await _service.GetOrSetAsync("feature:2", Factory);
        await _service.GetOrSetAsync("other:1", Factory);

        // Act - Try to invalidate pattern (should log warning but not throw)
        await _service.RemoveByPatternAsync("feature:*");

        // Assert - method should not throw
        // Note: pattern-based invalidation is not fully implemented, so this just verifies it doesn't throw
        factoryCallCount.Should().Be(3);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullWhenNotCached()
    {
        // Arrange
        var key = "non-existent-key";

        // Act
        var result = await _service.GetAsync<string>(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsValue()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";

        // Act
        await _service.SetAsync(key, value);
        var result = await _service.GetAsync<string>(key);

        // Assert
        result.Should().Be(value);
    }
}

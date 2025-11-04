using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Caching.Resilience;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Exceptions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Caching;

public sealed class CacheInvalidationRetryPolicyTests
{
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<IOptionsMonitor<CacheInvalidationOptions>> _optionsMonitorMock;
    private readonly Mock<ILogger<CacheInvalidationRetryPolicy>> _loggerMock;
    private readonly CacheInvalidationOptions _options;
    private readonly CacheInvalidationRetryPolicy _policy;

    public CacheInvalidationRetryPolicyTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _optionsMonitorMock = new Mock<IOptionsMonitor<CacheInvalidationOptions>>();
        _loggerMock = new Mock<ILogger<CacheInvalidationRetryPolicy>>();

        _options = new CacheInvalidationOptions
        {
            RetryCount = 3,
            RetryDelayMs = 100,
            MaxRetryDelayMs = 5000,
            OperationTimeout = TimeSpan.FromSeconds(2),
            EnableDetailedLogging = true
        };

        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(_options);
        _policy = new CacheInvalidationRetryPolicy(_optionsMonitorMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task InvalidateWithRetryAsync_SucceedsOnFirstAttempt()
    {
        // Arrange
        var cacheKey = "test-key";
        _cacheMock
            .Setup(x => x.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _policy.InvalidateWithRetryAsync(_cacheMock.Object, cacheKey, "test-cache");

        // Assert
        _cacheMock.Verify(x => x.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidateWithRetryAsync_RetriesOnFailureAndSucceeds()
    {
        // Arrange
        var cacheKey = "test-key";
        var attemptCount = 0;

        _cacheMock
            .Setup(x => x.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new InvalidOperationException("Simulated cache failure");
                }
                return Task.CompletedTask;
            });

        // Act
        await _policy.InvalidateWithRetryAsync(_cacheMock.Object, cacheKey, "test-cache");

        // Assert
        Assert.Equal(3, attemptCount);
        _cacheMock.Verify(x => x.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task InvalidateWithRetryAsync_ThrowsAfterAllRetriesFail()
    {
        // Arrange
        var cacheKey = "test-key";
        _cacheMock
            .Setup(x => x.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Persistent cache failure"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<CacheInvalidationException>(
            () => _policy.InvalidateWithRetryAsync(_cacheMock.Object, cacheKey, "test-cache"));

        Assert.Equal("test-cache", exception.CacheName);
        Assert.Equal(cacheKey, exception.CacheKey);
        Assert.Contains("Failed after", exception.Message);

        // Should retry: initial attempt + 3 retries = 4 total
        _cacheMock.Verify(x => x.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    [Fact]
    public async Task InvalidateWithRetryAsync_HandlesTimeout()
    {
        // Arrange
        var cacheKey = "test-key";
        _options.OperationTimeout = TimeSpan.FromMilliseconds(50);

        _cacheMock
            .Setup(x => x.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()))
            .Returns(async (string key, CancellationToken ct) =>
            {
                await Task.Delay(200, ct); // Delay longer than timeout
            });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<CacheInvalidationException>(
            () => _policy.InvalidateWithRetryAsync(_cacheMock.Object, cacheKey, "test-cache"));

        Assert.Equal("test-cache", exception.CacheName);
        Assert.Equal(cacheKey, exception.CacheKey);
    }

    [Fact]
    public async Task InvalidateWithRetryAsync_UsesExponentialBackoff()
    {
        // Arrange
        var cacheKey = "test-key";
        var attemptTimestamps = new System.Collections.Generic.List<DateTime>();

        _cacheMock
            .Setup(x => x.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attemptTimestamps.Add(DateTime.UtcNow);
                if (attemptTimestamps.Count < 3)
                {
                    throw new InvalidOperationException("Simulated failure");
                }
                return Task.CompletedTask;
            });

        // Act
        await _policy.InvalidateWithRetryAsync(_cacheMock.Object, cacheKey, "test-cache");

        // Assert
        Assert.Equal(3, attemptTimestamps.Count);

        // Check that delays increase exponentially (with some tolerance for timing)
        var delay1 = (attemptTimestamps[1] - attemptTimestamps[0]).TotalMilliseconds;
        var delay2 = (attemptTimestamps[2] - attemptTimestamps[1]).TotalMilliseconds;

        Assert.True(delay1 >= 80 && delay1 <= 150, $"First delay should be ~100ms, was {delay1}ms");
        Assert.True(delay2 >= 180 && delay2 <= 250, $"Second delay should be ~200ms, was {delay2}ms");
    }

    [Fact]
    public void GetRetryDelay_ReturnsCorrectExponentialBackoff()
    {
        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(100), _options.GetRetryDelay(1));
        Assert.Equal(TimeSpan.FromMilliseconds(200), _options.GetRetryDelay(2));
        Assert.Equal(TimeSpan.FromMilliseconds(400), _options.GetRetryDelay(3));
        Assert.Equal(TimeSpan.FromMilliseconds(800), _options.GetRetryDelay(4));
    }

    [Fact]
    public void GetRetryDelay_RespectsMaxDelay()
    {
        // Arrange
        _options.MaxRetryDelayMs = 500;

        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(100), _options.GetRetryDelay(1));
        Assert.Equal(TimeSpan.FromMilliseconds(200), _options.GetRetryDelay(2));
        Assert.Equal(TimeSpan.FromMilliseconds(400), _options.GetRetryDelay(3));
        Assert.Equal(TimeSpan.FromMilliseconds(500), _options.GetRetryDelay(4)); // Capped
        Assert.Equal(TimeSpan.FromMilliseconds(500), _options.GetRetryDelay(5)); // Capped
    }

    [Fact]
    public async Task TrySetShortTtlAsync_SucceedsWhenCacheWorks()
    {
        // Arrange
        var cacheKey = "test-key";
        var value = new byte[] { 1, 2, 3 };
        var ttlSet = false;

        _cacheMock
            .Setup(x => x.SetAsync(cacheKey, value, It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (key, val, opts, ct) =>
                {
                    ttlSet = opts.AbsoluteExpirationRelativeToNow == _options.ShortTtl;
                })
            .Returns(Task.CompletedTask);

        // Act
        var result = await _policy.TrySetShortTtlAsync(_cacheMock.Object, cacheKey, value, "test-cache");

        // Assert
        Assert.True(result);
        Assert.True(ttlSet);
        _cacheMock.Verify(x => x.SetAsync(cacheKey, value, It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TrySetShortTtlAsync_ReturnsFalseOnFailure()
    {
        // Arrange
        var cacheKey = "test-key";
        var value = new byte[] { 1, 2, 3 };

        _cacheMock
            .Setup(x => x.SetAsync(cacheKey, value, It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Cache failure"));

        // Act
        var result = await _policy.TrySetShortTtlAsync(_cacheMock.Object, cacheKey, value, "test-cache");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TrySetShortTtlAsync_HandlesNullInputsGracefully()
    {
        // Act & Assert
        var result1 = await _policy.TrySetShortTtlAsync(null!, "key", new byte[] { 1 }, "cache");
        Assert.False(result1);

        var result2 = await _policy.TrySetShortTtlAsync(_cacheMock.Object, null!, new byte[] { 1 }, "cache");
        Assert.False(result2);

        var result3 = await _policy.TrySetShortTtlAsync(_cacheMock.Object, "key", null!, "cache");
        Assert.False(result3);
    }

    [Fact]
    public async Task InvalidateWithRetryAsync_PropagatesCancellation()
    {
        // Arrange
        var cacheKey = "test-key";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _cacheMock
            .Setup(x => x.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _policy.InvalidateWithRetryAsync(_cacheMock.Object, cacheKey, "test-cache", cts.Token));
    }
}

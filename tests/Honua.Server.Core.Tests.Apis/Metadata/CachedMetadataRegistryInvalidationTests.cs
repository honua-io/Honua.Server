using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Caching.Resilience;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Metadata;

public sealed class CachedMetadataRegistryInvalidationTests
{
    private readonly Mock<IMetadataRegistry> _innerRegistryMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<IOptionsMonitor<MetadataCacheOptions>> _cacheOptionsMock;
    private readonly Mock<IOptionsMonitor<CacheInvalidationOptions>> _invalidationOptionsMock;
    private readonly Mock<CacheInvalidationRetryPolicy> _retryPolicyMock;
    private readonly Mock<ILogger<CachedMetadataRegistry>> _loggerMock;
    private readonly MetadataCacheMetrics _metrics;
    private readonly MetadataCacheOptions _cacheOptions;
    private readonly CacheInvalidationOptions _invalidationOptions;

    public CachedMetadataRegistryInvalidationTests()
    {
        _innerRegistryMock = new Mock<IMetadataRegistry>();
        _cacheMock = new Mock<IDistributedCache>();
        _cacheOptionsMock = new Mock<IOptionsMonitor<MetadataCacheOptions>>();
        _invalidationOptionsMock = new Mock<IOptionsMonitor<CacheInvalidationOptions>>();
        _loggerMock = new Mock<ILogger<CachedMetadataRegistry>>();
        _metrics = new MetadataCacheMetrics();

        _cacheOptions = new MetadataCacheOptions
        {
            KeyPrefix = "test:",
            SchemaVersion = 1
        };

        _invalidationOptions = new CacheInvalidationOptions
        {
            Strategy = CacheInvalidationStrategy.Strict,
            RetryCount = 3,
            RetryDelayMs = 10, // Short for testing
            EnableDetailedLogging = false
        };

        _cacheOptionsMock.Setup(x => x.CurrentValue).Returns(_cacheOptions);
        _invalidationOptionsMock.Setup(x => x.CurrentValue).Returns(_invalidationOptions);

        // Create mock for retry policy
        var retryPolicyLoggerMock = new Mock<ILogger<CacheInvalidationRetryPolicy>>();
        _retryPolicyMock = new Mock<CacheInvalidationRetryPolicy>(
            _invalidationOptionsMock.Object,
            retryPolicyLoggerMock.Object);
    }

    [Fact]
    public async Task ReloadAsync_Strict_ThrowsOnInvalidationFailure()
    {
        // Arrange
        _invalidationOptions.Strategy = CacheInvalidationStrategy.Strict;

        _innerRegistryMock
            .Setup(x => x.ReloadAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cacheMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Cache unavailable"));

        var registry = new CachedMetadataRegistry(
            _innerRegistryMock.Object,
            _cacheMock.Object,
            _cacheOptionsMock.Object,
            _invalidationOptionsMock.Object,
            _loggerMock.Object,
            null, // No retry policy to test direct failure
            _metrics);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.ReloadAsync(CancellationToken.None));

        // Verify reload was called but cache invalidation failed
        _innerRegistryMock.Verify(x => x.ReloadAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReloadAsync_Eventual_ContinuesOnInvalidationFailure()
    {
        // Arrange
        _invalidationOptions.Strategy = CacheInvalidationStrategy.Eventual;

        _innerRegistryMock
            .Setup(x => x.ReloadAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cacheMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Cache unavailable"));

        var registry = new CachedMetadataRegistry(
            _innerRegistryMock.Object,
            _cacheMock.Object,
            _cacheOptionsMock.Object,
            _invalidationOptionsMock.Object,
            _loggerMock.Object,
            null,
            _metrics);

        // Act - Should not throw
        await registry.ReloadAsync(CancellationToken.None);

        // Assert
        _innerRegistryMock.Verify(x => x.ReloadAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1, _metrics.GetInvalidationStatistics().Failures);
    }

    [Fact]
    public async Task UpdateAsync_Strict_ThrowsOnInvalidationFailure()
    {
        // Arrange
        _invalidationOptions.Strategy = CacheInvalidationStrategy.Strict;

        var snapshot = CreateMockSnapshot();
        _innerRegistryMock
            .Setup(x => x.UpdateAsync(snapshot, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cacheMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Cache unavailable"));

        var registry = new CachedMetadataRegistry(
            _innerRegistryMock.Object,
            _cacheMock.Object,
            _cacheOptionsMock.Object,
            _invalidationOptionsMock.Object,
            _loggerMock.Object,
            null,
            _metrics);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.UpdateAsync(snapshot, CancellationToken.None));

        // Verify update was called but cache invalidation failed
        _innerRegistryMock.Verify(x => x.UpdateAsync(snapshot, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidateCacheAsync_ShortTTL_AttemptsToSetShortTtl()
    {
        // Arrange
        _invalidationOptions.Strategy = CacheInvalidationStrategy.ShortTTL;
        _invalidationOptions.ShortTtl = TimeSpan.FromSeconds(30);
        _invalidationOptions.RetryCount = 2; // Retry policy needs at least one retry

        var cachedBytes = new byte[] { 1, 2, 3 };
        _cacheMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Cache removal failed"));

        _cacheMock
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedBytes);

        _cacheMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), cachedBytes, It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Need retry policy to get CacheInvalidationException
        var retryPolicyLogger = new Mock<ILogger<CacheInvalidationRetryPolicy>>();
        var retryPolicy = new CacheInvalidationRetryPolicy(
            _invalidationOptionsMock.Object,
            retryPolicyLogger.Object);

        var registry = new CachedMetadataRegistry(
            _innerRegistryMock.Object,
            _cacheMock.Object,
            _cacheOptionsMock.Object,
            _invalidationOptionsMock.Object,
            _loggerMock.Object,
            retryPolicy,
            _metrics);

        // Act - Should not throw with ShortTTL strategy
        await registry.InvalidateCacheAsync(CancellationToken.None);

        // Assert - Should attempt to get and re-set with short TTL
        _cacheMock.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidateCacheAsync_RecordsMetrics()
    {
        // Arrange
        _invalidationOptions.Strategy = CacheInvalidationStrategy.Eventual;

        _cacheMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var registry = new CachedMetadataRegistry(
            _innerRegistryMock.Object,
            _cacheMock.Object,
            _cacheOptionsMock.Object,
            _invalidationOptionsMock.Object,
            _loggerMock.Object,
            null,
            _metrics);

        // Act
        await registry.InvalidateCacheAsync(CancellationToken.None);

        // Assert
        var stats = _metrics.GetInvalidationStatistics();
        Assert.Equal(1, stats.Successes);
        Assert.Equal(0, stats.Failures);
    }

    [Fact]
    public async Task InvalidateCacheAsync_RecordsFailureMetrics()
    {
        // Arrange
        _invalidationOptions.Strategy = CacheInvalidationStrategy.Eventual;

        _cacheMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Cache error"));

        var registry = new CachedMetadataRegistry(
            _innerRegistryMock.Object,
            _cacheMock.Object,
            _cacheOptionsMock.Object,
            _invalidationOptionsMock.Object,
            _loggerMock.Object,
            null,
            _metrics);

        // Act
        await registry.InvalidateCacheAsync(CancellationToken.None);

        // Assert
        var stats = _metrics.GetInvalidationStatistics();
        Assert.Equal(0, stats.Successes);
        Assert.Equal(1, stats.Failures);
    }

    [Fact]
    public async Task InvalidateCacheAsync_WithRetryPolicy_RetriesOnFailure()
    {
        // Arrange
        var attemptCount = 0;
        _cacheMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new InvalidOperationException("Transient failure");
                }
                return Task.CompletedTask;
            });

        var retryPolicyLogger = new Mock<ILogger<CacheInvalidationRetryPolicy>>();
        var retryPolicy = new CacheInvalidationRetryPolicy(
            _invalidationOptionsMock.Object,
            retryPolicyLogger.Object);

        var registry = new CachedMetadataRegistry(
            _innerRegistryMock.Object,
            _cacheMock.Object,
            _cacheOptionsMock.Object,
            _invalidationOptionsMock.Object,
            _loggerMock.Object,
            retryPolicy,
            _metrics);

        // Act
        await registry.InvalidateCacheAsync(CancellationToken.None);

        // Assert
        Assert.Equal(3, attemptCount);
        _cacheMock.Verify(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    private static MetadataSnapshot CreateMockSnapshot()
    {
        var catalog = new CatalogDefinition
        {
            Id = "test-catalog",
            Title = "Test Catalog",
            Description = "Test catalog for invalidation tests"
        };

        return new MetadataSnapshot(
            catalog,
            Array.Empty<FolderDefinition>(),
            Array.Empty<DataSourceDefinition>(),
            Array.Empty<ServiceDefinition>(),
            Array.Empty<LayerDefinition>(),
            Array.Empty<RasterDatasetDefinition>(),
            Array.Empty<StyleDefinition>(),
            ServerDefinition.Default);
    }
}

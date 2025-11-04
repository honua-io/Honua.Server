using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Caching;
using Honua.Server.Core.Observability;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Caching;

/// <summary>
/// Tests for the QueryResultCacheService.
/// Verifies caching behavior, fallback mechanisms, compression, and cache-aside pattern.
/// </summary>
public sealed class QueryResultCacheServiceTests
{
    private readonly Mock<IDistributedCache> _mockDistributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ICacheMetrics> _mockMetrics;
    private readonly QueryResultCacheOptions _options;
    private readonly QueryResultCacheService _cacheService;

    public QueryResultCacheServiceTests()
    {
        _mockDistributedCache = new Mock<IDistributedCache>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockMetrics = new Mock<ICacheMetrics>();
        _options = new QueryResultCacheOptions
        {
            DefaultExpiration = TimeSpan.FromMinutes(5),
            EnableCompression = true,
            CompressionThreshold = 1024,
            UseDistributedCache = true
        };

        _cacheService = new QueryResultCacheService(
            _mockDistributedCache.Object,
            _memoryCache,
            NullLogger<QueryResultCacheService>.Instance,
            _mockMetrics.Object,
            Options.Create(_options)
        );
    }

    [Fact]
    public async Task GetOrSetAsync_CacheMiss_ExecutesFactoryAndCachesResult()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = "test-value";
        var factoryCallCount = 0;

        _mockDistributedCache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _cacheService.GetOrSetAsync(
            key,
            ct =>
            {
                factoryCallCount++;
                return Task.FromResult(expectedValue);
            }
        );

        // Assert
        Assert.Equal(expectedValue, result);
        Assert.Equal(1, factoryCallCount);

        // Verify factory was called only once
        var secondResult = await _cacheService.GetAsync<string>(key);
        Assert.Equal(expectedValue, secondResult);
        Assert.Equal(1, factoryCallCount); // Should still be 1
    }

    [Fact]
    public async Task GetOrSetAsync_CacheHit_DoesNotExecuteFactory()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = "test-value";
        var factoryCallCount = 0;

        // First call to populate cache
        await _cacheService.GetOrSetAsync(
            key,
            ct =>
            {
                factoryCallCount++;
                return Task.FromResult(expectedValue);
            }
        );

        // Act - Second call should hit cache
        var result = await _cacheService.GetOrSetAsync(
            key,
            ct =>
            {
                factoryCallCount++;
                return Task.FromResult("different-value");
            }
        );

        // Assert
        Assert.Equal(expectedValue, result);
        Assert.Equal(1, factoryCallCount); // Factory called only once
    }

    [Fact]
    public async Task SetAsync_StoresValueInBothCaches()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";

        // Act
        await _cacheService.SetAsync(key, value);

        // Assert - Should be in memory cache
        var memoryResult = await _cacheService.GetAsync<string>(key);
        Assert.Equal(value, memoryResult);

        // Verify distributed cache was called
        _mockDistributedCache.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task RemoveAsync_RemovesFromBothCaches()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        await _cacheService.SetAsync(key, value);

        // Act
        await _cacheService.RemoveAsync(key);

        // Assert - Should not be in memory cache
        var result = await _cacheService.GetAsync<string>(key);
        Assert.Null(result);

        // Verify distributed cache remove was called
        _mockDistributedCache.Verify(
            x => x.RemoveAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetOrSetAsync_WithCustomExpiration_UsesProvidedExpiration()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        var customExpiration = TimeSpan.FromSeconds(30);

        // Act
        await _cacheService.GetOrSetAsync(
            key,
            ct => Task.FromResult(value),
            expiration: customExpiration
        );

        // Assert - Verify SetAsync was called with custom expiration
        _mockDistributedCache.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.Is<DistributedCacheEntryOptions>(o =>
                    o.AbsoluteExpirationRelativeToNow == customExpiration
                ),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetOrSetAsync_FallsBackToMemoryCache_WhenDistributedCacheFails()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";

        _mockDistributedCache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis is down"));

        // Act
        var result = await _cacheService.GetOrSetAsync(
            key,
            ct => Task.FromResult(value)
        );

        // Assert
        Assert.Equal(value, result);

        // Verify error was recorded in metrics
        _mockMetrics.Verify(
            x => x.RecordCacheError(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            ),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public async Task GetOrSetAsync_ThrowsArgumentNullException_WhenKeyIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _cacheService.GetOrSetAsync<string>(
                null!,
                ct => Task.FromResult("value")
            )
        );
    }

    [Fact]
    public async Task GetOrSetAsync_ThrowsArgumentNullException_WhenFactoryIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _cacheService.GetOrSetAsync<string>(
                "key",
                null!
            )
        );
    }

    [Fact]
    public async Task GetOrSetAsync_WithComplexObject_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        var key = "complex-object-key";
        var value = new TestComplexObject
        {
            Id = 123,
            Name = "Test Object",
            Tags = new[] { "tag1", "tag2", "tag3" },
            Metadata = new TestMetadata
            {
                Created = DateTimeOffset.UtcNow,
                Modified = DateTimeOffset.UtcNow.AddHours(1)
            }
        };

        // Act
        var result = await _cacheService.GetOrSetAsync(
            key,
            ct => Task.FromResult(value)
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(value.Id, result.Id);
        Assert.Equal(value.Name, result.Name);
        Assert.Equal(value.Tags, result.Tags);
        Assert.Equal(value.Metadata.Created, result.Metadata.Created);
    }

    private sealed class TestComplexObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string[] Tags { get; set; } = Array.Empty<string>();
        public TestMetadata Metadata { get; set; } = new();
    }

    private sealed class TestMetadata
    {
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset Modified { get; set; }
    }
}

/// <summary>
/// Tests for CacheKeyBuilder to ensure consistent key generation.
/// </summary>
public sealed class CacheKeyBuilderTests
{
    [Fact]
    public void ForLayer_GeneratesCorrectKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder
            .ForLayer("service1", "layer1")
            .WithSuffix("metadata")
            .Build();

        // Assert
        Assert.Contains("layer", key);
        Assert.Contains("service1", key);
        Assert.Contains("layer1", key);
        Assert.Contains("metadata", key);
    }

    [Fact]
    public void ForTile_GeneratesCorrectKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder
            .ForTile("WebMercatorQuad", 5, 10, 12, "pbf")
            .Build();

        // Assert
        Assert.Contains("tile", key);
        Assert.Contains("WebMercatorQuad", key);
        Assert.Contains("5", key);
        Assert.Contains("10", key);
        Assert.Contains("12", key);
        Assert.Contains("pbf", key);
    }

    [Fact]
    public void ForQuery_WithBoundingBox_GeneratesConsistentKey()
    {
        // Arrange & Act
        var key1 = CacheKeyBuilder
            .ForQuery("layer1")
            .WithBoundingBox(-180, -90, 180, 90)
            .Build();

        var key2 = CacheKeyBuilder
            .ForQuery("layer1")
            .WithBoundingBox(-180, -90, 180, 90)
            .Build();

        // Assert
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void ForQuery_WithDifferentBoundingBox_GeneratesDifferentKey()
    {
        // Arrange & Act
        var key1 = CacheKeyBuilder
            .ForQuery("layer1")
            .WithBoundingBox(-180, -90, 180, 90)
            .Build();

        var key2 = CacheKeyBuilder
            .ForQuery("layer1")
            .WithBoundingBox(-180, -90, 180, 91)
            .Build();

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void WithObjectHash_GeneratesConsistentHashForSameObject()
    {
        // Arrange
        var obj1 = new { Foo = "bar", Baz = 123 };
        var obj2 = new { Foo = "bar", Baz = 123 };

        // Act
        var key1 = CacheKeyBuilder
            .ForQuery("layer1")
            .WithObjectHash(obj1)
            .Build();

        var key2 = CacheKeyBuilder
            .ForQuery("layer1")
            .WithObjectHash(obj2)
            .Build();

        // Assert
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void Build_ThrowsInvalidOperationException_WhenCalledTwice()
    {
        // Arrange
        var builder = CacheKeyBuilder.ForLayer("service1", "layer1");
        builder.Build();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void ForStacCollection_WithoutId_GeneratesCollectionsKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder
            .ForStacCollection()
            .Build();

        // Assert
        Assert.Contains("stac", key);
        Assert.Contains("collections", key);
    }

    [Fact]
    public void ForStacCollection_WithId_GeneratesCollectionKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder
            .ForStacCollection("collection-123")
            .Build();

        // Assert
        Assert.Contains("stac", key);
        Assert.Contains("collection", key);
        Assert.Contains("collection-123", key);
    }
}

/// <summary>
/// Tests for cache invalidation patterns.
/// </summary>
public sealed class CacheInvalidationPatternsTests
{
    [Fact]
    public void ForLayer_GeneratesCorrectPattern()
    {
        // Arrange & Act
        var pattern = CacheInvalidationPatterns.ForLayer("service1", "layer1");

        // Assert
        Assert.Equal("layer:service1:layer1:*", pattern);
    }

    [Fact]
    public void ForTileMatrixSet_GeneratesCorrectPattern()
    {
        // Arrange & Act
        var pattern = CacheInvalidationPatterns.ForTileMatrixSet("WebMercatorQuad");

        // Assert
        Assert.Equal("tile:WebMercatorQuad:*", pattern);
    }

    [Fact]
    public void ForTileZoomLevel_GeneratesCorrectPattern()
    {
        // Arrange & Act
        var pattern = CacheInvalidationPatterns.ForTileZoomLevel("WebMercatorQuad", 5);

        // Assert
        Assert.Equal("tile:WebMercatorQuad:5:*", pattern);
    }

    [Fact]
    public void ForStacCollections_GeneratesCorrectPattern()
    {
        // Arrange & Act
        var pattern = CacheInvalidationPatterns.ForStacCollections();

        // Assert
        Assert.Equal("stac:collections:*", pattern);
    }

    [Fact]
    public void ForStacCollection_GeneratesCorrectPattern()
    {
        // Arrange & Act
        var pattern = CacheInvalidationPatterns.ForStacCollection("collection-123");

        // Assert
        Assert.Equal("stac:collection:collection-123:*", pattern);
    }
}

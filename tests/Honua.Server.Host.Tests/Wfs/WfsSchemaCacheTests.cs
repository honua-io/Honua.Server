using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Host.Wfs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Host.Tests.Wfs;

/// <summary>
/// Unit tests for WFS schema cache implementation.
/// </summary>
public sealed class WfsSchemaCacheTests : IDisposable
{
    private readonly IMemoryCache _memoryCache;

    public WfsSchemaCacheTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1000
        });
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }

    [Fact]
    public void TryGetSchema_WhenCacheDisabled_ReturnsFalse()
    {
        // Arrange
        var options = new WfsOptions { EnableSchemaCaching = false };
        var cache = CreateCache(options);

        // Act
        var result = cache.TryGetSchema("test-collection", out var schema);

        // Assert
        Assert.False(result);
        Assert.Null(schema);
    }

    [Fact]
    public void TryGetSchema_WhenTtlIsZero_ReturnsFalse()
    {
        // Arrange
        var options = new WfsOptions
        {
            EnableSchemaCaching = true,
            DescribeFeatureTypeCacheDuration = 0
        };
        var cache = CreateCache(options);

        // Act
        var result = cache.TryGetSchema("test-collection", out var schema);

        // Assert
        Assert.False(result);
        Assert.Null(schema);
    }

    [Fact]
    public void TryGetSchema_WithNullCollectionId_ReturnsFalse()
    {
        // Arrange
        var cache = CreateCache();

        // Act
        var result = cache.TryGetSchema(null!, out var schema);

        // Assert
        Assert.False(result);
        Assert.Null(schema);
    }

    [Fact]
    public void TryGetSchema_WithWhitespaceCollectionId_ReturnsFalse()
    {
        // Arrange
        var cache = CreateCache();

        // Act
        var result = cache.TryGetSchema("   ", out var schema);

        // Assert
        Assert.False(result);
        Assert.Null(schema);
    }

    [Fact]
    public async Task TryGetSchema_WhenNotCached_ReturnsFalse()
    {
        // Arrange
        var cache = CreateCache();

        // Act
        var result = cache.TryGetSchema("non-existent", out var schema);

        // Assert
        Assert.False(result);
        Assert.Null(schema);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task SetSchemaAsync_ThenTryGetSchema_ReturnsTrue()
    {
        // Arrange
        var cache = CreateCache();
        var testSchema = CreateTestSchema("test-collection");

        // Act
        await cache.SetSchemaAsync("test-collection", testSchema);
        var result = cache.TryGetSchema("test-collection", out var retrievedSchema);

        // Assert
        Assert.True(result);
        Assert.NotNull(retrievedSchema);
        Assert.Equal(testSchema.ToString(), retrievedSchema!.ToString());
    }

    [Fact]
    public async Task SetSchemaAsync_WithNullCollectionId_ThrowsArgumentException()
    {
        // Arrange
        var cache = CreateCache();
        var testSchema = CreateTestSchema("test");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            cache.SetSchemaAsync(null!, testSchema));
    }

    [Fact]
    public async Task SetSchemaAsync_WithNullSchema_ThrowsArgumentNullException()
    {
        // Arrange
        var cache = CreateCache();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            cache.SetSchemaAsync("test-collection", null!));
    }

    [Fact]
    public async Task SetSchemaAsync_WhenCacheDisabled_DoesNotCache()
    {
        // Arrange
        var options = new WfsOptions { EnableSchemaCaching = false };
        var cache = CreateCache(options);
        var testSchema = CreateTestSchema("test-collection");

        // Act
        await cache.SetSchemaAsync("test-collection", testSchema);
        var result = cache.TryGetSchema("test-collection", out var retrievedSchema);

        // Assert
        Assert.False(result);
        Assert.Null(retrievedSchema);
    }

    [Fact]
    public async Task CacheKey_IsCaseInsensitive()
    {
        // Arrange
        var cache = CreateCache();
        var testSchema = CreateTestSchema("test-collection");

        // Act
        await cache.SetSchemaAsync("TEST-COLLECTION", testSchema);
        var result = cache.TryGetSchema("test-collection", out var retrievedSchema);

        // Assert
        Assert.True(result);
        Assert.NotNull(retrievedSchema);
    }

    [Fact]
    public async Task InvalidateSchema_RemovesCachedEntry()
    {
        // Arrange
        var cache = CreateCache();
        var testSchema = CreateTestSchema("test-collection");
        await cache.SetSchemaAsync("test-collection", testSchema);

        // Act
        cache.InvalidateSchema("test-collection");
        var result = cache.TryGetSchema("test-collection", out var retrievedSchema);

        // Assert
        Assert.False(result);
        Assert.Null(retrievedSchema);
    }

    [Fact]
    public void InvalidateSchema_WithNullCollectionId_DoesNotThrow()
    {
        // Arrange
        var cache = CreateCache();

        // Act & Assert - should not throw
        cache.InvalidateSchema(null!);
    }

    [Fact]
    public async Task InvalidateAll_RemovesAllEntries()
    {
        // Arrange
        var cache = CreateCache();
        var schema1 = CreateTestSchema("collection1");
        var schema2 = CreateTestSchema("collection2");
        var schema3 = CreateTestSchema("collection3");

        await cache.SetSchemaAsync("collection1", schema1);
        await cache.SetSchemaAsync("collection2", schema2);
        await cache.SetSchemaAsync("collection3", schema3);

        // Act
        cache.InvalidateAll();

        // Assert
        Assert.False(cache.TryGetSchema("collection1", out _));
        Assert.False(cache.TryGetSchema("collection2", out _));
        Assert.False(cache.TryGetSchema("collection3", out _));
    }

    [Fact]
    public async Task GetStatistics_ReflectsHitsAndMisses()
    {
        // Arrange
        var cache = CreateCache();
        var testSchema = CreateTestSchema("test-collection");
        await cache.SetSchemaAsync("test-collection", testSchema);

        // Act
        _ = cache.TryGetSchema("test-collection", out _); // Hit
        _ = cache.TryGetSchema("test-collection", out _); // Hit
        _ = cache.TryGetSchema("non-existent", out _); // Miss
        _ = cache.TryGetSchema("another-miss", out _); // Miss

        var stats = cache.GetStatistics();

        // Assert
        Assert.Equal(2, stats.Hits);
        Assert.Equal(2, stats.Misses);
        Assert.Equal(0.5, stats.HitRate, precision: 2);
    }

    [Fact]
    public void GetStatistics_WithNoActivity_ReturnsZeros()
    {
        // Arrange
        var cache = CreateCache();

        // Act
        var stats = cache.GetStatistics();

        // Assert
        Assert.Equal(0, stats.Hits);
        Assert.Equal(0, stats.Misses);
        Assert.Equal(0, stats.HitRate);
        Assert.Equal(0, stats.EntryCount);
    }

    [Fact]
    public async Task GetStatistics_EntryCount_ReflectsCachedSchemas()
    {
        // Arrange
        var cache = CreateCache();
        var schema1 = CreateTestSchema("collection1");
        var schema2 = CreateTestSchema("collection2");

        // Act
        await cache.SetSchemaAsync("collection1", schema1);
        await cache.SetSchemaAsync("collection2", schema2);
        var stats = cache.GetStatistics();

        // Assert
        Assert.Equal(2, stats.EntryCount);
    }

    [Fact]
    public async Task ConcurrentAccess_IsSafe()
    {
        // Arrange
        var cache = CreateCache();
        var schema = CreateTestSchema("test-collection");
        await cache.SetSchemaAsync("test-collection", schema);

        // Act - simulate concurrent reads
        var tasks = new Task<bool>[100];
        for (int i = 0; i < 100; i++)
        {
            tasks[i] = Task.Run(() => cache.TryGetSchema("test-collection", out _));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - all should succeed
        Assert.All(results, result => Assert.True(result));
    }

    [Fact]
    public async Task MultipleCollections_AreCachedIndependently()
    {
        // Arrange
        var cache = CreateCache();
        var schema1 = CreateTestSchema("collection1");
        var schema2 = CreateTestSchema("collection2");

        // Act
        await cache.SetSchemaAsync("collection1", schema1);
        await cache.SetSchemaAsync("collection2", schema2);

        // Assert
        Assert.True(cache.TryGetSchema("collection1", out var retrieved1));
        Assert.True(cache.TryGetSchema("collection2", out var retrieved2));
        Assert.NotEqual(retrieved1!.ToString(), retrieved2!.ToString());
    }

    [Fact]
    public async Task InvalidateSchema_OnlyAffectsSpecifiedCollection()
    {
        // Arrange
        var cache = CreateCache();
        var schema1 = CreateTestSchema("collection1");
        var schema2 = CreateTestSchema("collection2");

        await cache.SetSchemaAsync("collection1", schema1);
        await cache.SetSchemaAsync("collection2", schema2);

        // Act
        cache.InvalidateSchema("collection1");

        // Assert
        Assert.False(cache.TryGetSchema("collection1", out _));
        Assert.True(cache.TryGetSchema("collection2", out _));
    }

    [Fact]
    public async Task SchemaExpiration_RespectsTtl()
    {
        // Arrange - use very short TTL
        var options = new WfsOptions
        {
            EnableSchemaCaching = true,
            DescribeFeatureTypeCacheDuration = 1 // 1 second
        };
        var cache = CreateCache(options);
        var schema = CreateTestSchema("test-collection");

        // Act
        await cache.SetSchemaAsync("test-collection", schema);
        Assert.True(cache.TryGetSchema("test-collection", out _));

        // Wait for expiration
        await Task.Delay(1500);

        // Assert - should be expired
        Assert.False(cache.TryGetSchema("test-collection", out _));
    }

    [Fact]
    public async Task SetSchemaAsync_UpdatesExistingEntry()
    {
        // Arrange
        var cache = CreateCache();
        var schema1 = CreateTestSchema("collection", "version1");
        var schema2 = CreateTestSchema("collection", "version2");

        // Act
        await cache.SetSchemaAsync("collection", schema1);
        await cache.SetSchemaAsync("collection", schema2); // Update

        // Assert
        Assert.True(cache.TryGetSchema("collection", out var retrieved));
        Assert.Contains("version2", retrieved!.ToString());
        Assert.DoesNotContain("version1", retrieved.ToString());
    }

    #region Helper Methods

    private WfsSchemaCache CreateCache(WfsOptions? options = null)
    {
        options ??= new WfsOptions
        {
            EnableSchemaCaching = true,
            DescribeFeatureTypeCacheDuration = 3600
        };

        var optionsMonitor = new OptionsWrapper<WfsOptions>(options);
        return new WfsSchemaCache(_memoryCache, NullLogger<WfsSchemaCache>.Instance, optionsMonitor);
    }

    private static XDocument CreateTestSchema(string collectionId, string version = "1.0")
    {
        return new XDocument(
            new XElement("schema",
                new XAttribute("targetNamespace", $"https://honua.dev/wfs/{collectionId}"),
                new XAttribute("version", version),
                new XElement("complexType",
                    new XAttribute("name", collectionId),
                    new XElement("sequence",
                        new XElement("element",
                            new XAttribute("name", "id"),
                            new XAttribute("type", "xs:int")),
                        new XElement("element",
                            new XAttribute("name", "geometry"),
                            new XAttribute("type", "gml:GeometryPropertyType"))))));
    }

    #endregion
}

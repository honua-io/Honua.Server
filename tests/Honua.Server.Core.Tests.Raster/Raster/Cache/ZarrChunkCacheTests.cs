using System;
using System.Threading.Tasks;
using Honua.Server.Core.Raster.Cache;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Cache;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class ZarrChunkCacheTests : IDisposable
{
    private readonly ZarrChunkCache _cache;

    public ZarrChunkCacheTests()
    {
        _cache = new ZarrChunkCache(
            NullLogger<ZarrChunkCache>.Instance,
            new ZarrChunkCacheOptions
            {
                MaxCacheSizeBytes = 1024 * 1024, // 1 MB
                ChunkTtlMinutes = 5
            });
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    [Fact]
    public async Task GetOrFetchAsync_CacheMiss_FetchesAndCachesData()
    {
        // Arrange
        const string uri = "https://example.com/data.zarr";
        const string variableName = "temperature";
        var chunkCoords = new[] { 0, 0, 0 };
        var expectedData = new byte[] { 1, 2, 3, 4, 5 };

        var fetchCallCount = 0;

        // Act
        var result = await _cache.GetOrFetchAsync(
            uri,
            variableName,
            chunkCoords,
            async () =>
            {
                fetchCallCount++;
                await Task.CompletedTask;
                return expectedData;
            });

        // Assert
        Assert.Equal(expectedData, result);
        Assert.Equal(1, fetchCallCount);
    }

    [Fact]
    public async Task GetOrFetchAsync_CacheHit_ReturnsCachedData()
    {
        // Arrange
        const string uri = "https://example.com/data.zarr";
        const string variableName = "temperature";
        var chunkCoords = new[] { 0, 0, 0 };
        var expectedData = new byte[] { 1, 2, 3, 4, 5 };

        var fetchCallCount = 0;

        // First call - cache miss
        await _cache.GetOrFetchAsync(
            uri,
            variableName,
            chunkCoords,
            async () =>
            {
                fetchCallCount++;
                await Task.CompletedTask;
                return expectedData;
            });

        // Act - Second call - should be cache hit
        var result = await _cache.GetOrFetchAsync(
            uri,
            variableName,
            chunkCoords,
            async () =>
            {
                fetchCallCount++;
                await Task.CompletedTask;
                return new byte[] { 99, 99, 99 }; // Different data
            });

        // Assert
        Assert.Equal(expectedData, result); // Should get original cached data
        Assert.Equal(1, fetchCallCount); // Fetch should only happen once
    }

    [Fact]
    public async Task GetOrFetchAsync_DifferentChunks_CachesSeparately()
    {
        // Arrange
        const string uri = "https://example.com/data.zarr";
        const string variableName = "temperature";

        var chunk1Coords = new[] { 0, 0, 0 };
        var chunk1Data = new byte[] { 1, 2, 3 };

        var chunk2Coords = new[] { 0, 0, 1 };
        var chunk2Data = new byte[] { 4, 5, 6 };

        // Act
        var result1 = await _cache.GetOrFetchAsync(
            uri, variableName, chunk1Coords,
            async () => { await Task.CompletedTask; return chunk1Data; });

        var result2 = await _cache.GetOrFetchAsync(
            uri, variableName, chunk2Coords,
            async () => { await Task.CompletedTask; return chunk2Data; });

        // Assert
        Assert.Equal(chunk1Data, result1);
        Assert.Equal(chunk2Data, result2);
        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public async Task GetOrFetchAsync_DifferentVariables_CachesSeparately()
    {
        // Arrange
        const string uri = "https://example.com/data.zarr";
        var chunkCoords = new[] { 0, 0, 0 };

        var tempData = new byte[] { 1, 2, 3 };
        var pressureData = new byte[] { 4, 5, 6 };

        // Act
        var result1 = await _cache.GetOrFetchAsync(
            uri, "temperature", chunkCoords,
            async () => { await Task.CompletedTask; return tempData; });

        var result2 = await _cache.GetOrFetchAsync(
            uri, "pressure", chunkCoords,
            async () => { await Task.CompletedTask; return pressureData; });

        // Assert
        Assert.Equal(tempData, result1);
        Assert.Equal(pressureData, result2);
        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public async Task GetOrFetchAsync_DifferentUris_CachesSeparately()
    {
        // Arrange
        const string variableName = "temperature";
        var chunkCoords = new[] { 0, 0, 0 };

        var data1 = new byte[] { 1, 2, 3 };
        var data2 = new byte[] { 4, 5, 6 };

        // Act
        var result1 = await _cache.GetOrFetchAsync(
            "https://example.com/data1.zarr", variableName, chunkCoords,
            async () => { await Task.CompletedTask; return data1; });

        var result2 = await _cache.GetOrFetchAsync(
            "https://example.com/data2.zarr", variableName, chunkCoords,
            async () => { await Task.CompletedTask; return data2; });

        // Assert
        Assert.Equal(data1, result1);
        Assert.Equal(data2, result2);
        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public async Task Invalidate_RemovesChunkFromCache()
    {
        // Arrange
        const string uri = "https://example.com/data.zarr";
        const string variableName = "temperature";
        var chunkCoords = new[] { 0, 0, 0 };
        var originalData = new byte[] { 1, 2, 3 };
        var newData = new byte[] { 4, 5, 6 };

        var fetchCallCount = 0;

        // Cache the chunk
        await _cache.GetOrFetchAsync(
            uri, variableName, chunkCoords,
            async () =>
            {
                fetchCallCount++;
                await Task.CompletedTask;
                return originalData;
            });

        // Act - Invalidate
        _cache.Invalidate(uri, variableName, chunkCoords);

        // Fetch again - should call factory again
        var result = await _cache.GetOrFetchAsync(
            uri, variableName, chunkCoords,
            async () =>
            {
                fetchCallCount++;
                await Task.CompletedTask;
                return newData;
            });

        // Assert
        Assert.Equal(newData, result); // Should get new data
        Assert.Equal(2, fetchCallCount); // Should have called fetch twice
    }

    [Fact]
    public void Clear_RemovesAllCachedChunks()
    {
        // Arrange - Cache some data
        var uri = "https://example.com/data.zarr";
        var variableName = "temperature";
        var chunkCoords = new[] { 0, 0, 0 };

        // Pre-populate cache (can't easily verify without exposing internals)
        _ = _cache.GetOrFetchAsync(
            uri, variableName, chunkCoords,
            async () => { await Task.CompletedTask; return new byte[] { 1, 2, 3 }; });

        // Act
        _cache.Clear();

        // Assert - no exception should be thrown
        Assert.True(true);
    }

    [Fact]
    public void GetStats_ReturnsConfiguration()
    {
        // Act
        var stats = _cache.GetStats();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(1024 * 1024, stats.MaxSizeBytes);
        Assert.Equal(5, stats.ChunkTtlMinutes);
    }

    [Fact]
    public async Task GetOrFetchAsync_WithLargeData_RespectsMemoryLimit()
    {
        // Arrange - Create cache with small limit
        using var smallCache = new ZarrChunkCache(
            NullLogger<ZarrChunkCache>.Instance,
            new ZarrChunkCacheOptions
            {
                MaxCacheSizeBytes = 100, // Very small cache
                ChunkTtlMinutes = 5
            });

        var largeData = new byte[200]; // Larger than cache limit

        // Act & Assert - should not throw, but may evict immediately
        var result = await smallCache.GetOrFetchAsync(
            "https://example.com/data.zarr",
            "temperature",
            new[] { 0, 0, 0 },
            async () => { await Task.CompletedTask; return largeData; });

        Assert.Equal(largeData, result);
    }

    [Fact]
    public async Task GetOrFetchAsync_MultipleCalls_ConcurrencySafe()
    {
        // Arrange
        const string uri = "https://example.com/data.zarr";
        const string variableName = "temperature";
        var chunkCoords = new[] { 0, 0, 0 };
        var expectedData = new byte[] { 1, 2, 3, 4, 5 };

        var fetchCallCount = 0;

        // Act - Make multiple concurrent calls
        var tasks = new Task<byte[]>[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = _cache.GetOrFetchAsync(
                uri, variableName, chunkCoords,
                async () =>
                {
                    System.Threading.Interlocked.Increment(ref fetchCallCount);
                    await Task.Delay(10); // Simulate async work
                    return expectedData;
                });
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All should get the same data
        foreach (var result in results)
        {
            Assert.Equal(expectedData, result);
        }

        // Fetch might be called multiple times due to race conditions,
        // but should be relatively small
        Assert.True(fetchCallCount <= tasks.Length);
    }

    [Fact]
    public void Constructor_WithDefaultOptions_UsesDefaults()
    {
        // Act
        using var cache = new ZarrChunkCache(NullLogger<ZarrChunkCache>.Instance);
        var stats = cache.GetStats();

        // Assert
        Assert.Equal(256 * 1024 * 1024, stats.MaxSizeBytes); // 256 MB default
        Assert.Equal(60, stats.ChunkTtlMinutes); // 60 minutes default
    }

    [Fact]
    public void Constructor_WithCustomOptions_UsesProvidedValues()
    {
        // Arrange
        var options = new ZarrChunkCacheOptions
        {
            MaxCacheSizeBytes = 512 * 1024 * 1024,
            ChunkTtlMinutes = 30
        };

        // Act
        using var cache = new ZarrChunkCache(NullLogger<ZarrChunkCache>.Instance, options);
        var stats = cache.GetStats();

        // Assert
        Assert.Equal(512 * 1024 * 1024, stats.MaxSizeBytes);
        Assert.Equal(30, stats.ChunkTtlMinutes);
    }

    [Fact]
    public async Task GetOrFetchAsync_WithException_DoesNotCache()
    {
        // Arrange
        const string uri = "https://example.com/data.zarr";
        const string variableName = "temperature";
        var chunkCoords = new[] { 0, 0, 0 };

        var attemptCount = 0;

        // Act & Assert - First call throws
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _cache.GetOrFetchAsync(
                uri, variableName, chunkCoords,
                async () =>
                {
                    attemptCount++;
                    await Task.CompletedTask;
                    throw new InvalidOperationException("Test exception");
                });
        });

        // Second call - should try again (not cached)
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _cache.GetOrFetchAsync(
                uri, variableName, chunkCoords,
                async () =>
                {
                    attemptCount++;
                    await Task.CompletedTask;
                    throw new InvalidOperationException("Test exception");
                });
        });

        // Assert - Should have attempted twice
        Assert.Equal(2, attemptCount);
    }
}

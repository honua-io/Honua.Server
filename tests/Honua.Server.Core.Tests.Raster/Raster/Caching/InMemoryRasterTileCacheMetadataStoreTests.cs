using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Raster.Caching;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Caching;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class InMemoryRasterTileCacheMetadataStoreTests
{
    private readonly InMemoryRasterTileCacheMetadataStore _store;

    public InMemoryRasterTileCacheMetadataStoreTests()
    {
        _store = new InMemoryRasterTileCacheMetadataStore();
    }

    [Fact]
    public async Task RecordTileCreationAsync_ShouldCreateMetadata()
    {
        var key = CreateCacheKey("test-dataset", 5, 10, 20);

        await _store.RecordTileCreationAsync(key, 2048);

        var metadata = await _store.GetMetadataAsync(key);
        metadata.Should().NotBeNull();
        metadata!.DatasetId.Should().Be("test-dataset");
        metadata.TileMatrixSetId.Should().Be("WebMercatorQuad");
        metadata.ZoomLevel.Should().Be(5);
        metadata.TileRow.Should().Be(10);
        metadata.TileCol.Should().Be(20);
        metadata.SizeBytes.Should().Be(2048);
        metadata.AccessCount.Should().Be(0);
    }

    [Fact]
    public async Task RecordTileAccessAsync_ShouldIncrementAccessCount()
    {
        var key = CreateCacheKey("test-dataset", 0, 0, 0);
        await _store.RecordTileCreationAsync(key, 1024);

        await _store.RecordTileAccessAsync(key);
        await _store.RecordTileAccessAsync(key);

        var metadata = await _store.GetMetadataAsync(key);
        metadata!.AccessCount.Should().Be(2);
    }

    [Fact]
    public async Task RecordTileAccessAsync_ShouldUpdateLastAccessedTime()
    {
        var key = CreateCacheKey("test-dataset", 0, 0, 0);
        await _store.RecordTileCreationAsync(key, 1024);

        var beforeAccess = DateTimeOffset.UtcNow;
        await Task.Delay(10); // Small delay to ensure time difference
        await _store.RecordTileAccessAsync(key);

        var metadata = await _store.GetMetadataAsync(key);
        metadata!.LastAccessedUtc.Should().BeAfter(beforeAccess);
    }

    [Fact]
    public async Task RecordTileRemovalAsync_ShouldRemoveMetadata()
    {
        var key = CreateCacheKey("test-dataset", 0, 0, 0);
        await _store.RecordTileCreationAsync(key, 1024);

        await _store.RecordTileRemovalAsync(key);

        var metadata = await _store.GetMetadataAsync(key);
        metadata.Should().BeNull();
    }

    [Fact]
    public async Task GetDatasetMetadataAsync_ShouldReturnZeroForEmptyDataset()
    {
        var metadata = await _store.GetDatasetMetadataAsync("empty-dataset");

        metadata.DatasetId.Should().Be("empty-dataset");
        metadata.TotalTiles.Should().Be(0);
        metadata.TotalSizeBytes.Should().Be(0);
        metadata.OldestTileUtc.Should().BeNull();
        metadata.NewestTileUtc.Should().BeNull();
    }

    [Fact]
    public async Task GetDatasetMetadataAsync_ShouldAggregateMultipleTiles()
    {
        var key1 = CreateCacheKey("test-dataset", 0, 0, 0);
        var key2 = CreateCacheKey("test-dataset", 1, 0, 0);
        var key3 = CreateCacheKey("test-dataset", 2, 0, 0);

        await _store.RecordTileCreationAsync(key1, 1024);
        await Task.Delay(10);
        await _store.RecordTileCreationAsync(key2, 2048);
        await Task.Delay(10);
        await _store.RecordTileCreationAsync(key3, 512);

        var metadata = await _store.GetDatasetMetadataAsync("test-dataset");

        metadata.TotalTiles.Should().Be(3);
        metadata.TotalSizeBytes.Should().Be(3584);
        metadata.MinZoomLevel.Should().Be(0);
        metadata.MaxZoomLevel.Should().Be(2);
        metadata.OldestTileUtc.Should().NotBeNull();
        metadata.NewestTileUtc.Should().NotBeNull();
        metadata.NewestTileUtc.Should().BeAfter(metadata.OldestTileUtc!.Value);
    }

    [Fact]
    public async Task GetDatasetMetadataAsync_ShouldOnlyIncludeSpecifiedDataset()
    {
        var key1 = CreateCacheKey("dataset-1", 0, 0, 0);
        var key2 = CreateCacheKey("dataset-2", 0, 0, 0);

        await _store.RecordTileCreationAsync(key1, 1024);
        await _store.RecordTileCreationAsync(key2, 2048);

        var metadata = await _store.GetDatasetMetadataAsync("dataset-1");

        metadata.TotalTiles.Should().Be(1);
        metadata.TotalSizeBytes.Should().Be(1024);
    }

    [Fact]
    public async Task GetMetadataAsync_ShouldReturnNullForNonexistentTile()
    {
        var key = CreateCacheKey("test-dataset", 0, 0, 0);

        var metadata = await _store.GetMetadataAsync(key);

        metadata.Should().BeNull();
    }

    [Fact]
    public async Task RecordTileCreationAsync_ShouldSetCreatedAndLastAccessedToSameTime()
    {
        var key = CreateCacheKey("test-dataset", 0, 0, 0);

        await _store.RecordTileCreationAsync(key, 1024);

        var metadata = await _store.GetMetadataAsync(key);
        metadata!.CreatedUtc.Should().Be(metadata.LastAccessedUtc);
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldNotLoseAccessCounts()
    {
        // Arrange
        var key = CreateCacheKey("test-dataset", 0, 0, 0);
        await _store.RecordTileCreationAsync(key, 1024);

        // Act - Multiple concurrent accesses
        const int concurrentAccesses = 1000;
        var tasks = Enumerable.Range(0, concurrentAccesses)
            .Select(_ => Task.Run(async () => await _store.RecordTileAccessAsync(key)))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert - All access counts should be recorded
        var metadata = await _store.GetMetadataAsync(key);
        metadata!.AccessCount.Should().Be(concurrentAccesses,
            "atomic operations should not lose any concurrent updates");
    }

    [Fact]
    public async Task ConcurrentCreationAndAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var keys = Enumerable.Range(0, 10)
            .Select(i => CreateCacheKey($"dataset-{i}", i, i, i))
            .ToArray();

        // Act - Concurrent creation and access operations
        var creationTasks = keys.Select(key =>
            Task.Run(async () => await _store.RecordTileCreationAsync(key, 1024)));

        var accessTasks = keys.Select(key =>
            Task.Run(async () =>
            {
                // Wait a tiny bit to increase chance of concurrent access
                await Task.Delay(1);
                try
                {
                    await _store.RecordTileAccessAsync(key);
                }
                catch (InvalidOperationException)
                {
                    // Expected if tile hasn't been created yet
                }
            }));

        var allTasks = creationTasks.Concat(accessTasks).ToArray();
        await Task.WhenAll(allTasks);

        // Assert - All tiles should be created
        foreach (var key in keys)
        {
            var metadata = await _store.GetMetadataAsync(key);
            metadata.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ConcurrentRemoval_ShouldNotThrow()
    {
        // Arrange
        var keys = Enumerable.Range(0, 5)
            .Select(i => CreateCacheKey($"dataset-{i}", 0, 0, i))
            .ToArray();

        foreach (var key in keys)
        {
            await _store.RecordTileCreationAsync(key, 1024);
        }

        // Act - Concurrent removals (some may be duplicate)
        var tasks = keys
            .SelectMany(key => new[] { key, key }) // Duplicate each key to test concurrent removal
            .Select(key => Task.Run(async () => await _store.RecordTileRemovalAsync(key)))
            .ToArray();

        // Should not throw
        var act = async () => await Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();

        // Assert - All tiles should be removed
        foreach (var key in keys)
        {
            var metadata = await _store.GetMetadataAsync(key);
            metadata.Should().BeNull();
        }
    }

    [Fact]
    public async Task HighConcurrentLoad_ShouldMaintainDataIntegrity()
    {
        // Arrange
        const int datasetCount = 20;
        const int accessesPerDataset = 50;

        var keys = Enumerable.Range(0, datasetCount)
            .Select(i => CreateCacheKey("stress-dataset", i % 5, i, i))
            .ToArray();

        // Create all tiles first
        foreach (var key in keys)
        {
            await _store.RecordTileCreationAsync(key, 1024 * (Array.IndexOf(keys, key) + 1));
        }

        // Act - High concurrent load
        var accessTasks = keys.SelectMany(key =>
            Enumerable.Range(0, accessesPerDataset)
                .Select(_ => Task.Run(async () => await _store.RecordTileAccessAsync(key)))
        ).ToArray();

        await Task.WhenAll(accessTasks);

        // Assert - All access counts should be correct
        foreach (var key in keys)
        {
            var metadata = await _store.GetMetadataAsync(key);
            metadata.Should().NotBeNull();
            metadata!.AccessCount.Should().Be(accessesPerDataset);
        }

        // Verify dataset metadata aggregation still works correctly
        var datasetMetadata = await _store.GetDatasetMetadataAsync("stress-dataset");
        datasetMetadata.TotalTiles.Should().Be(datasetCount);
        datasetMetadata.TotalSizeBytes.Should().BeGreaterThan(0);
    }

    private static RasterTileCacheKey CreateCacheKey(string datasetId, int zoom, int row, int column)
    {
        return new RasterTileCacheKey(
            datasetId: datasetId,
            tileMatrixSetId: "WebMercatorQuad",
            zoom: zoom,
            row: row,
            column: column,
            styleId: "default",
            format: "image/png",
            transparent: true,
            tileSize: 256);
    }
}

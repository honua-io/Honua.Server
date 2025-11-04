using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Raster.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Caching;

/// <summary>
/// Integration tests for the complete disk quota system including
/// DiskQuotaService, RasterTileCacheDiskQuotaService, and FileSystemRasterTileCacheProvider.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class DiskQuotaIntegrationTests : IDisposable
{
    private readonly string _tempPath;
    private readonly InMemoryRasterTileCacheMetadataStore _metadataStore;
    private readonly DiskQuotaMetrics _quotaMetrics;
    private readonly FileSystemRasterTileCacheProvider _fileSystemProvider;
    private readonly DiskQuotaService _diskQuotaService;
    private readonly RasterTileCacheDiskQuotaService _tileCacheQuotaService;

    public DiskQuotaIntegrationTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"honua-integration-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempPath);

        _metadataStore = new InMemoryRasterTileCacheMetadataStore();
        _quotaMetrics = new DiskQuotaMetrics();

        var diskQuotaOptions = new DiskQuotaOptions
        {
            MaxDiskUsagePercent = 0.95,
            MinimumFreeSpaceBytes = 1024,
            EnableAutomaticCleanup = true,
            EvictionPolicy = QuotaExpirationPolicy.LeastRecentlyUsed
        };

        _diskQuotaService = new DiskQuotaService(
            NullLogger<DiskQuotaService>.Instance,
            _metadataStore,
            NullRasterTileCacheProvider.Instance,
            diskQuotaOptions,
            _quotaMetrics);

        _fileSystemProvider = new FileSystemRasterTileCacheProvider(
            _tempPath,
            NullLogger<FileSystemRasterTileCacheProvider>.Instance,
            _diskQuotaService,
            _metadataStore,
            _quotaMetrics);

        _tileCacheQuotaService = new RasterTileCacheDiskQuotaService(
            _metadataStore,
            _fileSystemProvider,
            NullLogger<RasterTileCacheDiskQuotaService>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempPath))
            {
                Directory.Delete(_tempPath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }

        _quotaMetrics?.Dispose();
    }

    [Fact]
    public async Task EndToEnd_ShouldTrackTileCreationAndAccess()
    {
        // Arrange
        var key = CreateTestKey("dataset1", 0, 0, 0);
        var entry = CreateTestEntry(2048);

        // Act - Store tile
        await _fileSystemProvider.StoreAsync(key, entry);

        // Access tile twice
        await _fileSystemProvider.TryGetAsync(key);
        await _fileSystemProvider.TryGetAsync(key);

        // Assert - Check metadata was recorded
        var metadata = await _metadataStore.GetMetadataAsync(key);
        metadata.Should().NotBeNull();
        metadata!.SizeBytes.Should().Be(2048);
        metadata.AccessCount.Should().Be(2);
        metadata.DatasetId.Should().Be("dataset1");
    }

    [Fact]
    public async Task EndToEnd_ShouldEnforceDatasetQuota_WithLRUEviction()
    {
        // Arrange - Set a small quota
        var quota = new DiskQuotaConfiguration(
            MaxSizeBytes: 5000,
            ExpirationPolicy: QuotaExpirationPolicy.LeastRecentlyUsed);

        await _tileCacheQuotaService.UpdateQuotaAsync("test-dataset", quota);

        // Create tiles that exceed quota
        var keys = Enumerable.Range(0, 5)
            .Select(i => CreateTestKey("test-dataset", 0, 0, i))
            .ToList();

        foreach (var key in keys)
        {
            await _fileSystemProvider.StoreAsync(key, CreateTestEntry(1500));
            await Task.Delay(10); // Ensure different timestamps
        }

        // Act - Enforce quota
        var result = await _tileCacheQuotaService.EnforceQuotaAsync("test-dataset");

        // Assert - Some tiles should have been removed
        var finalStatus = await _tileCacheQuotaService.GetQuotaStatusAsync("test-dataset");
        finalStatus.CurrentSizeBytes.Should().BeLessThanOrEqualTo(quota.MaxSizeBytes);
    }

    [Fact]
    public async Task EndToEnd_ShouldCheckDiskSpaceBeforeWrite()
    {
        // Arrange
        var key = CreateTestKey("dataset2", 0, 0, 0);
        var entry = CreateTestEntry(1024);

        // Act
        await _fileSystemProvider.StoreAsync(key, entry);

        // Assert - File should exist
        var result = await _fileSystemProvider.TryGetAsync(key);
        result.Should().NotBeNull();
        result!.Value.Content.Length.Should().Be(1024);
    }

    [Fact]
    public async Task EndToEnd_ShouldGetQuotaStatus_ForDataset()
    {
        // Arrange
        var keys = Enumerable.Range(0, 3)
            .Select(i => CreateTestKey("dataset3", 0, 0, i))
            .ToList();

        foreach (var key in keys)
        {
            await _fileSystemProvider.StoreAsync(key, CreateTestEntry(1024));
        }

        // Act
        var status = await _tileCacheQuotaService.GetQuotaStatusAsync("dataset3");

        // Assert
        status.Should().NotBeNull();
        status.DatasetId.Should().Be("dataset3");
        status.TileCount.Should().Be(3);
        status.CurrentSizeBytes.Should().Be(3072);
    }

    [Fact]
    public async Task EndToEnd_ShouldRemoveTilesAndUpdateMetadata()
    {
        // Arrange
        var key = CreateTestKey("dataset4", 0, 0, 0);
        await _fileSystemProvider.StoreAsync(key, CreateTestEntry(1024));

        var metadataBeforeRemove = await _metadataStore.GetMetadataAsync(key);
        metadataBeforeRemove.Should().NotBeNull();

        // Act
        await _fileSystemProvider.RemoveAsync(key);

        // Assert
        var metadataAfterRemove = await _metadataStore.GetMetadataAsync(key);
        metadataAfterRemove.Should().BeNull();

        var result = await _fileSystemProvider.TryGetAsync(key);
        result.Should().BeNull();
    }

    [Fact]
    public async Task EndToEnd_ShouldGetAllQuotas()
    {
        // Arrange
        await _tileCacheQuotaService.UpdateQuotaAsync("dataset-a", new DiskQuotaConfiguration(1000));
        await _tileCacheQuotaService.UpdateQuotaAsync("dataset-b", new DiskQuotaConfiguration(2000));
        await _tileCacheQuotaService.UpdateQuotaAsync("dataset-c", new DiskQuotaConfiguration(3000));

        // Act
        var quotas = await _tileCacheQuotaService.GetAllQuotasAsync();

        // Assert
        quotas.Should().HaveCount(3);
        quotas.Should().ContainKey("dataset-a");
        quotas.Should().ContainKey("dataset-b");
        quotas.Should().ContainKey("dataset-c");
        quotas["dataset-a"].MaxSizeBytes.Should().Be(1000);
    }

    [Fact]
    public async Task EndToEnd_ShouldPurgeDatasetAndClearMetadata()
    {
        // Arrange
        var keys = Enumerable.Range(0, 5)
            .Select(i => CreateTestKey("dataset5", 0, 0, i))
            .ToList();

        foreach (var key in keys)
        {
            await _fileSystemProvider.StoreAsync(key, CreateTestEntry(1024));
        }

        var statusBefore = await _tileCacheQuotaService.GetQuotaStatusAsync("dataset5");
        statusBefore.TileCount.Should().Be(5);

        // Act
        await _fileSystemProvider.PurgeDatasetAsync("dataset5");

        // Assert
        foreach (var key in keys)
        {
            var result = await _fileSystemProvider.TryGetAsync(key);
            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task EndToEnd_ShouldGetDiskSpaceStatus()
    {
        // Act
        var status = await _diskQuotaService.GetDiskSpaceStatusAsync(_tempPath);

        // Assert
        status.Should().NotBeNull();
        status.TotalBytes.Should().BeGreaterThan(0);
        status.FreeBytes.Should().BeGreaterThanOrEqualTo(0);
        status.UsedBytes.Should().BeGreaterThanOrEqualTo(0);
        status.UsagePercent.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public async Task EndToEnd_ShouldFreeUpSpaceWithCleanup()
    {
        // Arrange - Create multiple test files
        var keys = Enumerable.Range(0, 10)
            .Select(i => CreateTestKey("dataset6", 0, 0, i))
            .ToList();

        foreach (var key in keys)
        {
            await _fileSystemProvider.StoreAsync(key, CreateTestEntry(1024));
            await Task.Delay(5); // Ensure different timestamps
        }

        // Act - Free up space
        var statusBefore = await _diskQuotaService.GetDiskSpaceStatusAsync(_tempPath);
        var targetFreeSpace = statusBefore.FreeBytes + 5000; // Free up ~5 files worth

        var cleanupResult = await _diskQuotaService.FreeUpSpaceAsync(_tempPath, targetFreeSpace);

        // Assert
        cleanupResult.FilesRemoved.Should().BeGreaterThan(0);
        cleanupResult.BytesFreed.Should().BeGreaterThan(0);
        cleanupResult.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task EndToEnd_ShouldHandleMultipleDatasets()
    {
        // Arrange
        var dataset1Keys = Enumerable.Range(0, 3)
            .Select(i => CreateTestKey("multi-dataset-1", 0, 0, i))
            .ToList();

        var dataset2Keys = Enumerable.Range(0, 3)
            .Select(i => CreateTestKey("multi-dataset-2", 0, 0, i))
            .ToList();

        foreach (var key in dataset1Keys.Concat(dataset2Keys))
        {
            await _fileSystemProvider.StoreAsync(key, CreateTestEntry(1024));
        }

        // Act
        var status1 = await _tileCacheQuotaService.GetQuotaStatusAsync("multi-dataset-1");
        var status2 = await _tileCacheQuotaService.GetQuotaStatusAsync("multi-dataset-2");

        // Assert
        status1.TileCount.Should().Be(3);
        status2.TileCount.Should().Be(3);
        status1.CurrentSizeBytes.Should().Be(3072);
        status2.CurrentSizeBytes.Should().Be(3072);
    }

    [Fact]
    public async Task EndToEnd_ShouldRespectEvictionPolicy_LRU()
    {
        // Arrange
        var quota = new DiskQuotaConfiguration(
            MaxSizeBytes: 3000,
            ExpirationPolicy: QuotaExpirationPolicy.LeastRecentlyUsed);

        await _tileCacheQuotaService.UpdateQuotaAsync("lru-test", quota);

        var key1 = CreateTestKey("lru-test", 0, 0, 0);
        var key2 = CreateTestKey("lru-test", 0, 0, 1);
        var key3 = CreateTestKey("lru-test", 0, 0, 2);

        await _fileSystemProvider.StoreAsync(key1, CreateTestEntry(1200));
        await Task.Delay(20);
        await _fileSystemProvider.StoreAsync(key2, CreateTestEntry(1200));
        await Task.Delay(20);
        await _fileSystemProvider.StoreAsync(key3, CreateTestEntry(1200));

        // Access key1 to make it recently used
        await _fileSystemProvider.TryGetAsync(key1);

        // Act - Enforce quota, which should remove key2 (least recently accessed)
        var result = await _tileCacheQuotaService.EnforceQuotaAsync("lru-test");

        // Assert
        result.TilesRemoved.Should().BeGreaterThan(0);
        var finalStatus = await _tileCacheQuotaService.GetQuotaStatusAsync("lru-test");
        finalStatus.CurrentSizeBytes.Should().BeLessThanOrEqualTo(quota.MaxSizeBytes);
    }

    private RasterTileCacheKey CreateTestKey(string datasetId, int zoom, int row, int col)
    {
        return new RasterTileCacheKey(
            datasetId: datasetId,
            tileMatrixSetId: "WebMercatorQuad",
            zoom: zoom,
            row: row,
            column: col,
            styleId: "default",
            format: "image/png",
            transparent: true,
            tileSize: 256);
    }

    private RasterTileCacheEntry CreateTestEntry(int size)
    {
        var data = new byte[size];
        return new RasterTileCacheEntry(
            content: data,
            contentType: "image/png",
            createdUtc: DateTimeOffset.UtcNow);
    }
}

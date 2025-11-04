using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Raster.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Caching;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class FileSystemRasterTileCacheProviderDiskQuotaTests : IDisposable
{
    private readonly string _tempPath;
    private readonly InMemoryRasterTileCacheMetadataStore _metadataStore;
    private readonly DiskQuotaMetrics _quotaMetrics;

    public FileSystemRasterTileCacheProviderDiskQuotaTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"honua-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempPath);
        _metadataStore = new InMemoryRasterTileCacheMetadataStore();
        _quotaMetrics = new DiskQuotaMetrics();
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
    public async Task StoreAsync_ShouldStoreFile_WhenSufficientSpaceAvailable()
    {
        // Arrange
        var diskQuotaService = CreateDiskQuotaService();
        var provider = new FileSystemRasterTileCacheProvider(
            _tempPath,
            NullLogger<FileSystemRasterTileCacheProvider>.Instance,
            diskQuotaService,
            _metadataStore,
            _quotaMetrics);

        var key = CreateTestKey();
        var entry = CreateTestEntry(1024);

        // Act
        await provider.StoreAsync(key, entry);

        // Assert
        var path = GetExpectedPath(key);
        File.Exists(path).Should().BeTrue();

        var metadata = await _metadataStore.GetMetadataAsync(key);
        metadata.Should().NotBeNull();
        metadata!.SizeBytes.Should().Be(1024);
    }

    [Fact]
    public async Task StoreAsync_ShouldRecordMetadata_WhenMetadataStoreProvided()
    {
        // Arrange
        var diskQuotaService = CreateDiskQuotaService();
        var provider = new FileSystemRasterTileCacheProvider(
            _tempPath,
            NullLogger<FileSystemRasterTileCacheProvider>.Instance,
            diskQuotaService,
            _metadataStore,
            _quotaMetrics);

        var key = CreateTestKey();
        var entry = CreateTestEntry(2048);

        // Act
        await provider.StoreAsync(key, entry);

        // Assert
        var metadata = await _metadataStore.GetMetadataAsync(key);
        metadata.Should().NotBeNull();
        metadata!.DatasetId.Should().Be(key.DatasetId);
        metadata.SizeBytes.Should().Be(2048);
        metadata.AccessCount.Should().Be(0);
    }

    [Fact]
    public async Task TryGetAsync_ShouldRecordAccess_WhenMetadataStoreProvided()
    {
        // Arrange
        var diskQuotaService = CreateDiskQuotaService();
        var provider = new FileSystemRasterTileCacheProvider(
            _tempPath,
            NullLogger<FileSystemRasterTileCacheProvider>.Instance,
            diskQuotaService,
            _metadataStore,
            _quotaMetrics);

        var key = CreateTestKey();
        var entry = CreateTestEntry(1024);

        await provider.StoreAsync(key, entry);

        // Act
        var hit1 = await provider.TryGetAsync(key);
        var hit2 = await provider.TryGetAsync(key);

        // Assert
        hit1.Should().NotBeNull();
        hit2.Should().NotBeNull();

        var metadata = await _metadataStore.GetMetadataAsync(key);
        metadata.Should().NotBeNull();
        metadata!.AccessCount.Should().Be(2);
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveMetadata_WhenMetadataStoreProvided()
    {
        // Arrange
        var diskQuotaService = CreateDiskQuotaService();
        var provider = new FileSystemRasterTileCacheProvider(
            _tempPath,
            NullLogger<FileSystemRasterTileCacheProvider>.Instance,
            diskQuotaService,
            _metadataStore,
            _quotaMetrics);

        var key = CreateTestKey();
        var entry = CreateTestEntry(1024);

        await provider.StoreAsync(key, entry);

        // Act
        await provider.RemoveAsync(key);

        // Assert
        var path = GetExpectedPath(key);
        File.Exists(path).Should().BeFalse();

        var metadata = await _metadataStore.GetMetadataAsync(key);
        metadata.Should().BeNull();
    }

    [Fact]
    public async Task StoreAsync_ShouldWorkWithoutDiskQuotaService()
    {
        // Arrange - no quota service
        var provider = new FileSystemRasterTileCacheProvider(
            _tempPath,
            NullLogger<FileSystemRasterTileCacheProvider>.Instance);

        var key = CreateTestKey();
        var entry = CreateTestEntry(1024);

        // Act
        await provider.StoreAsync(key, entry);

        // Assert
        var path = GetExpectedPath(key);
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public async Task StoreAsync_ShouldWorkWithoutMetadataStore()
    {
        // Arrange - no metadata store
        var diskQuotaService = CreateDiskQuotaService();
        var provider = new FileSystemRasterTileCacheProvider(
            _tempPath,
            NullLogger<FileSystemRasterTileCacheProvider>.Instance,
            diskQuotaService);

        var key = CreateTestKey();
        var entry = CreateTestEntry(1024);

        // Act
        await provider.StoreAsync(key, entry);

        // Assert
        var path = GetExpectedPath(key);
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public async Task TryGetAsync_ShouldReturnNull_WhenFileDoesNotExist()
    {
        // Arrange
        var provider = new FileSystemRasterTileCacheProvider(
            _tempPath,
            NullLogger<FileSystemRasterTileCacheProvider>.Instance);

        var key = CreateTestKey();

        // Act
        var result = await provider.TryGetAsync(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task StoreAsync_ShouldCreateDirectory_WhenItDoesNotExist()
    {
        // Arrange
        var nestedPath = Path.Combine(_tempPath, "nested", "path");
        var provider = new FileSystemRasterTileCacheProvider(
            nestedPath,
            NullLogger<FileSystemRasterTileCacheProvider>.Instance);

        var key = CreateTestKey();
        var entry = CreateTestEntry(1024);

        // Act
        await provider.StoreAsync(key, entry);

        // Assert
        Directory.Exists(nestedPath).Should().BeTrue();
    }

    [Fact]
    public async Task PurgeDatasetAsync_ShouldRemoveAllDatasetFiles()
    {
        // Arrange
        var provider = new FileSystemRasterTileCacheProvider(
            _tempPath,
            NullLogger<FileSystemRasterTileCacheProvider>.Instance);

        var key1 = CreateTestKey("test-dataset", 0, 0, 0);
        var key2 = CreateTestKey("test-dataset", 0, 0, 1);
        var entry = CreateTestEntry(1024);

        await provider.StoreAsync(key1, entry);
        await provider.StoreAsync(key2, entry);

        // Act
        await provider.PurgeDatasetAsync("test-dataset");

        // Assert
        var result1 = await provider.TryGetAsync(key1);
        var result2 = await provider.TryGetAsync(key2);
        result1.Should().BeNull();
        result2.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldCreateRootDirectory()
    {
        // Arrange
        var newPath = Path.Combine(_tempPath, "new-root");

        // Act
        _ = new FileSystemRasterTileCacheProvider(
            newPath,
            NullLogger<FileSystemRasterTileCacheProvider>.Instance);

        // Assert
        Directory.Exists(newPath).Should().BeTrue();
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_ForNullPath()
    {
        // Act
        var act = () => new FileSystemRasterTileCacheProvider(
            null!,
            NullLogger<FileSystemRasterTileCacheProvider>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_ForNullLogger()
    {
        // Act
        var act = () => new FileSystemRasterTileCacheProvider(
            _tempPath,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private DiskQuotaService CreateDiskQuotaService()
    {
        var options = new DiskQuotaOptions
        {
            MaxDiskUsagePercent = 0.95, // Very high threshold for tests
            MinimumFreeSpaceBytes = 1024, // Very low minimum for tests
            EnableAutomaticCleanup = true
        };

        return new DiskQuotaService(
            NullLogger<DiskQuotaService>.Instance,
            _metadataStore,
            NullRasterTileCacheProvider.Instance,
            options,
            _quotaMetrics);
    }

    private RasterTileCacheKey CreateTestKey(
        string? datasetId = null,
        int zoom = 0,
        int row = 0,
        int col = 0)
    {
        return new RasterTileCacheKey(
            datasetId: datasetId ?? "test-dataset",
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

    private string GetExpectedPath(RasterTileCacheKey key)
    {
        var relative = RasterTileCachePathHelper.GetRelativePath(key, Path.DirectorySeparatorChar);
        return Path.Combine(_tempPath, relative);
    }
}

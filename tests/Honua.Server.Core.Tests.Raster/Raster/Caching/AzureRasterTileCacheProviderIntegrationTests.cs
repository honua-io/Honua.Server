using System;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using FluentAssertions;
using Honua.Server.Core.Raster.Caching;
using Honua.Server.Core.Tests.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Caching;

/// <summary>
/// Integration tests for AzureBlobRasterTileCacheProvider using Azurite Testcontainer.
/// Automatically starts Azurite container for testing.
/// Requires Docker to be running.
/// </summary>
[Collection("StorageContainers")]
[Trait("Category", "Integration")]
public class AzureRasterTileCacheProviderIntegrationTests : IAsyncLifetime
{
    private const string ContainerName = "test-raster-tiles";

    private readonly StorageContainerFixture _fixture;
    private BlobServiceClient? _blobServiceClient;
    private AzureBlobRasterTileCacheProvider? _provider;

    public AzureRasterTileCacheProviderIntegrationTests(StorageContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Skip if Docker or Azurite is not available
        if (!_fixture.IsDockerAvailable || !_fixture.AzuriteAvailable)
        {
            throw new SkipException("Docker or Azurite container is not available. Ensure Docker is running.");
        }

        // Use the shared Azurite client
        _blobServiceClient = _fixture.AzuriteClient;

        // Create test container
        var containerClient = _blobServiceClient!.GetBlobContainerClient(ContainerName);
        await containerClient.CreateIfNotExistsAsync();

        _provider = new AzureBlobRasterTileCacheProvider(
            containerClient,
            ensureContainer: false,
            NullLogger<AzureBlobRasterTileCacheProvider>.Instance);
    }

    public async Task DisposeAsync()
    {
        if (_blobServiceClient != null && _fixture.AzuriteAvailable)
        {
            try
            {
                // Clean up test container
                var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
                await containerClient.DeleteIfExistsAsync();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task StoreAsync_ThenTryGetAsync_ShouldReturnStoredTile()
    {
        var key = new RasterTileCacheKey(
            "dataset1",
            "WorldWebMercatorQuad",
            8, 10, 20,
            "default",
            "image/png",
            true,
            256);
        var content = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG header
        var entry = new RasterTileCacheEntry(content, "image/png", DateTimeOffset.UtcNow);

        await _provider!.StoreAsync(key, entry);

        var result = await _provider.TryGetAsync(key);

        result.Should().NotBeNull();
        result!.Value.Content.ToArray().Should().Equal(content);
        result.Value.ContentType.Should().Be("image/png");
        result.Value.CreatedUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task TryGetAsync_WhenTileDoesNotExist_ShouldReturnNull()
    {
        var key = new RasterTileCacheKey(
            "nonexistent",
            "WorldWebMercatorQuad",
            8, 10, 20,
            "default",
            "image/png",
            true,
            256);

        var result = await _provider!.TryGetAsync(key);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_ShouldDeleteTile()
    {
        var key = new RasterTileCacheKey(
            "dataset2",
            "WorldWebMercatorQuad",
            8, 10, 20,
            "default",
            "image/png",
            true,
            256);
        var entry = new RasterTileCacheEntry(new byte[] { 1, 2, 3 }, "image/png", DateTimeOffset.UtcNow);

        await _provider!.StoreAsync(key, entry);
        (await _provider.TryGetAsync(key)).Should().NotBeNull();

        await _provider.RemoveAsync(key);

        var result = await _provider.TryGetAsync(key);
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_WhenTileDoesNotExist_ShouldNotThrow()
    {
        var key = new RasterTileCacheKey(
            "dataset3",
            "WorldWebMercatorQuad",
            8, 10, 20,
            "default",
            "image/png",
            true,
            256);

        var act = async () => await _provider!.RemoveAsync(key);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PurgeDatasetAsync_ShouldRemoveAllTilesForDataset()
    {
        var key1 = new RasterTileCacheKey("dataset4", "WorldWebMercatorQuad", 8, 10, 20, "default", "image/png", true, 256);
        var key2 = new RasterTileCacheKey("dataset4", "WorldWebMercatorQuad", 8, 11, 21, "default", "image/png", true, 256);
        var key3 = new RasterTileCacheKey("dataset5", "WorldWebMercatorQuad", 8, 10, 20, "default", "image/png", true, 256);

        var entry = new RasterTileCacheEntry(new byte[] { 1, 2, 3 }, "image/png", DateTimeOffset.UtcNow);
        await _provider!.StoreAsync(key1, entry);
        await _provider.StoreAsync(key2, entry);
        await _provider.StoreAsync(key3, entry);

        await _provider.PurgeDatasetAsync("dataset4");

        (await _provider.TryGetAsync(key1)).Should().BeNull();
        (await _provider.TryGetAsync(key2)).Should().BeNull();
        (await _provider.TryGetAsync(key3)).Should().NotBeNull(); // Different dataset
    }

    [Fact]
    public async Task StoreAsync_DifferentFormats_ShouldPreserveContentType()
    {
        var keyPng = new RasterTileCacheKey("dataset6", "WorldWebMercatorQuad", 8, 10, 20, "default", "image/png", true, 256);
        var keyJpeg = new RasterTileCacheKey("dataset6", "WorldWebMercatorQuad", 8, 11, 21, "default", "image/jpeg", true, 256);
        var keyWebp = new RasterTileCacheKey("dataset6", "WorldWebMercatorQuad", 8, 12, 22, "default", "image/webp", true, 256);

        var pngEntry = new RasterTileCacheEntry(new byte[] { 1 }, "image/png", DateTimeOffset.UtcNow);
        var jpegEntry = new RasterTileCacheEntry(new byte[] { 2 }, "image/jpeg", DateTimeOffset.UtcNow);
        var webpEntry = new RasterTileCacheEntry(new byte[] { 3 }, "image/webp", DateTimeOffset.UtcNow);

        await _provider!.StoreAsync(keyPng, pngEntry);
        await _provider.StoreAsync(keyJpeg, jpegEntry);
        await _provider.StoreAsync(keyWebp, webpEntry);

        (await _provider.TryGetAsync(keyPng))!.Value.ContentType.Should().Be("image/png");
        (await _provider.TryGetAsync(keyJpeg))!.Value.ContentType.Should().Be("image/jpeg");
        (await _provider.TryGetAsync(keyWebp))!.Value.ContentType.Should().Be("image/webp");
    }

    [Fact]
    public async Task StoreAsync_LargeTile_ShouldHandleCorrectly()
    {
        var key = new RasterTileCacheKey("dataset7", "WorldWebMercatorQuad", 12, 100, 200, "default", "image/png", true, 256);
        var largeContent = new byte[1024 * 1024]; // 1 MB PNG
        new Random().NextBytes(largeContent);
        var entry = new RasterTileCacheEntry(largeContent, "image/png", DateTimeOffset.UtcNow);

        await _provider!.StoreAsync(key, entry);

        var result = await _provider.TryGetAsync(key);

        result.Should().NotBeNull();
        result!.Value.Content.Length.Should().Be(largeContent.Length);
    }

    [Fact]
    public async Task StoreAsync_MultipleZoomLevels_ShouldIsolateTiles()
    {
        var key1 = new RasterTileCacheKey("dataset8", "WorldWebMercatorQuad", 8, 10, 20, "default", "image/png", true, 256);
        var key2 = new RasterTileCacheKey("dataset8", "WorldWebMercatorQuad", 9, 20, 40, "default", "image/png", true, 256);
        var key3 = new RasterTileCacheKey("dataset8", "WorldWebMercatorQuad", 10, 40, 80, "default", "image/png", true, 256);

        var entry1 = new RasterTileCacheEntry(new byte[] { 1 }, "image/png", DateTimeOffset.UtcNow);
        var entry2 = new RasterTileCacheEntry(new byte[] { 2 }, "image/png", DateTimeOffset.UtcNow);
        var entry3 = new RasterTileCacheEntry(new byte[] { 3 }, "image/png", DateTimeOffset.UtcNow);

        await _provider!.StoreAsync(key1, entry1);
        await _provider.StoreAsync(key2, entry2);
        await _provider.StoreAsync(key3, entry3);

        (await _provider.TryGetAsync(key1))!.Value.Content.ToArray().Should().Equal(new byte[] { 1 });
        (await _provider.TryGetAsync(key2))!.Value.Content.ToArray().Should().Equal(new byte[] { 2 });
        (await _provider.TryGetAsync(key3))!.Value.Content.ToArray().Should().Equal(new byte[] { 3 });
    }

    [Fact]
    public async Task StoreAsync_DifferentTileMatrixSets_ShouldIsolateTiles()
    {
        var keyWebMercator = new RasterTileCacheKey("dataset9", "WorldWebMercatorQuad", 8, 10, 20, "default", "image/png", true, 256);
        var keyWgs84 = new RasterTileCacheKey("dataset9", "WorldCRS84Quad", 8, 10, 20, "default", "image/png", true, 256);

        var entry1 = new RasterTileCacheEntry(new byte[] { 1 }, "image/png", DateTimeOffset.UtcNow);
        var entry2 = new RasterTileCacheEntry(new byte[] { 2 }, "image/png", DateTimeOffset.UtcNow);

        await _provider!.StoreAsync(keyWebMercator, entry1);
        await _provider.StoreAsync(keyWgs84, entry2);

        (await _provider.TryGetAsync(keyWebMercator))!.Value.Content.ToArray().Should().Equal(new byte[] { 1 });
        (await _provider.TryGetAsync(keyWgs84))!.Value.Content.ToArray().Should().Equal(new byte[] { 2 });
    }

    [Fact]
    public async Task StoreAsync_ShouldOrganizeByDataset()
    {
        var key = new RasterTileCacheKey("dataset10", "WorldWebMercatorQuad", 8, 10, 20, "default", "image/png", true, 256);
        var entry = new RasterTileCacheEntry(new byte[] { 1, 2, 3 }, "image/png", DateTimeOffset.UtcNow);

        await _provider!.StoreAsync(key, entry);

        // Verify blob exists with correct path structure
        var containerClient = _blobServiceClient!.GetBlobContainerClient(ContainerName);
        var blobs = containerClient.GetBlobsAsync(prefix: "dataset10/");
        var foundBlob = false;
        await foreach (var blob in blobs)
        {
            if (blob.Name.StartsWith("dataset10/"))
            {
                foundBlob = true;
                break;
            }
        }

        foundBlob.Should().BeTrue("Blob should be stored under dataset10/ prefix");
    }
}

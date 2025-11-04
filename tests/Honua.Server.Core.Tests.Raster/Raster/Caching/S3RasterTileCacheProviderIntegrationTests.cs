using System;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Honua.Server.Core.Raster.Caching;
using Honua.Server.Core.Tests.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Caching;

/// <summary>
/// Integration tests for S3RasterTileCacheProvider using MinIO Testcontainer.
/// Automatically starts MinIO container for testing.
/// Requires Docker to be running.
/// </summary>
[Collection("StorageContainers")]
[Trait("Category", "Integration")]
public class S3RasterTileCacheProviderIntegrationTests : IAsyncLifetime
{
    private const string BucketName = "test-raster-tiles";

    private readonly StorageContainerFixture _fixture;
    private IAmazonS3? _s3Client;
    private S3RasterTileCacheProvider? _provider;

    public S3RasterTileCacheProviderIntegrationTests(StorageContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Skip if Docker or MinIO is not available
        if (!_fixture.IsDockerAvailable || !_fixture.MinioAvailable)
        {
            throw new SkipException("Docker or MinIO container is not available. Ensure Docker is running.");
        }

        // Use the shared MinIO client
        _s3Client = _fixture.MinioClient;

        // Create test bucket
        try
        {
            await _s3Client!.GetBucketLocationAsync(BucketName);
        }
        catch (AmazonS3Exception)
        {
            await _s3Client!.PutBucketAsync(BucketName);
        }

        _provider = new S3RasterTileCacheProvider(
            _s3Client!,
            BucketName,
            "raster/",
            ensureBucket: false,
            NullLogger<S3RasterTileCacheProvider>.Instance);
    }

    public async Task DisposeAsync()
    {
        if (_s3Client != null && _fixture.MinioAvailable)
        {
            try
            {
                // Clean up test bucket
                var listResponse = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = BucketName
                });

                foreach (var obj in listResponse.S3Objects)
                {
                    await _s3Client.DeleteObjectAsync(BucketName, obj.Key);
                }

                await _s3Client.DeleteBucketAsync(BucketName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        await (_provider?.DisposeAsync() ?? ValueTask.CompletedTask);
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
    public async Task StoreAsync_WithPrefix_ShouldOrganizeObjectsCorrectly()
    {
        var key = new RasterTileCacheKey("dataset6", "WorldWebMercatorQuad", 8, 10, 20, "default", "image/png", true, 256);
        var entry = new RasterTileCacheEntry(new byte[] { 1, 2, 3 }, "image/png", DateTimeOffset.UtcNow);

        await _provider!.StoreAsync(key, entry);

        // Verify object exists with correct prefix
        var listResponse = await _s3Client!.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = BucketName,
            Prefix = "raster/"
        });

        var foundObject = false;
        foreach (var obj in listResponse.S3Objects)
        {
            if (obj.Key.StartsWith("raster/dataset6/"))
            {
                foundObject = true;
                break;
            }
        }

        foundObject.Should().BeTrue("Object should be stored under raster/ prefix");
    }

    [Fact]
    public async Task StoreAsync_DifferentFormats_ShouldPreserveContentType()
    {
        var keyPng = new RasterTileCacheKey("dataset7", "WorldWebMercatorQuad", 8, 10, 20, "default", "image/png", true, 256);
        var keyJpeg = new RasterTileCacheKey("dataset7", "WorldWebMercatorQuad", 8, 11, 21, "default", "image/jpeg", true, 256);
        var keyWebp = new RasterTileCacheKey("dataset7", "WorldWebMercatorQuad", 8, 12, 22, "default", "image/webp", true, 256);

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
        var key = new RasterTileCacheKey("dataset8", "WorldWebMercatorQuad", 12, 100, 200, "default", "image/png", true, 256);
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
        var key1 = new RasterTileCacheKey("dataset9", "WorldWebMercatorQuad", 8, 10, 20, "default", "image/png", true, 256);
        var key2 = new RasterTileCacheKey("dataset9", "WorldWebMercatorQuad", 9, 20, 40, "default", "image/png", true, 256);
        var key3 = new RasterTileCacheKey("dataset9", "WorldWebMercatorQuad", 10, 40, 80, "default", "image/png", true, 256);

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
        var keyWebMercator = new RasterTileCacheKey("dataset10", "WorldWebMercatorQuad", 8, 10, 20, "default", "image/png", true, 256);
        var keyWgs84 = new RasterTileCacheKey("dataset10", "WorldCRS84Quad", 8, 10, 20, "default", "image/png", true, 256);

        var entry1 = new RasterTileCacheEntry(new byte[] { 1 }, "image/png", DateTimeOffset.UtcNow);
        var entry2 = new RasterTileCacheEntry(new byte[] { 2 }, "image/png", DateTimeOffset.UtcNow);

        await _provider!.StoreAsync(keyWebMercator, entry1);
        await _provider.StoreAsync(keyWgs84, entry2);

        (await _provider.TryGetAsync(keyWebMercator))!.Value.Content.ToArray().Should().Equal(new byte[] { 1 });
        (await _provider.TryGetAsync(keyWgs84))!.Value.Content.ToArray().Should().Equal(new byte[] { 2 });
    }
}

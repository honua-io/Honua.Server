using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Caching;
using Honua.Server.Core.Raster.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster;

[Trait("Category", "Unit")]
public class RasterTileCacheProviderTests
{
    [Fact]
    public async Task FileSystemProvider_ShouldRoundTripTile()
    {
        var root = Path.Combine(Path.GetTempPath(), "honua-cache-tests", Guid.NewGuid().ToString("N"));
        var provider = new FileSystemRasterTileCacheProvider(root, NullLogger<FileSystemRasterTileCacheProvider>.Instance);

        var key = new RasterTileCacheKey("dataset", "WorldCRS84Quad", 1, 0, 0, "default", "image/png", true, 256);
        var entry = new RasterTileCacheEntry(new byte[] { 1, 2, 3 }, "image/png", DateTimeOffset.UtcNow);

        await provider.StoreAsync(key, entry);

        var hit = await provider.TryGetAsync(key);
        hit.Should().NotBeNull();
        hit!.Value.Content.ToArray().Should().Equal(entry.Content.ToArray());
        hit.Value.ContentType.Should().Be("image/png");

        await provider.PurgeDatasetAsync("dataset");

        var after = await provider.TryGetAsync(key);
        after.Should().BeNull();

        var datasetPath = Path.Combine(root, CacheKeyNormalizer.SanitizeForFilesystem("dataset"));
        Directory.Exists(datasetPath).Should().BeFalse();
    }

    [Theory]
    [InlineData("dataset", "WorldCRS84Quad", 2, 5, 10, "style", "image/png", true, 256)]
    [InlineData("Data Set", "WorldWebMercatorQuad", 3, 7, 1, "Infrared", "image/jpeg", false, 512)]
    public void PathHelper_ShouldProduceStableKeys(string datasetId, string matrixId, int zoom, int row, int column, string styleId, string format, bool transparent, int tileSize)
    {
        var key = new RasterTileCacheKey(datasetId, matrixId, zoom, row, column, styleId, format, transparent, tileSize);
        var relative = RasterTileCachePathHelper.GetRelativePath(key, '/');

        relative.Should().NotBeNullOrWhiteSpace();
        relative.Should().Contain(matrixId.ToLowerInvariant());
        relative.Should().EndWith($"/{column}/{row}.{RasterTileCachePathHelper.ResolveExtension(format)}");
    }
}

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Raster.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster;

[Trait("Category", "Unit")]
public sealed class RasterTileCacheProviderFactoryTests
{
    [Fact]
    public void Create_ShouldReturnNullProvider_WhenConfigurationDisabled()
    {
        var provider = InvokeFactory(new RasterTileCacheConfiguration { Enabled = false });
        provider.Should().BeSameAs(NullRasterTileCacheProvider.Instance);
    }

    [Fact]
    public void Create_ShouldReturnFileSystemProvider_WhenConfigurationMissing()
    {
        var provider = InvokeFactory(null);
        provider.Should().BeOfType<FileSystemRasterTileCacheProvider>();
    }

    [Fact]
    public void Create_ShouldThrow_ForUnknownProvider()
    {
        var config = new RasterTileCacheConfiguration
        {
            Enabled = true,
            Provider = "unsupported"
        };

        Action act = () => InvokeFactory(config);
        act.Should().Throw<NotSupportedException>().WithMessage("*unsupported*");
    }

    [Fact]
    public void Create_ShouldThrow_WhenS3BucketMissing()
    {
        var config = new RasterTileCacheConfiguration
        {
            Enabled = true,
            Provider = "s3",
            S3 = new RasterTileS3Configuration
            {
                AccessKeyId = "access",
                SecretAccessKey = "secret"
            }
        };

        Action act = () => InvokeFactory(config);
        act.Should().Throw<InvalidDataException>().WithMessage("*bucketName*");
    }

    [Fact]
    public void Create_ShouldThrow_WhenAzureConnectionStringMissing()
    {
        var config = new RasterTileCacheConfiguration
        {
            Enabled = true,
            Provider = "azure",
            Azure = new RasterTileAzureConfiguration
            {
                ContainerName = "tiles"
            }
        };

        Action act = () => InvokeFactory(config);
        act.Should().Throw<InvalidDataException>().WithMessage("*connectionString*");
    }

    [Fact]
    public async Task FileSystemProvider_ShouldPersistTileUnderRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "honua-raster-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new RasterTileCacheConfiguration
            {
                Enabled = true,
                Provider = "filesystem",
                FileSystem = new RasterTileFileSystemConfiguration { RootPath = root }
            };

            var provider = InvokeFactory(config).Should().BeOfType<FileSystemRasterTileCacheProvider>().Subject;

            var key = new RasterTileCacheKey("dataset", "matrix", 2, 1, 3, "style", "image/png", transparent: true, tileSize: 256);
            var entry = new RasterTileCacheEntry(new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 }), "image/png", DateTimeOffset.UtcNow);
            await provider.StoreAsync(key, entry);

            var expectedPath = Path.Combine(
                Path.GetFullPath(root),
                RasterTileCachePathHelper.GetRelativePath(key, Path.DirectorySeparatorChar));

            File.Exists(expectedPath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static IRasterTileCacheProvider InvokeFactory(RasterTileCacheConfiguration? configuration)
    {
        var factory = new RasterTileCacheProviderFactory(NullLoggerFactory.Instance);
        return factory.Create(configuration ?? RasterTileCacheConfiguration.Default);
    }
}

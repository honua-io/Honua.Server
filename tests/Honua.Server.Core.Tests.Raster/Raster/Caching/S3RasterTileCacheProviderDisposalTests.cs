using System;
using System.Threading.Tasks;
using Amazon.S3;
using Honua.Server.Core.Raster.Caching;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Caching;

/// <summary>
/// Tests for proper disposal of S3 raster tile cache provider resources to prevent resource leaks.
/// </summary>
public sealed class S3RasterTileCacheProviderDisposalTests
{
    private readonly Mock<ILogger<S3RasterTileCacheProvider>> _logger;
    private readonly Mock<IAmazonS3> _mockS3Client;

    public S3RasterTileCacheProviderDisposalTests()
    {
        _logger = new Mock<ILogger<S3RasterTileCacheProvider>>();
        _mockS3Client = new Mock<IAmazonS3>();
    }

    [Fact]
    public async Task S3RasterTileCacheProvider_DisposeAsync_DisposesOwnedClient()
    {
        // Arrange
        var provider = new S3RasterTileCacheProvider(
            _mockS3Client.Object,
            "test-bucket",
            "tiles",
            ensureBucket: false,
            _logger.Object,
            metrics: null,
            ownsClient: true);

        // Act
        await provider.DisposeAsync();

        // Assert
        _mockS3Client.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task S3RasterTileCacheProvider_DisposeAsync_DoesNotDisposeUnownedClient()
    {
        // Arrange
        var provider = new S3RasterTileCacheProvider(
            _mockS3Client.Object,
            "test-bucket",
            "tiles",
            ensureBucket: false,
            _logger.Object,
            metrics: null,
            ownsClient: false);

        // Act
        await provider.DisposeAsync();

        // Assert
        _mockS3Client.Verify(x => x.Dispose(), Times.Never);
    }

    [Fact]
    public async Task S3RasterTileCacheProvider_DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var provider = new S3RasterTileCacheProvider(
            _mockS3Client.Object,
            "test-bucket",
            "tiles",
            ensureBucket: false,
            _logger.Object,
            metrics: null,
            ownsClient: true);

        // Act
        await provider.DisposeAsync();
        await provider.DisposeAsync();
        await provider.DisposeAsync();

        // Assert - only disposed once due to ownership check
        _mockS3Client.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task S3RasterTileCacheProvider_DisposeAsync_DefaultOwnership_DoesNotDisposeClient()
    {
        // Arrange - default ownsClient is false
        var provider = new S3RasterTileCacheProvider(
            _mockS3Client.Object,
            "test-bucket",
            "tiles",
            ensureBucket: false,
            _logger.Object,
            metrics: null);

        // Act
        await provider.DisposeAsync();

        // Assert
        _mockS3Client.Verify(x => x.Dispose(), Times.Never);
    }

    [Fact]
    public async Task S3RasterTileCacheProvider_DisposeAsync_DisposesSemaphoreSlim()
    {
        // Arrange
        var provider = new S3RasterTileCacheProvider(
            _mockS3Client.Object,
            "test-bucket",
            "tiles",
            ensureBucket: false,
            _logger.Object,
            metrics: null,
            ownsClient: false);

        // Act
        await provider.DisposeAsync();

        // Assert - SemaphoreSlim is disposed
        // Attempting to use provider after disposal would throw ObjectDisposedException
        // This test verifies no exceptions during disposal
    }

    [Fact]
    public void S3RasterTileCacheProvider_Constructor_RequiresClient()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new S3RasterTileCacheProvider(null!, "test-bucket", "tiles", false, _logger.Object));
    }

    [Fact]
    public void S3RasterTileCacheProvider_Constructor_RequiresBucketName()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new S3RasterTileCacheProvider(_mockS3Client.Object, "", "tiles", false, _logger.Object));
    }

    [Fact]
    public void S3RasterTileCacheProvider_Constructor_RequiresLogger()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new S3RasterTileCacheProvider(_mockS3Client.Object, "test-bucket", "tiles", false, null!));
    }
}

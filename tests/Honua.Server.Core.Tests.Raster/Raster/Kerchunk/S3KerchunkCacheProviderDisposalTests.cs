using System;
using System.Threading.Tasks;
using Amazon.S3;
using Honua.Server.Core.Raster.Kerchunk;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Kerchunk;

/// <summary>
/// Tests for proper disposal of S3 kerchunk cache provider resources to prevent resource leaks.
/// </summary>
public sealed class S3KerchunkCacheProviderDisposalTests
{
    private readonly Mock<ILogger<S3KerchunkCacheProvider>> _logger;
    private readonly Mock<IAmazonS3> _mockS3Client;

    public S3KerchunkCacheProviderDisposalTests()
    {
        _logger = new Mock<ILogger<S3KerchunkCacheProvider>>();
        _mockS3Client = new Mock<IAmazonS3>();
    }

    [Fact]
    public async Task S3KerchunkCacheProvider_DisposeAsync_DisposesOwnedClient()
    {
        // Arrange
        var provider = new S3KerchunkCacheProvider(
            _mockS3Client.Object,
            "test-bucket",
            "kerchunk-refs",
            _logger.Object,
            ownsClient: true);

        // Act
        await provider.DisposeAsync();

        // Assert
        _mockS3Client.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task S3KerchunkCacheProvider_DisposeAsync_DoesNotDisposeUnownedClient()
    {
        // Arrange
        var provider = new S3KerchunkCacheProvider(
            _mockS3Client.Object,
            "test-bucket",
            "kerchunk-refs",
            _logger.Object,
            ownsClient: false);

        // Act
        await provider.DisposeAsync();

        // Assert
        _mockS3Client.Verify(x => x.Dispose(), Times.Never);
    }

    [Fact]
    public async Task S3KerchunkCacheProvider_DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var provider = new S3KerchunkCacheProvider(
            _mockS3Client.Object,
            "test-bucket",
            "kerchunk-refs",
            _logger.Object,
            ownsClient: true);

        // Act
        await provider.DisposeAsync();
        await provider.DisposeAsync();
        await provider.DisposeAsync();

        // Assert - only disposed once due to ownership check
        _mockS3Client.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task S3KerchunkCacheProvider_DisposeAsync_DefaultOwnership_DoesNotDisposeClient()
    {
        // Arrange - default ownsClient is false
        var provider = new S3KerchunkCacheProvider(
            _mockS3Client.Object,
            "test-bucket",
            "kerchunk-refs",
            _logger.Object);

        // Act
        await provider.DisposeAsync();

        // Assert
        _mockS3Client.Verify(x => x.Dispose(), Times.Never);
    }

    [Fact]
    public void S3KerchunkCacheProvider_Constructor_RequiresClient()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new S3KerchunkCacheProvider(null!, "test-bucket", "prefix", _logger.Object));
    }

    [Fact]
    public void S3KerchunkCacheProvider_Constructor_RequiresBucketName()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new S3KerchunkCacheProvider(_mockS3Client.Object, null!, "prefix", _logger.Object));
    }

    [Fact]
    public void S3KerchunkCacheProvider_Constructor_RequiresLogger()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new S3KerchunkCacheProvider(_mockS3Client.Object, "test-bucket", "prefix", null!));
    }
}

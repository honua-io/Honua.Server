using System;
using System.Threading.Tasks;
using Amazon.S3;
using Honua.Server.Core.Raster.Sources;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Sources;

/// <summary>
/// Tests for proper disposal of S3 raster source provider resources to prevent resource leaks.
/// </summary>
public sealed class S3RasterSourceProviderDisposalTests
{
    private readonly Mock<ILogger<S3RasterSourceProvider>> _logger;
    private readonly Mock<IAmazonS3> _mockS3Client;

    public S3RasterSourceProviderDisposalTests()
    {
        _logger = new Mock<ILogger<S3RasterSourceProvider>>();
        _mockS3Client = new Mock<IAmazonS3>();
    }

    [Fact]
    public async Task S3RasterSourceProvider_DisposeAsync_DisposesOwnedClient()
    {
        // Arrange
        var provider = new S3RasterSourceProvider(_mockS3Client.Object, _logger.Object, ownsClient: true);

        // Act
        await provider.DisposeAsync();

        // Assert
        _mockS3Client.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task S3RasterSourceProvider_DisposeAsync_DoesNotDisposeUnownedClient()
    {
        // Arrange
        var provider = new S3RasterSourceProvider(_mockS3Client.Object, _logger.Object, ownsClient: false);

        // Act
        await provider.DisposeAsync();

        // Assert
        _mockS3Client.Verify(x => x.Dispose(), Times.Never);
    }

    [Fact]
    public async Task S3RasterSourceProvider_DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var provider = new S3RasterSourceProvider(_mockS3Client.Object, _logger.Object, ownsClient: true);

        // Act
        await provider.DisposeAsync();
        await provider.DisposeAsync();
        await provider.DisposeAsync();

        // Assert - only disposed once due to ownership check
        _mockS3Client.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task S3RasterSourceProvider_DisposeAsync_DefaultOwnership_DoesNotDisposeClient()
    {
        // Arrange - default ownsClient is false
        var provider = new S3RasterSourceProvider(_mockS3Client.Object, _logger.Object);

        // Act
        await provider.DisposeAsync();

        // Assert
        _mockS3Client.Verify(x => x.Dispose(), Times.Never);
    }

    [Fact]
    public void S3RasterSourceProvider_Constructor_RequiresClient()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new S3RasterSourceProvider(null!, _logger.Object));
    }

    [Fact]
    public void S3RasterSourceProvider_Constructor_RequiresLogger()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new S3RasterSourceProvider(_mockS3Client.Object, null!));
    }
}

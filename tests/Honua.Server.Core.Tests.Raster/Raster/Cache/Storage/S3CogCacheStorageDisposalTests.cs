using System;
using System.Threading.Tasks;
using Amazon.S3;
using Honua.Server.Core.Raster.Cache.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Cache.Storage;

/// <summary>
/// Tests for proper disposal of S3 COG cache storage resources to prevent resource leaks.
/// </summary>
public sealed class S3CogCacheStorageDisposalTests
{
    private readonly Mock<ILogger<S3CogCacheStorage>> _logger;
    private readonly Mock<IAmazonS3> _mockS3Client;

    public S3CogCacheStorageDisposalTests()
    {
        _logger = new Mock<ILogger<S3CogCacheStorage>>();
        _mockS3Client = new Mock<IAmazonS3>();
    }

    [Fact]
    public async Task S3CogCacheStorage_DisposeAsync_DisposesOwnedClient()
    {
        // Arrange
        var storage = new S3CogCacheStorage(
            _mockS3Client.Object,
            "test-bucket",
            "cog-cache",
            _logger.Object,
            ownsClient: true);

        // Act
        await storage.DisposeAsync();

        // Assert
        _mockS3Client.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task S3CogCacheStorage_DisposeAsync_DoesNotDisposeUnownedClient()
    {
        // Arrange
        var storage = new S3CogCacheStorage(
            _mockS3Client.Object,
            "test-bucket",
            "cog-cache",
            _logger.Object,
            ownsClient: false);

        // Act
        await storage.DisposeAsync();

        // Assert
        _mockS3Client.Verify(x => x.Dispose(), Times.Never);
    }

    [Fact]
    public async Task S3CogCacheStorage_DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var storage = new S3CogCacheStorage(
            _mockS3Client.Object,
            "test-bucket",
            "cog-cache",
            _logger.Object,
            ownsClient: true);

        // Act
        await storage.DisposeAsync();
        await storage.DisposeAsync();
        await storage.DisposeAsync();

        // Assert - only disposed once due to ownership check
        _mockS3Client.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task S3CogCacheStorage_DisposeAsync_DefaultOwnership_DoesNotDisposeClient()
    {
        // Arrange - default ownsClient is false
        var storage = new S3CogCacheStorage(
            _mockS3Client.Object,
            "test-bucket",
            "cog-cache",
            _logger.Object);

        // Act
        await storage.DisposeAsync();

        // Assert
        _mockS3Client.Verify(x => x.Dispose(), Times.Never);
    }

    [Fact]
    public void S3CogCacheStorage_Constructor_RequiresClient()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new S3CogCacheStorage(null!, "test-bucket", "prefix", _logger.Object));
    }

    [Fact]
    public void S3CogCacheStorage_Constructor_RequiresBucket()
    {
        // Act & Assert - base class throws ArgumentException for empty bucket
        Assert.Throws<ArgumentException>(() =>
            new S3CogCacheStorage(_mockS3Client.Object, null!, "prefix", _logger.Object));
    }

    [Fact]
    public void S3CogCacheStorage_Constructor_RequiresLogger()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new S3CogCacheStorage(_mockS3Client.Object, "test-bucket", "prefix", null!));
    }
}

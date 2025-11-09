using Honua.MapSDK.Models.Drone;
using Honua.MapSDK.Utilities.Drone;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.MapSDK.Tests.Utilities.Drone;

public class LazReaderTests
{
    private readonly Mock<ILogger<LazReader>> _mockLogger;
    private readonly LazReader _reader;

    public LazReaderTests()
    {
        _mockLogger = new Mock<ILogger<LazReader>>();
        _reader = new LazReader(_mockLogger.Object);
    }

    [Fact]
    public async Task ReadMetadataAsync_ValidFile_ReturnsMetadata()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".laz";
        await File.WriteAllTextAsync(tempFile, "test content");

        try
        {
            // Act
            var metadata = await _reader.ReadMetadataAsync(tempFile);

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal(tempFile, metadata.FilePath);
            Assert.True(metadata.FileSize > 0);
            Assert.True(metadata.PointCount > 0);
            Assert.NotNull(metadata.BoundingBox);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ReadMetadataAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = "/nonexistent/file.laz";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _reader.ReadMetadataAsync(nonExistentFile));
    }

    [Fact]
    public async Task ReadPointsAsync_ValidFile_YieldsPoints()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".laz";
        await File.WriteAllTextAsync(tempFile, "test content");

        try
        {
            // Act
            var points = new List<PointCloudPoint>();
            await foreach (var point in _reader.ReadPointsAsync(tempFile, limit: 10))
            {
                points.Add(point);
            }

            // Assert
            Assert.Equal(10, points.Count);
            Assert.All(points, p =>
            {
                Assert.InRange(p.X, -180, 180);
                Assert.InRange(p.Y, -90, 90);
                Assert.InRange(p.Z, 0, 10000);
            });
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ReadPointsAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = "/nonexistent/file.laz";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await foreach (var point in _reader.ReadPointsAsync(nonExistentFile))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task ValidateLazFileAsync_ValidLazFile_ReturnsTrue()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".laz";
        await File.WriteAllTextAsync(tempFile, "test content");

        try
        {
            // Act
            var isValid = await _reader.ValidateLazFileAsync(tempFile);

            // Assert
            Assert.True(isValid);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ValidateLazFileAsync_InvalidExtension_ReturnsFalse()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".txt";
        await File.WriteAllTextAsync(tempFile, "test content");

        try
        {
            // Act
            var isValid = await _reader.ValidateLazFileAsync(tempFile);

            // Assert
            Assert.False(isValid);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ValidateLazFileAsync_NonExistentFile_ReturnsFalse()
    {
        // Arrange
        var nonExistentFile = "/nonexistent/file.laz";

        // Act
        var isValid = await _reader.ValidateLazFileAsync(nonExistentFile);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task ExportToLazAsync_ValidPoints_CompletesSuccessfully()
    {
        // Arrange
        var points = new List<PointCloudPoint>
        {
            new(0, 0, 0, 255, 255, 255, 1),
            new(1, 1, 1, 255, 0, 0, 2)
        };

        var outputPath = Path.GetTempFileName() + ".laz";
        var metadata = new LazMetadata
        {
            FilePath = outputPath,
            PointCount = points.Count,
            BoundingBox = new BoundingBox3D(0, 0, 0, 1, 1, 1)
        };

        try
        {
            // Act
            await _reader.ExportToLazAsync(points, outputPath, metadata);

            // Assert - should complete without exceptions
            Assert.True(true);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }
}

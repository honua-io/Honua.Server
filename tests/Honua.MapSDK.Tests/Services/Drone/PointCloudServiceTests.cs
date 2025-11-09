using Honua.MapSDK.Models.Drone;
using Honua.MapSDK.Services.Drone;
using Honua.Server.Core.DataOperations.Drone;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.MapSDK.Tests.Services.Drone;

public class PointCloudServiceTests
{
    private readonly Mock<IDroneDataRepository> _mockRepository;
    private readonly Mock<ILogger<PointCloudService>> _mockLogger;
    private readonly PointCloudService _service;

    public PointCloudServiceTests()
    {
        _mockRepository = new Mock<IDroneDataRepository>();
        _mockLogger = new Mock<ILogger<PointCloudService>>();
        _service = new PointCloudService(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetStatisticsAsync_ValidSurvey_ReturnsStatistics()
    {
        // Arrange
        var surveyId = Guid.NewGuid();
        var expectedStats = new PointCloudStatistics
        {
            TotalPoints = 5_000_000,
            BoundingBox = new BoundingBox3D(-122.5, 37.7, 0, -122.4, 37.8, 100)
        };

        _mockRepository
            .Setup(r => r.GetPointCloudStatisticsAsync(surveyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _service.GetStatisticsAsync(surveyId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5_000_000, result.TotalPoints);
        Assert.NotNull(result.BoundingBox);
    }

    [Fact]
    public async Task ImportLazFileAsync_ValidFile_ReturnsSuccessResult()
    {
        // Arrange
        var surveyId = Guid.NewGuid();
        var lazFilePath = "/tmp/test.laz";

        // Create a temporary test file
        await File.WriteAllTextAsync(lazFilePath, "test");

        try
        {
            // Act
            var result = await _service.ImportLazFileAsync(surveyId, lazFilePath);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.PointsImported > 0);
            Assert.True(result.DurationSeconds >= 0);
        }
        finally
        {
            // Cleanup
            if (File.Exists(lazFilePath))
            {
                File.Delete(lazFilePath);
            }
        }
    }

    [Fact]
    public async Task ImportLazFileAsync_InvalidFile_ReturnsFailureResult()
    {
        // Arrange
        var surveyId = Guid.NewGuid();
        var lazFilePath = "/nonexistent/test.laz";

        // Act
        var result = await _service.ImportLazFileAsync(surveyId, lazFilePath);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(0, result.PointsImported);
        Assert.Contains("failed", result.Message.ToLower());
    }

    [Fact]
    public async Task GenerateLodLevelsAsync_ValidSurvey_ReturnsSuccess()
    {
        // Arrange
        var surveyId = Guid.NewGuid();

        // Act
        var result = await _service.GenerateLodLevelsAsync(surveyId);

        // Assert
        Assert.True(result.Success);
        Assert.Contains(1, result.LevelsGenerated);
        Assert.Contains(2, result.LevelsGenerated);
    }

    [Fact]
    public async Task GetPointsByClassificationAsync_ValidClassifications_ReturnsFilteredPoints()
    {
        // Arrange
        var surveyId = Guid.NewGuid();
        var classifications = new byte[] { 2, 6 }; // Ground and Buildings

        var testPoints = new List<PointCloudPoint>
        {
            new(0, 0, 0, 255, 255, 255, 2),
            new(1, 1, 1, 255, 0, 0, 6)
        };

        _mockRepository
            .Setup(r => r.QueryPointCloudAsync(
                surveyId,
                It.IsAny<PointCloudQueryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(testPoints.ToAsyncEnumerable());

        // Act
        var result = await _service.GetPointsByClassificationAsync(surveyId, classifications);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
    }

    [Theory]
    [InlineData(18.0, "Full")]
    [InlineData(15.0, "Coarse")]
    [InlineData(10.0, "Sparse")]
    public async Task QueryAsync_DifferentZoomLevels_SelectsAppropriate Lod(double zoomLevel, string expectedLod)
    {
        // Arrange
        var surveyId = Guid.NewGuid();
        var bbox = new BoundingBox3D(-122.5, 37.7, 0, -122.4, 37.8, 100);

        var testPoints = new List<PointCloudPoint>
        {
            new(0, 0, 0, 255, 255, 255, 1)
        };

        _mockRepository
            .Setup(r => r.QueryPointCloudAsync(
                surveyId,
                It.IsAny<PointCloudQueryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(testPoints.ToAsyncEnumerable());

        // Act
        var points = new List<PointCloudPoint>();
        await foreach (var point in _service.QueryAsync(surveyId, bbox, zoomLevel))
        {
            points.Add(point);
        }

        // Assert
        Assert.NotEmpty(points);
        _mockRepository.Verify(
            r => r.QueryPointCloudAsync(
                surveyId,
                It.Is<PointCloudQueryOptions>(o =>
                    (expectedLod == "Full" && o.LodLevel == PointCloudLodLevel.Full) ||
                    (expectedLod == "Coarse" && o.LodLevel == PointCloudLodLevel.Coarse) ||
                    (expectedLod == "Sparse" && o.LodLevel == PointCloudLodLevel.Sparse)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

// Helper extension for creating async enumerables from lists
public static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            await Task.Yield();
            yield return item;
        }
    }
}

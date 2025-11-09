using Honua.MapSDK.Models.Drone;
using Honua.MapSDK.Utilities.Drone;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.MapSDK.Tests.Utilities.Drone;

public class PointCloudLodGeneratorTests
{
    private readonly Mock<ILogger<PointCloudLodGenerator>> _mockLogger;
    private readonly PointCloudLodGenerator _generator;

    public PointCloudLodGeneratorTests()
    {
        _mockLogger = new Mock<ILogger<PointCloudLodGenerator>>();
        _generator = new PointCloudLodGenerator(_mockLogger.Object);
    }

    [Fact]
    public void DecimatePoints_ValidInput_ReturnsDecimatedPoints()
    {
        // Arrange
        var points = Enumerable.Range(0, 100)
            .Select(i => new PointCloudPoint(i, i, i, 255, 255, 255, 1))
            .ToList();

        // Act
        var decimated = _generator.DecimatePoints(points, decimationRatio: 0.1).ToList();

        // Assert
        Assert.True(decimated.Count <= 10);
        Assert.True(decimated.Count >= 9); // Allow for rounding
    }

    [Fact]
    public void DecimatePoints_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        var points = new List<PointCloudPoint>();

        // Act
        var decimated = _generator.DecimatePoints(points).ToList();

        // Assert
        Assert.Empty(decimated);
    }

    [Fact]
    public void VoxelGridFilter_ValidInput_ReturnsFilteredPoints()
    {
        // Arrange
        var points = new List<PointCloudPoint>
        {
            new(0.1, 0.1, 0.1, 255, 255, 255, 1),
            new(0.2, 0.2, 0.2, 255, 255, 255, 1), // Same voxel
            new(1.0, 1.0, 1.0, 255, 0, 0, 2),     // Different voxel
            new(1.1, 1.1, 1.1, 255, 0, 0, 2)      // Same voxel as previous
        };

        // Act
        var filtered = _generator.VoxelGridFilter(points, voxelSize: 0.5).ToList();

        // Assert
        Assert.Equal(2, filtered.Count);
    }

    [Fact]
    public void VoxelGridFilter_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        var points = new List<PointCloudPoint>();

        // Act
        var filtered = _generator.VoxelGridFilter(points).ToList();

        // Assert
        Assert.Empty(filtered);
    }
}

public class PointCloudLodSelectorTests
{
    private readonly PointCloudLodSelector _selector;

    public PointCloudLodSelectorTests()
    {
        _selector = new PointCloudLodSelector();
    }

    [Theory]
    [InlineData(20, 0.0001, PointCloudLodLevel.Full)]
    [InlineData(18, 0.0001, PointCloudLodLevel.Full)]
    [InlineData(15, 0.001, PointCloudLodLevel.Coarse)]
    [InlineData(12, 0.01, PointCloudLodLevel.Coarse)]
    [InlineData(10, 0.1, PointCloudLodLevel.Sparse)]
    [InlineData(8, 1.0, PointCloudLodLevel.Sparse)]
    public void SelectLod_VariousZoomLevels_ReturnsExpectedLod(
        double zoomLevel,
        double viewportSize,
        PointCloudLodLevel expectedLod)
    {
        // Arrange
        var bbox = new BoundingBox3D(
            -122.5, 37.7, 0,
            -122.5 + Math.Sqrt(viewportSize),
            37.7 + Math.Sqrt(viewportSize),
            100);

        // Act
        var lod = _selector.SelectLod(zoomLevel, bbox);

        // Assert
        Assert.Equal(expectedLod, lod);
    }

    [Fact]
    public void EstimatePointCount_FullLod_ReturnsFullCount()
    {
        // Arrange
        var bbox = new BoundingBox3D(-122.5, 37.7, 0, -122.4, 37.8, 100);
        var totalPoints = 1_000_000L;

        // Act
        var estimate = _selector.EstimatePointCount(bbox, totalPoints, PointCloudLodLevel.Full);

        // Assert
        Assert.Equal(totalPoints, estimate);
    }

    [Fact]
    public void EstimatePointCount_CoarseLod_Returns10Percent()
    {
        // Arrange
        var bbox = new BoundingBox3D(-122.5, 37.7, 0, -122.4, 37.8, 100);
        var totalPoints = 1_000_000L;

        // Act
        var estimate = _selector.EstimatePointCount(bbox, totalPoints, PointCloudLodLevel.Coarse);

        // Assert
        Assert.Equal(100_000L, estimate);
    }

    [Fact]
    public void EstimatePointCount_SparseLod_Returns1Percent()
    {
        // Arrange
        var bbox = new BoundingBox3D(-122.5, 37.7, 0, -122.4, 37.8, 100);
        var totalPoints = 1_000_000L;

        // Act
        var estimate = _selector.EstimatePointCount(bbox, totalPoints, PointCloudLodLevel.Sparse);

        // Assert
        Assert.Equal(10_000L, estimate);
    }
}

public class PointCloudClassificationColorsTests
{
    [Theory]
    [InlineData(0, 128, 128, 128)]  // Never Classified
    [InlineData(2, 139, 69, 19)]    // Ground
    [InlineData(6, 255, 0, 0)]      // Building
    [InlineData(9, 0, 0, 255)]      // Water
    public void GetColor_StandardClassifications_ReturnsCorrectColor(
        byte classification,
        byte expectedR,
        byte expectedG,
        byte expectedB)
    {
        // Act
        var color = PointCloudClassificationColors.GetColor(classification);

        // Assert
        Assert.Equal(expectedR, color.R);
        Assert.Equal(expectedG, color.G);
        Assert.Equal(expectedB, color.B);
    }

    [Fact]
    public void GetColor_UnknownClassification_ReturnsDefaultGray()
    {
        // Arrange
        byte unknownClassification = 99;

        // Act
        var color = PointCloudClassificationColors.GetColor(unknownClassification);

        // Assert
        Assert.Equal(200, color.R);
        Assert.Equal(200, color.G);
        Assert.Equal(200, color.B);
    }
}

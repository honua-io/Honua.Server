// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Services.Terrain;
using Microsoft.Extensions.Logging;
using Moq;

namespace Honua.MapSDK.Tests.Services.Terrain;

/// <summary>
/// Unit tests for TerrainTileService.
/// </summary>
public class TerrainTileServiceTests
{
    private readonly Mock<ILogger<TerrainTileService>> _loggerMock;
    private readonly Mock<IElevationService> _elevationServiceMock;
    private readonly TerrainTileService _service;

    public TerrainTileServiceTests()
    {
        _loggerMock = new Mock<ILogger<TerrainTileService>>();
        _elevationServiceMock = new Mock<IElevationService>();
        _service = new TerrainTileService(_loggerMock.Object, _elevationServiceMock.Object);
    }

    [Fact]
    public async Task GenerateTerrainRGBTileAsync_ReturnsValidTileData()
    {
        // Arrange
        var testGrid = CreateTestElevationGrid(256, 256);
        _elevationServiceMock
            .Setup(x => x.QueryAreaElevationAsync(
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(testGrid);

        // Act
        var tileData = await _service.GenerateTerrainRGBTileAsync(10, 163, 395, tileSize: 256);

        // Assert
        tileData.Should().NotBeNull();
        tileData.Should().HaveCount(256 * 256 * 3); // RGB data
    }

    [Fact]
    public async Task GenerateTerrainMeshTileAsync_GeneratesValidMesh()
    {
        // Arrange
        var testGrid = CreateTestElevationGrid(257, 257); // Power of 2 + 1
        _elevationServiceMock
            .Setup(x => x.QueryAreaElevationAsync(
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(testGrid);

        // Act
        var meshTile = await _service.GenerateTerrainMeshTileAsync(10, 163, 395, maxError: 1.0f);

        // Assert
        meshTile.Should().NotBeNull();
        meshTile.Z.Should().Be(10);
        meshTile.X.Should().Be(163);
        meshTile.Y.Should().Be(395);
        meshTile.Vertices.Should().NotBeEmpty();
        meshTile.Indices.Should().NotBeEmpty();
        meshTile.VertexCount.Should().BeGreaterThan(0);
        meshTile.TriangleCount.Should().BeGreaterThan(0);

        // Verify vertices format (x, y, z triplets)
        meshTile.Vertices.Length.Should().Be(meshTile.VertexCount * 3);

        // Verify indices format (triplets for triangles)
        meshTile.Indices.Length.Should().Be(meshTile.TriangleCount * 3);
    }

    [Fact]
    public async Task GenerateHillshadeTileAsync_GeneratesValidData()
    {
        // Arrange
        var testGrid = CreateTestElevationGrid(256, 256);
        _elevationServiceMock
            .Setup(x => x.QueryAreaElevationAsync(
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(testGrid);

        // Act
        var hillshadeData = await _service.GenerateHillshadeTileAsync(
            z: 10, x: 163, y: 395,
            azimuth: 315, altitude: 45);

        // Assert
        hillshadeData.Should().NotBeNull();
        hillshadeData.Length.Should().Be(256 * 256); // Grayscale data
    }

    [Fact]
    public async Task GenerateSlopeTileAsync_GeneratesValidData()
    {
        // Arrange
        var testGrid = CreateTestElevationGrid(256, 256);
        _elevationServiceMock
            .Setup(x => x.QueryAreaElevationAsync(
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(testGrid);

        // Act
        var slopeData = await _service.GenerateSlopeTileAsync(10, 163, 395);

        // Assert
        slopeData.Should().NotBeNull();
        slopeData.Length.Should().Be(256 * 256 * 3); // RGB data
    }

    [Fact]
    public async Task GetTileMetadataAsync_ReturnsCorrectMetadata()
    {
        // Arrange
        var testGrid = CreateTestElevationGrid(100, 100, minElev: 0, maxElev: 1000);
        _elevationServiceMock
            .Setup(x => x.QueryAreaElevationAsync(
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(testGrid);

        // Act
        var metadata = await _service.GetTileMetadataAsync(10, 163, 395);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Z.Should().Be(10);
        metadata.X.Should().Be(163);
        metadata.Y.Should().Be(395);
        metadata.MinElevation.Should().BeGreaterOrEqualTo(0);
        metadata.MaxElevation.Should().BeLessOrEqualTo(1000);
        metadata.MeanElevation.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(10, 163, 395)]
    [InlineData(15, 5242, 12661)]
    public async Task GenerateTerrainRGBTileAsync_CachesResults(int z, int x, int y)
    {
        // Arrange
        var testGrid = CreateTestElevationGrid(256, 256);
        _elevationServiceMock
            .Setup(s => s.QueryAreaElevationAsync(
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(testGrid);

        // Act - Generate tile twice
        var tile1 = await _service.GenerateTerrainRGBTileAsync(z, x, y);
        var tile2 = await _service.GenerateTerrainRGBTileAsync(z, x, y);

        // Assert - Should be same data (cached)
        tile1.Should().Equal(tile2);

        // Verify elevation service was only called once (cached on second call)
        _elevationServiceMock.Verify(
            s => s.QueryAreaElevationAsync(
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<int>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void TileMath_TileToBounds_CalculatesCorrectBounds()
    {
        // Act
        var bounds = TileMath.TileToBounds(10, 163, 395);

        // Assert
        bounds.Should().NotBeNull();
        bounds.MinLon.Should().BeLessThan(bounds.MaxLon);
        bounds.MinLat.Should().BeLessThan(bounds.MaxLat);

        // Verify bounds are within valid geographic range
        bounds.MinLon.Should().BeGreaterOrEqualTo(-180);
        bounds.MaxLon.Should().BeLessOrEqualTo(180);
        bounds.MinLat.Should().BeGreaterOrEqualTo(-90);
        bounds.MaxLat.Should().BeLessOrEqualTo(90);
    }

    [Fact]
    public void TileMath_LatLonToTile_CalculatesCorrectTile()
    {
        // Arrange
        var lat = 37.7749;
        var lon = -122.4194;
        var z = 10;

        // Act
        var (x, y) = TileMath.LatLonToTile(lat, lon, z);

        // Assert
        x.Should().BeGreaterOrEqualTo(0);
        y.Should().BeGreaterOrEqualTo(0);
        x.Should().BeLessThan(Math.Pow(2, z));
        y.Should().BeLessThan(Math.Pow(2, z));
    }

    [Fact]
    public void TileMath_RoundTrip_PreservesLocation()
    {
        // Arrange
        var originalLat = 37.7749;
        var originalLon = -122.4194;
        var z = 10;

        // Act - Convert to tile and back to bounds
        var (x, y) = TileMath.LatLonToTile(originalLat, originalLon, z);
        var bounds = TileMath.TileToBounds(z, x, y);

        // Assert - Original point should be within tile bounds
        originalLon.Should().BeGreaterOrEqualTo(bounds.MinLon);
        originalLon.Should().BeLessOrEqualTo(bounds.MaxLon);
        originalLat.Should().BeGreaterOrEqualTo(bounds.MinLat);
        originalLat.Should().BeLessOrEqualTo(bounds.MaxLat);
    }

    private static ElevationGrid CreateTestElevationGrid(
        int width,
        int height,
        float minElev = 0,
        float maxElev = 100)
    {
        var data = new float[height, width];
        var random = new Random(42); // Deterministic for testing

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                data[y, x] = minElev + (float)random.NextDouble() * (maxElev - minElev);
            }
        }

        return new ElevationGrid
        {
            Data = data,
            Width = width,
            Height = height,
            MinLongitude = -122.6,
            MinLatitude = 37.7,
            MaxLongitude = -122.5,
            MaxLatitude = 37.8,
            Resolution = width
        };
    }
}

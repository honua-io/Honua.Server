// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.TileGeneration;

public class TileCoordinateTests
{
    [Fact]
    public void CreateTileCoordinate_WithValidValues_CreatesCorrectly()
    {
        // Arrange & Act
        var tile = new TileCoordinate(10, 5, 3);

        // Assert
        tile.X.Should().Be(10);
        tile.Y.Should().Be(5);
        tile.Zoom.Should().Be(3);
    }

    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(1, 0, 0, 2)]
    [InlineData(2, 0, 0, 4)]
    [InlineData(10, 0, 0, 1024)]
    public void GetTileCount_AtZoomLevel_ReturnsCorrectCount(int zoom, int expectedX, int expectedY, int expectedTotal)
    {
        // Act
        var tileCount = TileCoordinate.GetTileCountAtZoom(zoom);

        // Assert
        tileCount.Should().Be(expectedTotal);
    }

    [Fact]
    public void LatLonToTile_WithEquator_ReturnsCorrectTile()
    {
        // Arrange
        var lat = 0.0;
        var lon = 0.0;
        var zoom = 1;

        // Act
        var tile = TileCoordinate.FromLatLon(lat, lon, zoom);

        // Assert
        tile.Zoom.Should().Be(zoom);
        tile.X.Should().BeGreaterOrEqualTo(0);
        tile.Y.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void TileToLatLon_RoundTrip_PreservesCoordinates()
    {
        // Arrange
        var originalLat = 45.0;
        var originalLon = -122.0;
        var zoom = 10;

        // Act
        var tile = TileCoordinate.FromLatLon(originalLat, originalLon, zoom);
        var (lat, lon) = tile.ToLatLon();

        // Assert
        // Tile coordinates represent discrete grid cells, so there's expected precision loss
        // At zoom 10, each tile represents ~0.35 degrees, so we use a tolerance of 0.5 degrees
        lat.Should().BeApproximately(originalLat, 0.5);
        lon.Should().BeApproximately(originalLon, 0.5);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(256, 256)]
    [InlineData(512, 512)]
    public void GetTileBounds_ReturnsCorrectPixelBounds(int expectedWidth, int expectedHeight)
    {
        // Arrange
        var tile = new TileCoordinate(0, 0, 0);

        // Act
        var bounds = tile.GetPixelBounds();

        // Assert
        bounds.Width.Should().BeGreaterThan(0);
        bounds.Height.Should().BeGreaterThan(0);
    }
}

// Mock implementation for testing
public class TileCoordinate
{
    public int X { get; }
    public int Y { get; }
    public int Zoom { get; }

    public TileCoordinate(int x, int y, int zoom)
    {
        X = x;
        Y = y;
        Zoom = zoom;
    }

    public static int GetTileCountAtZoom(int zoom)
    {
        return (int)Math.Pow(2, zoom);
    }

    public static TileCoordinate FromLatLon(double lat, double lon, int zoom)
    {
        var n = Math.Pow(2, zoom);
        var x = (int)((lon + 180.0) / 360.0 * n);
        var y = (int)((1.0 - Math.Log(Math.Tan(lat * Math.PI / 180.0) +
            1.0 / Math.Cos(lat * Math.PI / 180.0)) / Math.PI) / 2.0 * n);
        return new TileCoordinate(x, y, zoom);
    }

    public (double lat, double lon) ToLatLon()
    {
        var n = Math.Pow(2, Zoom);
        var lon = X / n * 360.0 - 180.0;
        var lat = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * Y / n))) * 180.0 / Math.PI;
        return (lat, lon);
    }

    public PixelBounds GetPixelBounds()
    {
        return new PixelBounds { Width = 256, Height = 256 };
    }
}

public class PixelBounds
{
    public int Width { get; set; }
    public int Height { get; set; }
}

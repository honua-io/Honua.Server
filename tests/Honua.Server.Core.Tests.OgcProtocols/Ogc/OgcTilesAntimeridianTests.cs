using System;
using FluentAssertions;
using Honua.Server.Host.Ogc;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Ogc;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "OGC")]
[Trait("Speed", "Fast")]
public class OgcTilesAntimeridianTests
{
    [Theory]
    [InlineData(-170.0, 170.0, false)] // Standard bbox, not crossing
    [InlineData(170.0, -170.0, true)]  // Crosses antimeridian: minX > maxX
    [InlineData(175.0, -175.0, true)]  // Crosses antimeridian
    [InlineData(-10.0, 10.0, false)]   // Normal bbox around prime meridian
    [InlineData(179.0, -179.0, true)]  // Crosses very close to antimeridian
    public void CrossesAntimeridian_ShouldDetectCorrectly(double minX, double maxX, bool expectedCrossing)
    {
        // Act
        var result = OgcTileMatrixHelper.CrossesAntimeridian(minX, maxX);

        // Assert
        result.Should().Be(expectedCrossing);
    }

    [Fact]
    public void SplitAntimeridianBbox_WhenNotCrossing_ShouldReturnSingleBbox()
    {
        // Arrange
        var minX = -170.0;
        var minY = -10.0;
        var maxX = 170.0;
        var maxY = 10.0;

        // Act
        var result = OgcTileMatrixHelper.SplitAntimeridianBbox(minX, minY, maxX, maxY);

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().BeEquivalentTo(new[] { minX, minY, maxX, maxY });
    }

    [Fact]
    public void SplitAntimeridianBbox_WhenCrossing_ShouldReturnTwoBboxes()
    {
        // Arrange
        var minX = 170.0;
        var minY = -10.0;
        var maxX = -170.0;
        var maxY = 10.0;

        // Act
        var result = OgcTileMatrixHelper.SplitAntimeridianBbox(minX, minY, maxX, maxY);

        // Assert
        result.Should().HaveCount(2);

        // Western hemisphere: [170, -10, 180, 10]
        result[0].Should().BeEquivalentTo(new[] { 170.0, -10.0, 180.0, 10.0 });

        // Eastern hemisphere: [-180, -10, -170, 10]
        result[1].Should().BeEquivalentTo(new[] { -180.0, -10.0, -170.0, 10.0 });
    }

    [Fact]
    public void SplitAntimeridianBbox_PacificCentered_ShouldSplitCorrectly()
    {
        // Arrange: Bbox centered on Pacific (180°)
        var minX = 160.0;
        var minY = -20.0;
        var maxX = -160.0;
        var maxY = 20.0;

        // Act
        var result = OgcTileMatrixHelper.SplitAntimeridianBbox(minX, minY, maxX, maxY);

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().BeEquivalentTo(new[] { 160.0, -20.0, 180.0, 20.0 });
        result[1].Should().BeEquivalentTo(new[] { -180.0, -20.0, -160.0, 20.0 });
    }

    [Theory]
    [InlineData(190.0, -170.0)]   // 190 wraps to -170
    [InlineData(-190.0, 170.0)]   // -190 wraps to 170
    [InlineData(360.0, 0.0)]      // 360 wraps to 0
    [InlineData(-360.0, 0.0)]     // -360 wraps to 0
    [InlineData(540.0, -180.0)]   // Multiple wraps
    [InlineData(0.0, 0.0)]        // No change needed
    [InlineData(180.0, 180.0)]    // Edge case at antimeridian
    [InlineData(-180.0, -180.0)]  // Edge case at antimeridian
    public void NormalizeLongitude_ShouldWrapCorrectly(double input, double expected)
    {
        // Act
        var result = OgcTileMatrixHelper.NormalizeLongitude(input);

        // Assert
        result.Should().BeApproximately(expected, 0.0001);
    }

    [Fact]
    public void GetBoundingBox_WorldCrs84Quad_Zoom0_Tile0_0_ShouldCoverWesternHemisphere()
    {
        // Arrange: At zoom 0, WorldCRS84Quad has 2 columns × 1 row
        // Tile 0,0 covers western hemisphere, Tile 0,1 covers eastern hemisphere
        var zoom = 0;
        var row = 0;
        var col = 0;

        // Act
        var bbox = OgcTileMatrixHelper.GetBoundingBox("WorldCRS84Quad", zoom, row, col);

        // Assert
        bbox.Should().HaveCount(4);
        bbox[0].Should().BeApproximately(-180.0, 0.001); // minX
        bbox[1].Should().BeApproximately(-90.0, 0.001);  // minY
        bbox[2].Should().BeApproximately(0.0, 0.001);    // maxX - western hemisphere only
        bbox[3].Should().BeApproximately(90.0, 0.001);   // maxY
    }

    [Fact]
    public void GetBoundingBox_WorldCrs84Quad_Zoom1_RightmostTile_ShouldApproachAntimeridian()
    {
        // Arrange: At zoom 1, WorldCRS84Quad has 4 columns × 2 rows (OGC standard quad-tree doubling)
        var zoom = 1;
        var row = 0;
        var col = 3; // Rightmost tile in top row (columns 0-3)

        // Act
        var bbox = OgcTileMatrixHelper.GetBoundingBox("WorldCRS84Quad", zoom, row, col);

        // Assert
        bbox.Should().HaveCount(4);
        bbox[0].Should().BeApproximately(90.0, 0.001);   // minX
        bbox[1].Should().BeApproximately(0.0, 0.001);    // minY
        bbox[2].Should().BeApproximately(180.0, 0.001);  // maxX - reaches antimeridian
        bbox[3].Should().BeApproximately(90.0, 0.001);   // maxY
    }

    [Fact]
    public void GetBoundingBox_WorldCrs84Quad_Zoom2_TileNearAntimeridian_ShouldBeValid()
    {
        // Arrange: At zoom 2, WorldCRS84Quad has 8 columns × 4 rows (OGC standard quad-tree doubling)
        var zoom = 2;
        var row = 1;
        var col = 7; // Rightmost tile in second row (columns 0-7)

        // Act
        var bbox = OgcTileMatrixHelper.GetBoundingBox("WorldCRS84Quad", zoom, row, col);

        // Assert
        bbox.Should().HaveCount(4);
        bbox[0].Should().BeApproximately(135.0, 0.001);  // minX
        bbox[1].Should().BeApproximately(0.0, 0.001);    // minY
        bbox[2].Should().BeApproximately(180.0, 0.001);  // maxX - reaches antimeridian
        bbox[3].Should().BeApproximately(45.0, 0.001);   // maxY

        // Should not cross antimeridian
        OgcTileMatrixHelper.CrossesAntimeridian(bbox[0], bbox[2]).Should().BeFalse();
    }

    [Fact]
    public void GetBoundingBox_WorldWebMercatorQuad_ShouldNeverCrossAntimeridian()
    {
        // Web Mercator projection doesn't extend to ±180° longitude
        // It's limited to approximately ±85.05° latitude and uses meters, not degrees

        // Arrange: Test various tiles
        var testCases = new[]
        {
            (zoom: 0, row: 0, col: 0),
            (zoom: 1, row: 0, col: 1),
            (zoom: 2, row: 1, col: 3),
            (zoom: 5, row: 10, col: 31)
        };

        foreach (var (zoom, row, col) in testCases)
        {
            // Act
            var bbox = OgcTileMatrixHelper.GetBoundingBox("WorldWebMercatorQuad", zoom, row, col);

            // Assert
            bbox.Should().HaveCount(4);

            // Web Mercator uses projected coordinates in meters, not degrees
            // The values should be within the Web Mercator bounds (~±20037508 meters)
            bbox[0].Should().BeGreaterThanOrEqualTo(-20037509.0);
            bbox[2].Should().BeLessThanOrEqualTo(20037509.0);

            // In Web Mercator, minX should always be less than maxX
            bbox[0].Should().BeLessThan(bbox[2]);
        }
    }

    [Theory]
    [InlineData("WorldCRS84Quad", 0, 0, 0, false)]   // Zoom 0 covers entire world
    [InlineData("WorldCRS84Quad", 1, 0, 0, false)]   // Western hemisphere
    [InlineData("WorldCRS84Quad", 1, 0, 1, false)]   // Eastern hemisphere
    [InlineData("WorldCRS84Quad", 10, 500, 1023, false)] // Far east tile
    [InlineData("WorldWebMercatorQuad", 0, 0, 0, false)] // Web Mercator never crosses
    [InlineData("WorldWebMercatorQuad", 5, 10, 31, false)]
    public void GetBoundingBox_ValidTiles_ShouldNotCrossAntimeridianInStandardCases(
        string tileMatrixSetId, int zoom, int row, int col, bool shouldCross)
    {
        // Act
        var bbox = OgcTileMatrixHelper.GetBoundingBox(tileMatrixSetId, zoom, row, col);

        // Assert
        if (tileMatrixSetId.Contains("CRS84", StringComparison.OrdinalIgnoreCase))
        {
            // For geographic CRS, check if crosses antimeridian
            var crosses = OgcTileMatrixHelper.CrossesAntimeridian(bbox[0], bbox[2]);
            crosses.Should().Be(shouldCross);
        }
        else
        {
            // For Web Mercator, minX should always be < maxX
            bbox[0].Should().BeLessThan(bbox[2]);
        }
    }

    [Fact]
    public void GetTileRange_WithAntimeridianCrossingBbox_ShouldReturnWrappedIndices()
    {
        // Arrange: Bbox that crosses antimeridian
        var zoom = 5;
        var minX = 170.0;
        var minY = -10.0;
        var maxX = -170.0; // minX > maxX indicates crossing
        var maxY = 10.0;

        // Act
        var (minRow, maxRow, minCol, maxCol) = OgcTileMatrixHelper.GetTileRange(
            "WorldCRS84Quad", zoom, minX, minY, maxX, maxY);

        // Assert
        minRow.Should().BeGreaterThanOrEqualTo(0);
        maxRow.Should().BeGreaterThanOrEqualTo(0);

        // When crossing antimeridian, minCol > maxCol indicates wraparound
        // This is by design as indicated in the BUG FIX #3 comment
        minCol.Should().BeGreaterThan(maxCol, "minCol > maxCol indicates antimeridian wraparound");
    }

    [Fact]
    public void GetTileRange_WithNormalBbox_ShouldReturnNormalIndices()
    {
        // Arrange: Normal bbox that doesn't cross antimeridian
        var zoom = 5;
        var minX = -170.0;
        var minY = -10.0;
        var maxX = 170.0;
        var maxY = 10.0;

        // Act
        var (minRow, maxRow, minCol, maxCol) = OgcTileMatrixHelper.GetTileRange(
            "WorldCRS84Quad", zoom, minX, minY, maxX, maxY);

        // Assert
        minRow.Should().BeGreaterThanOrEqualTo(0);
        maxRow.Should().BeGreaterThanOrEqualTo(minRow);
        minCol.Should().BeGreaterThanOrEqualTo(0);
        maxCol.Should().BeGreaterThanOrEqualTo(minCol);
    }
}

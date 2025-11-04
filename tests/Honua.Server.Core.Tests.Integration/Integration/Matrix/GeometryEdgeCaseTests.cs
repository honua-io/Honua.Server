using System.Text.Json.Nodes;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Postgres;
using Honua.Server.Core.Data.Sqlite;
using Honua.Server.Core.Tests.Shared;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Integration.Integration.Matrix;

/// <summary>
/// Comprehensive geometry edge case tests covering degenerate geometries,
/// extreme coordinates, and production-observed edge cases.
/// </summary>
[Collection("MultiProvider")]
[Trait("Category", "Integration")]
[Trait("Category", "EdgeCases")]
public sealed class GeometryEdgeCaseTests
{
    private readonly ITestOutputHelper _output;
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    public GeometryEdgeCaseTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Degenerate Geometry Tests

    [Fact]
    public void NullIsland_Coordinate_ShouldBeHandledCorrectly()
    {
        // Arrange - Point at (0,0), where equator meets prime meridian
        // This is a common error coordinate when geocoding fails
        var nullIsland = RealisticGisTestData.CreateNullIsland();

        // Assert
        nullIsland.X.Should().Be(0.0);
        nullIsland.Y.Should().Be(0.0);
        nullIsland.SRID.Should().Be(4326);
        nullIsland.IsValid.Should().BeTrue();

        _output.WriteLine($"Null Island: {nullIsland.Coordinate}");
    }

    [Fact]
    public void DegeneratePolygon_ZeroArea_ShouldBeDetectable()
    {
        // Arrange - All points on same line (zero area)
        var degeneratePolygon = RealisticGisTestData.CreateDegeneratePolygon_ZeroArea();

        // Assert
        degeneratePolygon.Area.Should().Be(0.0, "degenerate polygon should have zero area");
        degeneratePolygon.IsValid.Should().BeTrue("degenerate but topologically valid");

        _output.WriteLine($"Degenerate polygon area: {degeneratePolygon.Area}");
        _output.WriteLine($"Degenerate polygon WKT: {degeneratePolygon.AsText()}");
    }

    [Fact]
    public void DegenerateLineString_ZeroLength_ShouldBeDetectable()
    {
        // Arrange - Same point repeated
        var degenerateLine = RealisticGisTestData.CreateDegenerateLineString_ZeroLength();

        // Assert
        degenerateLine.Length.Should().Be(0.0, "degenerate line should have zero length");
        degenerateLine.NumPoints.Should().Be(2);
        degenerateLine.IsValid.Should().BeTrue();

        _output.WriteLine($"Degenerate line length: {degenerateLine.Length}");
    }

    #endregion

    #region Antimeridian Crossing Tests

    [Fact]
    public void AntimeridianCrossing_WestToEast_ShouldPreserveCoordinates()
    {
        // Arrange
        var line = RealisticGisTestData.CreateAntimeridianCrossingLine_WestToEast();

        // Assert
        line.Coordinates.Should().HaveCount(2);
        line.Coordinates[0].X.Should().BeApproximately(179.9, 0.001);
        line.Coordinates[1].X.Should().BeApproximately(-179.9, 0.001);

        _output.WriteLine($"Antimeridian crossing (W→E): {line.AsText()}");
    }

    [Fact]
    public void AntimeridianCrossing_EastToWest_ShouldPreserveCoordinates()
    {
        // Arrange
        var line = RealisticGisTestData.CreateAntimeridianCrossingLine_EastToWest();

        // Assert
        line.Coordinates.Should().HaveCount(2);
        line.Coordinates[0].X.Should().BeApproximately(-179.9, 0.001);
        line.Coordinates[1].X.Should().BeApproximately(179.9, 0.001);

        _output.WriteLine($"Antimeridian crossing (E→W): {line.AsText()}");
    }

    [Fact]
    public void AntimeridianCrossing_Polygon_ShouldPreserveCoordinates()
    {
        // Arrange - Polygon spanning Pacific Ocean
        var polygon = RealisticGisTestData.CreateAntimeridianCrossingPolygon();

        // Assert
        polygon.IsValid.Should().BeTrue();
        var coords = polygon.Coordinates;

        // Should have coordinates on both sides of antimeridian
        var hasEastSide = coords.Any(c => c.X > 170);
        var hasWestSide = coords.Any(c => c.X < -170);

        hasEastSide.Should().BeTrue("should have coordinates east of antimeridian");
        hasWestSide.Should().BeTrue("should have coordinates west of antimeridian");

        _output.WriteLine($"Antimeridian polygon: {polygon.AsText()}");
    }

    #endregion

    #region Polar Region Tests

    [Fact]
    public void NorthPole_Polygon_ShouldHandleHighLatitudes()
    {
        // Arrange
        var northPolePolygon = RealisticGisTestData.CreateNorthPolePolygon();

        // Assert
        northPolePolygon.IsValid.Should().BeTrue();
        var maxLat = northPolePolygon.Coordinates.Max(c => c.Y);
        maxLat.Should().BeGreaterThanOrEqualTo(89.0, "should include high northern latitudes");

        _output.WriteLine($"North Pole polygon max lat: {maxLat}°");
        _output.WriteLine($"North Pole polygon WKT: {northPolePolygon.AsText()}");
    }

    [Fact]
    public void SouthPole_Polygon_ShouldHandleHighSouthernLatitudes()
    {
        // Arrange
        var southPolePolygon = RealisticGisTestData.CreateSouthPolePolygon();

        // Assert
        southPolePolygon.IsValid.Should().BeTrue();
        var minLat = southPolePolygon.Coordinates.Min(c => c.Y);
        minLat.Should().BeLessThanOrEqualTo(-89.0, "should include high southern latitudes");

        _output.WriteLine($"South Pole polygon min lat: {minLat}°");
    }

    [Fact]
    public void Alert_Canada_ExtremNorthernCoordinate_ShouldBeValid()
    {
        // Arrange - Alert, Nunavut: northernmost permanently inhabited place
        var (lon, lat) = RealisticGisTestData.Alert;
        var point = Factory.CreatePoint(new Coordinate(lon, lat));

        // Assert
        point.Y.Should().BeGreaterThan(82.0, "Alert is at ~82.5°N");
        point.IsValid.Should().BeTrue();

        _output.WriteLine($"Alert, Canada: ({lon}, {lat})");
    }

    [Fact]
    public void Ushuaia_Argentina_ExtremeSouthernCoordinate_ShouldBeValid()
    {
        // Arrange - Ushuaia: southernmost city
        var (lon, lat) = RealisticGisTestData.Ushuaia;
        var point = Factory.CreatePoint(new Coordinate(lon, lat));

        // Assert
        point.Y.Should().BeLessThan(-54.0, "Ushuaia is at ~54.8°S");
        point.IsValid.Should().BeTrue();

        _output.WriteLine($"Ushuaia, Argentina: ({lon}, {lat})");
    }

    #endregion

    #region Meridian and Equator Tests

    [Fact]
    public void PrimeMeridian_VerticalLine_ShouldBeValid()
    {
        // Arrange
        var primeMeridian = RealisticGisTestData.CreatePrimeMeridianLine();

        // Assert
        primeMeridian.IsValid.Should().BeTrue();
        primeMeridian.Coordinates.Should().AllSatisfy(c => c.X.Should().Be(0.0));
        primeMeridian.Length.Should().BeGreaterThan(0);

        _output.WriteLine($"Prime Meridian line: {primeMeridian.AsText()}");
    }

    [Fact]
    public void Equator_HorizontalLine_ShouldBeValid()
    {
        // Arrange
        var equator = RealisticGisTestData.CreateEquatorLine();

        // Assert
        equator.IsValid.Should().BeTrue();
        equator.Coordinates.Should().AllSatisfy(c => c.Y.Should().Be(0.0));
        equator.Length.Should().BeGreaterThan(0);

        _output.WriteLine($"Equator line: {equator.AsText()}");
    }

    #endregion

    #region Coordinate Precision Tests

    [Fact]
    public void MaxPrecision_Point_ShouldPreserveReasonablePrecision()
    {
        // Arrange
        var maxPrecisionPoint = RealisticGisTestData.CreateMaxPrecisionPoint();

        // Assert - Most databases preserve 6-8 decimal places
        // 8 decimal places = ~1.1mm precision at equator, which is sufficient
        maxPrecisionPoint.X.Should().BeApproximately(-122.419412345678901, 1e-8,
            "should preserve at least 8 decimal places");
        maxPrecisionPoint.Y.Should().BeApproximately(45.523123456789012, 1e-8,
            "should preserve at least 8 decimal places");

        _output.WriteLine($"Max precision point: X={maxPrecisionPoint.X:F15}, Y={maxPrecisionPoint.Y:F15}");
    }

    [Fact]
    public void SubnormalPrecision_Point_ShouldHandleSmallValues()
    {
        // Arrange
        var subnormalPoint = RealisticGisTestData.CreateSubnormalPrecisionPoint();

        // Assert
        subnormalPoint.X.Should().BeApproximately(1.0e-10, 1e-11);
        subnormalPoint.Y.Should().BeApproximately(1.0e-10, 1e-11);
        subnormalPoint.IsValid.Should().BeTrue();

        _output.WriteLine($"Subnormal precision point: X={subnormalPoint.X:E}, Y={subnormalPoint.Y:E}");
    }

    #endregion

    #region Large and Complex Geometry Tests

    [Fact]
    public void LargeParcel_1000PlusVertices_ShouldBeValid()
    {
        // Arrange
        var largeParcel = RealisticGisTestData.CreateLargeParcel();

        // Assert
        largeParcel.NumPoints.Should().BeGreaterThan(1000, "should have 1000+ vertices");
        largeParcel.IsValid.Should().BeTrue();
        largeParcel.Area.Should().BeGreaterThan(0);

        _output.WriteLine($"Large parcel vertices: {largeParcel.NumPoints}");
        _output.WriteLine($"Large parcel area: {largeParcel.Area:F6} square degrees");
    }

    [Fact]
    public void ParcelWithMultipleHoles_ShouldPreserveHoles()
    {
        // Arrange
        var parcelWithHoles = RealisticGisTestData.CreateParcelWithMultipleHoles();

        // Assert
        parcelWithHoles.IsValid.Should().BeTrue();
        parcelWithHoles.NumInteriorRings.Should().Be(3, "should have 3 interior holes");
        parcelWithHoles.Area.Should().BeGreaterThan(0);

        // Calculate total hole area
        var totalHoleArea = 0.0;
        for (var i = 0; i < parcelWithHoles.NumInteriorRings; i++)
        {
            var hole = (NetTopologySuite.Geometries.LinearRing)parcelWithHoles.GetInteriorRingN(i);
            var holePolygon = Factory.CreatePolygon(hole);
            totalHoleArea += holePolygon.Area;
        }

        var shellArea = Factory.CreatePolygon(parcelWithHoles.Shell).Area;
        var netArea = shellArea - totalHoleArea;

        netArea.Should().BeApproximately(parcelWithHoles.Area, 1e-6,
            "net area should equal polygon area (shell minus holes)");

        _output.WriteLine($"Parcel with holes - Shell area: {shellArea:F6}, Hole area: {totalHoleArea:F6}, Net area: {netArea:F6}");
    }

    #endregion

    #region Real-World City Coordinates

    [Theory]
    [InlineData("New York", -73.9855, 40.7580)]
    [InlineData("Tokyo", 139.7006, 35.6595)]
    [InlineData("Sydney", 151.2153, -33.8568)]
    [InlineData("London", -0.1246, 51.5007)]
    [InlineData("São Paulo", -46.6333, -23.5505)]
    [InlineData("Mumbai", 72.8777, 19.0760)]
    [InlineData("Cairo", 31.2357, 30.0444)]
    [InlineData("Moscow", 37.6173, 55.7558)]
    [InlineData("Reykjavik", -21.8952, 64.1466)]
    public void RealWorldCity_Coordinates_ShouldBeValid(string cityName, double lon, double lat)
    {
        // Arrange
        var point = Factory.CreatePoint(new Coordinate(lon, lat));

        // Assert
        point.IsValid.Should().BeTrue($"{cityName} coordinates should be valid");
        point.X.Should().BeInRange(-180, 180, $"{cityName} longitude should be in valid range");
        point.Y.Should().BeInRange(-90, 90, $"{cityName} latitude should be in valid range");

        _output.WriteLine($"{cityName}: ({lon}, {lat})");
    }

    #endregion

    #region Coordinate Validation Edge Cases

    [Theory]
    [InlineData(-180.0, 0.0)] // Western edge
    [InlineData(180.0, 0.0)] // Eastern edge
    [InlineData(0.0, -90.0)] // South pole
    [InlineData(0.0, 90.0)] // North pole
    [InlineData(-180.0, -90.0)] // SW corner
    [InlineData(180.0, 90.0)] // NE corner
    public void BoundaryCoordinates_ShouldBeValid(double lon, double lat)
    {
        // Arrange
        var point = Factory.CreatePoint(new Coordinate(lon, lat));

        // Assert
        point.IsValid.Should().BeTrue($"boundary coordinate ({lon}, {lat}) should be valid");

        _output.WriteLine($"Boundary coordinate: ({lon}, {lat})");
    }

    [Fact]
    public void Fiji_NearAntimeridian_ShouldBeValid()
    {
        // Arrange - Fiji crosses the antimeridian
        var (lon, lat) = RealisticGisTestData.Fiji;
        var point = Factory.CreatePoint(new Coordinate(lon, lat));

        // Assert
        point.X.Should().BeGreaterThan(178.0, "Fiji is near antimeridian");
        point.IsValid.Should().BeTrue();

        _output.WriteLine($"Fiji coordinates: ({lon}, {lat})");
    }

    #endregion

    #region GeoJSON Round-Trip Tests

    [Fact]
    public void ComplexGeometry_GeoJsonRoundTrip_ShouldPreserveStructure()
    {
        // Arrange
        var original = RealisticGisTestData.CreateParcelWithMultipleHoles();
        var writer = new GeoJsonWriter();
        var reader = new GeoJsonReader();

        // Act - Write to GeoJSON and read back
        var geoJson = writer.Write(original);
        _output.WriteLine($"GeoJSON: {geoJson}");

        var roundTripped = reader.Read<Polygon>(geoJson);

        // Assert
        roundTripped.NumInteriorRings.Should().Be(original.NumInteriorRings,
            "should preserve number of holes in round-trip");
        roundTripped.NumPoints.Should().Be(original.NumPoints,
            "should preserve number of points in round-trip");
        roundTripped.Area.Should().BeApproximately(original.Area, 1e-6,
            "should preserve area in round-trip");
    }

    [Fact]
    public void AntimeridianPolygon_GeoJsonRoundTrip_ShouldPreserveCoordinates()
    {
        // Arrange
        var original = RealisticGisTestData.CreateAntimeridianCrossingPolygon();
        var writer = new GeoJsonWriter();
        var reader = new GeoJsonReader();

        // Act
        var geoJson = writer.Write(original);
        var roundTripped = reader.Read<Polygon>(geoJson);

        // Assert
        var originalHasWest = original.Coordinates.Any(c => c.X < -170);
        var originalHasEast = original.Coordinates.Any(c => c.X > 170);
        var roundTrippedHasWest = roundTripped.Coordinates.Any(c => c.X < -170);
        var roundTrippedHasEast = roundTripped.Coordinates.Any(c => c.X > 170);

        originalHasWest.Should().Be(roundTrippedHasWest, "should preserve west side coordinates");
        originalHasEast.Should().Be(roundTrippedHasEast, "should preserve east side coordinates");
    }

    #endregion
}

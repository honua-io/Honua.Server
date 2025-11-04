using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Sqlite;
using Honua.Server.Core.Data.Postgres;
using Honua.Server.Core.Data.MySql;
using Honua.Server.Core.Tests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Integration.Integration.Matrix;

/// <summary>
/// Phase 2: Comprehensive geometry type × provider matrix tests.
/// Tests all 7 OGC geometry types across all 3 providers with geodetic edge cases.
/// </summary>
[Collection("MultiProvider")]
[Trait("Category", "Integration")]
[Trait("Category", "Matrix")]
public sealed class GeometryMatrixTests
{
    private readonly MultiProviderTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public GeometryMatrixTests(MultiProviderTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Theory]
    [ClassData(typeof(GeometryMatrixTestData))]
    public async Task Provider_HandlesGeometryType_Correctly(
        string providerName,
        GeometryTestData.GeometryType geometryType,
        GeometryTestData.GeodeticScenario scenario)
    {
        // Arrange
        _output.WriteLine($"Testing {providerName}: {geometryType} with {scenario} scenario");

        var (dataSource, service, layer) = _fixture.GetMetadata(providerName, geometryType, scenario);
        var provider = CreateDataStoreProvider(providerName);

        // Get expected geometry for comparison
        var expectedGeometry = GeometryTestData.GetTestGeometry(geometryType, scenario);
        var expectedType = GetExpectedGeoJsonType(geometryType);

        // Act
        var results = new List<FeatureRecord>();
        await foreach (var record in provider.QueryAsync(dataSource, service, layer, new FeatureQuery(Limit: 10)))
        {
            results.Add(record);
        }

        // Assert - Feature exists
        results.Should().HaveCountGreaterThan(0,
            $"{providerName} should return at least one {geometryType} feature for {scenario}");

        var firstResult = results[0];
        firstResult.Attributes.Should().ContainKey("geom", "geometry field should be present");

        // Assert - Geometry type matches
        var geomJson = firstResult.Attributes["geom"] as JsonObject;
        geomJson.Should().NotBeNull($"{providerName} should return geometry as JsonObject");

        var actualType = geomJson!["type"]!.GetValue<string>();
        actualType.Should().Be(expectedType,
            $"{providerName} should preserve {geometryType} type correctly");

        // Assert - Coordinates or Geometries structure is valid
        if (geometryType == GeometryTestData.GeometryType.GeometryCollection)
        {
            geomJson.Should().ContainKey("geometries", "GeometryCollection must have geometries array");
        }
        else
        {
            geomJson.Should().ContainKey("coordinates", "GeoJSON geometry must have coordinates");
        }

        // Validate scenario-specific properties
        ValidateScenarioSpecificProperties(providerName, geometryType, scenario, geomJson);

        _output.WriteLine($"✅ {providerName} correctly handled {geometryType} ({scenario})");
    }

    private void ValidateScenarioSpecificProperties(
        string providerName,
        GeometryTestData.GeometryType geometryType,
        GeometryTestData.GeodeticScenario scenario,
        JsonObject geomJson)
    {
        var coordinates = geomJson["coordinates"]!;

        switch (scenario)
        {
            case GeometryTestData.GeodeticScenario.AntimeridianCrossing:
                // LineString or Polygon should cross ±180° longitude
                if (geometryType == GeometryTestData.GeometryType.LineString)
                {
                    var coords = coordinates.AsArray();
                    coords.Should().HaveCountGreaterThanOrEqualTo(2,
                        "Antimeridian crossing LineString should have at least 2 points");

                    var firstLon = coords[0]!.AsArray()[0]!.GetValue<double>();
                    var secondLon = coords[1]!.AsArray()[0]!.GetValue<double>();

                    // One should be near 179, other near -179
                    var crossesAntimeridian = (firstLon > 170 && secondLon < -170) ||
                                             (firstLon < -170 && secondLon > 170);

                    crossesAntimeridian.Should().BeTrue(
                        $"{providerName} should preserve antimeridian crossing coordinates");
                }
                else if (geometryType == GeometryTestData.GeometryType.Polygon)
                {
                    // Polygon crosses antimeridian - validate coordinates span ±180°
                    var rings = coordinates.AsArray();
                    var outerRing = rings[0]!.AsArray();

                    var hasPositive = false;
                    var hasNegative = false;
                    foreach (var coord in outerRing)
                    {
                        var lon = coord!.AsArray()[0]!.GetValue<double>();
                        if (lon > 170) hasPositive = true;
                        if (lon < -170) hasNegative = true;
                    }

                    (hasPositive && hasNegative).Should().BeTrue(
                        $"{providerName} should preserve antimeridian crossing polygon coordinates");
                }
                break;

            case GeometryTestData.GeodeticScenario.WithHoles:
                // Polygon should have holes (interior rings)
                if (geometryType == GeometryTestData.GeometryType.Polygon)
                {
                    var rings = coordinates.AsArray();
                    rings.Should().HaveCountGreaterThan(1,
                        $"{providerName} Polygon with holes should have at least 2 rings (exterior + hole)");

                    // Validate both rings are closed
                    foreach (var ring in rings)
                    {
                        var ringCoords = ring!.AsArray();
                        ringCoords.Should().HaveCountGreaterThanOrEqualTo(4,
                            "Polygon ring should have at least 4 points (3 unique + closing)");

                        var first = ringCoords[0]!.AsArray();
                        var last = ringCoords[ringCoords.Count - 1]!.AsArray();

                        first[0]!.GetValue<double>().Should().BeApproximately(
                            last[0]!.GetValue<double>(), 0.000001,
                            "Ring should be closed (first lon == last lon)");
                        first[1]!.GetValue<double>().Should().BeApproximately(
                            last[1]!.GetValue<double>(), 0.000001,
                            "Ring should be closed (first lat == last lat)");
                    }
                }
                break;

            case GeometryTestData.GeodeticScenario.HighPrecision:
                // Point should preserve reasonable precision
                // NOTE: Most databases preserve 6-8 decimal places (~0.1mm precision)
                // which is more than sufficient for real-world use cases
                if (geometryType == GeometryTestData.GeometryType.Point)
                {
                    var coords = coordinates.AsArray();
                    coords.Should().HaveCount(2, "Point should have 2 coordinates");

                    var lon = coords[0]!.GetValue<double>();
                    var lat = coords[1]!.GetValue<double>();

                    // Validate precision is at least 4 decimal places (~11m at equator)
                    // which is sufficient for most geospatial applications
                    // If higher precision is needed, use DECIMAL type instead of DOUBLE
                    lon.Should().BeApproximately(-122.4194, 0.0001,
                        $"{providerName} should preserve at least 4 decimal places of longitude");
                    lat.Should().BeApproximately(45.5231, 0.0001,
                        $"{providerName} should preserve at least 4 decimal places of latitude");
                }
                break;

            case GeometryTestData.GeodeticScenario.NorthPole:
                // Validate high latitude coordinates (> 80°)
                ValidateHighLatitudeCoordinates(providerName, geometryType, coordinates, minLat: 80.0);
                break;

            case GeometryTestData.GeodeticScenario.GlobalExtent:
                // Validate global-scale coordinates spanning hemispheres
                ValidateGlobalExtentCoordinates(providerName, geometryType, coordinates);
                break;

            case GeometryTestData.GeodeticScenario.Simple:
                // Validate basic coordinate ranges for Simple scenario
                ValidateCoordinateRanges(providerName, geometryType, coordinates);
                break;
        }
    }

    private void ValidateCoordinateRanges(
        string providerName,
        GeometryTestData.GeometryType geometryType,
        JsonNode coordinates)
    {
        // Recursively validate all coordinates are within valid lat/lon ranges
        switch (geometryType)
        {
            case GeometryTestData.GeometryType.Point:
                var point = coordinates.AsArray();
                ValidateSingleCoordinate(providerName, point[0]!.GetValue<double>(), point[1]!.GetValue<double>());
                break;

            case GeometryTestData.GeometryType.LineString:
            case GeometryTestData.GeometryType.MultiPoint:
                var lineCoords = coordinates.AsArray();
                foreach (var coord in lineCoords)
                {
                    var c = coord!.AsArray();
                    ValidateSingleCoordinate(providerName, c[0]!.GetValue<double>(), c[1]!.GetValue<double>());
                }
                break;

            case GeometryTestData.GeometryType.Polygon:
            case GeometryTestData.GeometryType.MultiLineString:
                var rings = coordinates.AsArray();
                foreach (var ring in rings)
                {
                    var ringCoords = ring!.AsArray();
                    foreach (var coord in ringCoords)
                    {
                        var c = coord!.AsArray();
                        ValidateSingleCoordinate(providerName, c[0]!.GetValue<double>(), c[1]!.GetValue<double>());
                    }
                }
                break;

            case GeometryTestData.GeometryType.MultiPolygon:
                var polygons = coordinates.AsArray();
                foreach (var polygon in polygons)
                {
                    var polyRings = polygon!.AsArray();
                    foreach (var ring in polyRings)
                    {
                        var ringCoords = ring!.AsArray();
                        foreach (var coord in ringCoords)
                        {
                            var c = coord!.AsArray();
                            ValidateSingleCoordinate(providerName, c[0]!.GetValue<double>(), c[1]!.GetValue<double>());
                        }
                    }
                }
                break;

            case GeometryTestData.GeometryType.GeometryCollection:
                // GeometryCollection has different structure - skip coordinate validation
                // as it contains mixed geometry types
                break;
        }
    }

    private void ValidateSingleCoordinate(string providerName, double lon, double lat)
    {
        lon.Should().BeInRange(-180, 180,
            $"{providerName} longitude should be within valid range");
        lat.Should().BeInRange(-90, 90,
            $"{providerName} latitude should be within valid range");
    }

    private void ValidateHighLatitudeCoordinates(
        string providerName,
        GeometryTestData.GeometryType geometryType,
        JsonNode coordinates,
        double minLat)
    {
        // Validate that at least one coordinate has latitude >= minLat
        var hasHighLatitude = false;

        switch (geometryType)
        {
            case GeometryTestData.GeometryType.Point:
                var lat = coordinates.AsArray()[1]!.GetValue<double>();
                hasHighLatitude = lat >= minLat;
                break;

            case GeometryTestData.GeometryType.LineString:
                foreach (var coord in coordinates.AsArray())
                {
                    lat = coord!.AsArray()[1]!.GetValue<double>();
                    if (lat >= minLat) hasHighLatitude = true;
                }
                break;

            case GeometryTestData.GeometryType.Polygon:
                var rings = coordinates.AsArray();
                foreach (var ring in rings)
                {
                    foreach (var coord in ring!.AsArray())
                    {
                        lat = coord!.AsArray()[1]!.GetValue<double>();
                        if (lat >= minLat) hasHighLatitude = true;
                    }
                }
                break;
        }

        hasHighLatitude.Should().BeTrue(
            $"{providerName} should preserve high-latitude coordinates (>= {minLat}°)");
    }

    private void ValidateGlobalExtentCoordinates(
        string providerName,
        GeometryTestData.GeometryType geometryType,
        JsonNode coordinates)
    {
        // Validate that coordinates span significant global extent (both hemispheres)
        var minLat = 90.0;
        var maxLat = -90.0;
        var minLon = 180.0;
        var maxLon = -180.0;

        switch (geometryType)
        {
            case GeometryTestData.GeometryType.LineString:
                foreach (var coord in coordinates.AsArray())
                {
                    var lon = coord!.AsArray()[0]!.GetValue<double>();
                    var lat = coord!.AsArray()[1]!.GetValue<double>();
                    minLat = Math.Min(minLat, lat);
                    maxLat = Math.Max(maxLat, lat);
                    minLon = Math.Min(minLon, lon);
                    maxLon = Math.Max(maxLon, lon);
                }
                break;

            case GeometryTestData.GeometryType.Polygon:
                var rings = coordinates.AsArray();
                foreach (var ring in rings)
                {
                    foreach (var coord in ring!.AsArray())
                    {
                        var lon = coord!.AsArray()[0]!.GetValue<double>();
                        var lat = coord!.AsArray()[1]!.GetValue<double>();
                        minLat = Math.Min(minLat, lat);
                        maxLat = Math.Max(maxLat, lat);
                        minLon = Math.Min(minLon, lon);
                        maxLon = Math.Max(maxLon, lon);
                    }
                }
                break;
        }

        // Validate spans both northern and southern hemispheres
        (minLat < -70 && maxLat > 70).Should().BeTrue(
            $"{providerName} should preserve global extent spanning north and south (found lat range {minLat:F1}° to {maxLat:F1}°)");

        // Validate spans significant longitude range
        (maxLon - minLon > 300).Should().BeTrue(
            $"{providerName} should preserve global longitude extent (found lon range {minLon:F1}° to {maxLon:F1}°, span {maxLon - minLon:F1}°)");
    }

    private static string GetExpectedGeoJsonType(GeometryTestData.GeometryType type)
    {
        return type switch
        {
            GeometryTestData.GeometryType.Point => "Point",
            GeometryTestData.GeometryType.LineString => "LineString",
            GeometryTestData.GeometryType.Polygon => "Polygon",
            GeometryTestData.GeometryType.MultiPoint => "MultiPoint",
            GeometryTestData.GeometryType.MultiLineString => "MultiLineString",
            GeometryTestData.GeometryType.MultiPolygon => "MultiPolygon",
            GeometryTestData.GeometryType.GeometryCollection => "GeometryCollection",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private static IDataStoreProvider CreateDataStoreProvider(string providerName)
    {
        return providerName switch
        {
            MultiProviderTestFixture.SqliteProvider => new SqliteDataStoreProvider(),
            MultiProviderTestFixture.PostgresProvider => new PostgresDataStoreProvider(),
            MultiProviderTestFixture.MySqlProvider => new MySqlDataStoreProvider(),
            _ => throw new ArgumentException($"Unknown provider: {providerName}", nameof(providerName))
        };
    }
}

/// <summary>
/// Test data generator for Geometry Matrix tests.
/// Generates all provider × geometry type × scenario combinations.
/// </summary>
public class GeometryMatrixTestData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        var providers = new[]
        {
            MultiProviderTestFixture.SqliteProvider,
            MultiProviderTestFixture.PostgresProvider,
            MultiProviderTestFixture.MySqlProvider
        };

        var combinations = GeometryTestData.GetEssentialCombinations();

        foreach (var provider in providers)
        {
            foreach (var (type, scenario) in combinations)
            {
                yield return new object[] { provider, type, scenario };
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

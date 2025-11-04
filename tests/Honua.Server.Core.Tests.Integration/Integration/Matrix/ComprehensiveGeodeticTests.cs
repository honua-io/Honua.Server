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
/// Option 2: Comprehensive geodetic test suite covering ALL geometry type × scenario combinations.
/// This provides exhaustive coverage of geodetic edge cases across all providers.
///
/// Test Matrix: 3 providers × ~40 combinations = ~120 tests
/// Run time: ~60-90 seconds
///
/// Use this for thorough geodetic validation before major releases or when debugging
/// geodesy-related issues.
/// </summary>
[Collection("MultiProvider")]
[Trait("Category", "Integration")]
[Trait("Category", "Comprehensive")]
[Trait("Category", "Geodetic")]
public sealed class ComprehensiveGeodeticTests
{
    private readonly MultiProviderTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ComprehensiveGeodeticTests(MultiProviderTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Theory]
    [ClassData(typeof(ComprehensiveGeodeticTestData))]
    public async Task Provider_HandlesAllGeodeticScenarios_Correctly(
        string providerName,
        GeometryTestData.GeometryType geometryType,
        GeometryTestData.GeodeticScenario scenario)
    {
        // Arrange
        _output.WriteLine($"Comprehensive Test: {providerName} - {geometryType} - {scenario}");

        var (dataSource, service, layer) = _fixture.GetMetadata(providerName, geometryType, scenario);
        var provider = CreateDataStoreProvider(providerName);

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

        // Assert - Geometry structure is valid
        var geomJson = firstResult.Attributes["geom"] as JsonObject;
        geomJson.Should().NotBeNull($"{providerName} should return geometry as JsonObject");

        var actualType = geomJson!["type"]!.GetValue<string>();
        var expectedType = GetExpectedGeoJsonType(geometryType);
        actualType.Should().Be(expectedType,
            $"{providerName} should preserve {geometryType} type correctly");

        // Validate coordinates or geometries structure
        if (geometryType == GeometryTestData.GeometryType.GeometryCollection)
        {
            geomJson.Should().ContainKey("geometries", "GeometryCollection must have geometries array");
        }
        else
        {
            geomJson.Should().ContainKey("coordinates", "GeoJSON geometry must have coordinates");
        }

        _output.WriteLine($"✅ {providerName} correctly handled {geometryType} ({scenario})");
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
/// Test data generator for comprehensive geodetic tests.
/// Generates all provider × geometry type × scenario combinations (excluding incompatible pairs).
/// </summary>
public class ComprehensiveGeodeticTestData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        var providers = new[]
        {
            MultiProviderTestFixture.SqliteProvider,
            MultiProviderTestFixture.PostgresProvider,
            MultiProviderTestFixture.MySqlProvider
        };

        var combinations = GeometryTestData.GetAllTestCombinations();

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

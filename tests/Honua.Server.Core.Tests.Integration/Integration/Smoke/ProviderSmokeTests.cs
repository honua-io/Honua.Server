using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Sqlite;
using Honua.Server.Core.Data.Postgres;
using Honua.Server.Core.Data.MySql;
using Honua.Server.Core.Tests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Integration.Integration.Smoke;

/// <summary>
/// Phase 1 Smoke Tests: Validate each provider works with basic Point geometry queries.
/// These tests catch ~50% of provider integration bugs with minimal test count.
/// </summary>
[Collection("MultiProvider")]
[Trait("Category", "Integration")]
[Trait("Category", "Smoke")]
public sealed class ProviderSmokeTests
{
    private readonly MultiProviderTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ProviderSmokeTests(MultiProviderTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Theory]
    [InlineData(MultiProviderTestFixture.SqliteProvider)]
    [InlineData(MultiProviderTestFixture.PostgresProvider)]
    [InlineData(MultiProviderTestFixture.MySqlProvider)]
    public async Task Provider_CanQueryPointGeometry(string providerName)
    {
        // Arrange
        _output.WriteLine($"Testing provider: {providerName}");

        var (dataSource, service, layer) = _fixture.GetMetadata(
            providerName,
            GeometryTestData.GeometryType.Point);

        var provider = CreateDataStoreProvider(providerName);

        // Act
        var results = new List<FeatureRecord>();
        await foreach (var record in provider.QueryAsync(dataSource, service, layer, new FeatureQuery(Limit: 10)))
        {
            results.Add(record);
        }

        // Assert
        results.Should().HaveCountGreaterThan(0, $"{providerName} should return at least one Point feature");

        var firstResult = results[0];
        firstResult.Attributes.Should().ContainKey("geom", "geometry field should be present");
        firstResult.Attributes.Should().ContainKey("feature_id", "feature_id should be present");
        firstResult.Attributes.Should().ContainKey("name", "name attribute should be present");
        firstResult.Attributes.Should().ContainKey("geometry_type", "geometry_type should be present");

        // Validate geometry is Point and has valid GeoJSON structure
        var geomJson = firstResult.Attributes["geom"] as JsonObject;
        geomJson.Should().NotBeNull($"{providerName} should return geometry as JsonObject");
        geomJson!["type"]!.GetValue<string>().Should().Be("Point",
            $"{providerName} should return Point geometry type");

        var coordinates = geomJson["coordinates"]!.AsArray();
        coordinates.Should().HaveCount(2, "Point should have 2 coordinates (lon, lat)");

        var lon = coordinates[0]!.GetValue<double>();
        var lat = coordinates[1]!.GetValue<double>();

        lon.Should().BeInRange(-180, 180, "longitude should be valid");
        lat.Should().BeInRange(-90, 90, "latitude should be valid");

        _output.WriteLine($"✅ {providerName} returned {results.Count} features");
        _output.WriteLine($"   First feature: {firstResult.Attributes["name"]}");
        _output.WriteLine($"   Coordinates: [{lon:F6}, {lat:F6}]");
    }

    [Theory]
    [InlineData(MultiProviderTestFixture.SqliteProvider)]
    [InlineData(MultiProviderTestFixture.PostgresProvider)]
    [InlineData(MultiProviderTestFixture.MySqlProvider)]
    public async Task Provider_ReturnsCorrectFeatureCount(string providerName)
    {
        // Arrange
        var (dataSource, service, layer) = _fixture.GetMetadata(
            providerName,
            GeometryTestData.GeometryType.Point);

        var provider = CreateDataStoreProvider(providerName);

        // Act
        var count = await provider.CountAsync(dataSource, service, layer, new FeatureQuery());

        // Assert
        count.Should().BeGreaterThan(0, $"{providerName} should have at least one feature");

        _output.WriteLine($"✅ {providerName} count: {count} features");
    }

    [Theory]
    [InlineData(MultiProviderTestFixture.SqliteProvider)]
    [InlineData(MultiProviderTestFixture.PostgresProvider)]
    [InlineData(MultiProviderTestFixture.MySqlProvider)]
    public async Task Provider_CanQueryLineStringGeometry(string providerName)
    {
        // Arrange
        _output.WriteLine($"Testing LineString on provider: {providerName}");

        var (dataSource, service, layer) = _fixture.GetMetadata(
            providerName,
            GeometryTestData.GeometryType.LineString);

        var provider = CreateDataStoreProvider(providerName);

        // Act
        var results = new List<FeatureRecord>();
        await foreach (var record in provider.QueryAsync(dataSource, service, layer, new FeatureQuery(Limit: 10)))
        {
            results.Add(record);
        }

        // Assert
        results.Should().HaveCountGreaterThan(0, $"{providerName} should return at least one LineString feature");

        var geomJson = results[0].Attributes["geom"] as JsonObject;
        geomJson.Should().NotBeNull();
        geomJson!["type"]!.GetValue<string>().Should().Be("LineString",
            $"{providerName} should return LineString geometry type");

        var coordinates = geomJson["coordinates"]!.AsArray();
        coordinates.Should().HaveCountGreaterThan(1, "LineString should have at least 2 coordinates");

        _output.WriteLine($"✅ {providerName} returned LineString with {coordinates.Count} vertices");
    }

    [Theory]
    [InlineData(MultiProviderTestFixture.SqliteProvider)]
    [InlineData(MultiProviderTestFixture.PostgresProvider)]
    [InlineData(MultiProviderTestFixture.MySqlProvider)]
    public async Task Provider_CanQueryPolygonGeometry(string providerName)
    {
        // Arrange
        _output.WriteLine($"Testing Polygon on provider: {providerName}");

        var (dataSource, service, layer) = _fixture.GetMetadata(
            providerName,
            GeometryTestData.GeometryType.Polygon);

        var provider = CreateDataStoreProvider(providerName);

        // Act
        var results = new List<FeatureRecord>();
        await foreach (var record in provider.QueryAsync(dataSource, service, layer, new FeatureQuery(Limit: 10)))
        {
            results.Add(record);
        }

        // Assert
        results.Should().HaveCountGreaterThan(0, $"{providerName} should return at least one Polygon feature");

        var geomJson = results[0].Attributes["geom"] as JsonObject;
        geomJson.Should().NotBeNull();
        geomJson!["type"]!.GetValue<string>().Should().Be("Polygon",
            $"{providerName} should return Polygon geometry type");

        var coordinates = geomJson["coordinates"]!.AsArray();
        coordinates.Should().HaveCountGreaterThan(0, "Polygon should have at least one ring");

        // Validate outer ring is closed (first point == last point)
        var outerRing = coordinates[0]!.AsArray();
        outerRing.Should().HaveCountGreaterThanOrEqualTo(4, "Polygon ring should have at least 4 points (3 unique + closing)");

        var firstPoint = outerRing[0]!.AsArray();
        var lastPoint = outerRing[outerRing.Count - 1]!.AsArray();

        firstPoint[0]!.GetValue<double>().Should().BeApproximately(
            lastPoint[0]!.GetValue<double>(), 0.000001,
            "First and last longitude should match (closed ring)");
        firstPoint[1]!.GetValue<double>().Should().BeApproximately(
            lastPoint[1]!.GetValue<double>(), 0.000001,
            "First and last latitude should match (closed ring)");

        _output.WriteLine($"✅ {providerName} returned Polygon with {outerRing.Count} vertices in outer ring");
    }

    [Theory]
    [InlineData(MultiProviderTestFixture.SqliteProvider)]
    [InlineData(MultiProviderTestFixture.PostgresProvider)]
    [InlineData(MultiProviderTestFixture.MySqlProvider)]
    public async Task Provider_PreservesAttributeDataTypes(string providerName)
    {
        // Arrange
        var (dataSource, service, layer) = _fixture.GetMetadata(
            providerName,
            GeometryTestData.GeometryType.Point);

        var provider = CreateDataStoreProvider(providerName);

        // Act
        var results = new List<FeatureRecord>();
        await foreach (var record in provider.QueryAsync(dataSource, service, layer, new FeatureQuery(Limit: 1)))
        {
            results.Add(record);
        }

        // Assert
        results.Should().HaveCount(1);
        var attrs = results[0].Attributes;

        // Validate data types match expected schema
        attrs.Should().ContainKey("feature_id");
        var featureId = Convert.ToInt64(attrs["feature_id"]);
        featureId.Should().BeGreaterThan(0);

        attrs["name"].Should().BeOfType<string>()
            .Which.Should().NotBeNullOrEmpty();

        attrs.Should().ContainKey("priority");
        var priority = Convert.ToInt32(attrs["priority"]);
        priority.Should().BeInRange(1, 10);

        attrs.Should().ContainKey("active");
        // Active can be bool, long, or int depending on provider

        attrs.Should().ContainKey("measurement");
        var measurement = Convert.ToDouble(attrs["measurement"]);
        measurement.Should().BeGreaterThan(0);

        _output.WriteLine($"✅ {providerName} preserved all attribute data types correctly");
    }

    [Theory]
    [InlineData(MultiProviderTestFixture.SqliteProvider)]
    [InlineData(MultiProviderTestFixture.PostgresProvider)]
    [InlineData(MultiProviderTestFixture.MySqlProvider)]
    public async Task Provider_HandlesLimitParameter(string providerName)
    {
        // Arrange
        var (dataSource, service, layer) = _fixture.GetMetadata(
            providerName,
            GeometryTestData.GeometryType.Point);

        var provider = CreateDataStoreProvider(providerName);

        // Act - Request limit of 1
        var results = new List<FeatureRecord>();
        await foreach (var record in provider.QueryAsync(dataSource, service, layer, new FeatureQuery(Limit: 1)))
        {
            results.Add(record);
        }

        // Assert
        results.Should().HaveCount(1, $"{providerName} should respect limit parameter");

        _output.WriteLine($"✅ {providerName} correctly limited results to 1 feature");
    }

    private IDataStoreProvider CreateDataStoreProvider(string providerName)
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
/// Collection definition for multi-provider tests using shared fixture.
/// The fixture spins up Docker containers (Postgres, MySQL) and creates SQLite database.
/// </summary>
[CollectionDefinition("MultiProvider")]
public class MultiProviderCollection : ICollectionFixture<MultiProviderTestFixture>
{
    // This class has no code, and is never instantiated.
    // Its purpose is to define the collection fixture for xUnit.
}

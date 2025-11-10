// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Deployment.E2ETests.Infrastructure;
using Npgsql;
using Xunit;

namespace Honua.Server.Deployment.E2ETests;

/// <summary>
/// End-to-end tests for 3D features in OGC API - Features.
/// Tests elevation and building height support.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "3D")]
public class OgcApi3DFeaturesTests : IClassFixture<DeploymentTestFactory>
{
    private readonly DeploymentTestFactory _factory;

    public OgcApi3DFeaturesTests(DeploymentTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Features_WithInclude3D_ReturnsZCoordinates()
    {
        // Arrange
        await SetupTest3DTableAsync();

        var metadata = new TestMetadataBuilder()
            .WithCatalog("3d-test", "3D Features Test", "Test 3D feature output")
            .AddPostgresDataSource("db", _factory.PostgresConnectionString)
            .AddFeatureService("geo-svc", "Geo Service", "db", layers =>
            {
                layers.Add(new
                {
                    id = "buildings_3d",
                    title = "3D Buildings",
                    type = "table",
                    schema = "public",
                    table = "test_buildings_3d",
                    idField = "id",
                    geometryField = "geom",
                    extensions = new
                    {
                        elevation = new
                        {
                            source = "attribute",
                            elevationAttribute = "base_elevation",
                            heightAttribute = "building_height",
                            includeHeight = true
                        }
                    }
                });
            })
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act - Request features WITH 3D
        var response = await client.GetAsync("/ogc/geo-svc/collections/buildings_3d/items?include3D=true&limit=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

        payload!.RootElement.GetProperty("type").GetString().Should().Be("FeatureCollection");
        var features = payload.RootElement.GetProperty("features").EnumerateArray().ToList();
        features.Should().NotBeEmpty();

        // Verify first feature has 3D coordinates
        var firstFeature = features[0];
        var geometry = firstFeature.GetProperty("geometry");
        var coordinates = geometry.GetProperty("coordinates").EnumerateArray().ToList();

        // Point should have 3 coordinates [lon, lat, z]
        coordinates.Should().HaveCount(3);

        // Z coordinate should be the elevation value
        var zCoord = coordinates[2].GetDouble();
        zCoord.Should().BeGreaterThan(0); // Base elevation should be > 0

        // Verify height property is included
        var properties = firstFeature.GetProperty("properties");
        properties.TryGetProperty("height", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Features_WithoutInclude3D_Returns2DCoordinates()
    {
        // Arrange
        await SetupTest3DTableAsync();

        var metadata = new TestMetadataBuilder()
            .WithCatalog("2d-test", "2D Features Test", "Test 2D feature output")
            .AddPostgresDataSource("db", _factory.PostgresConnectionString)
            .AddFeatureService("geo-svc", "Geo Service", "db")
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act - Request features WITHOUT 3D (default behavior)
        var response = await client.GetAsync("/ogc/geo-svc/collections/test_buildings_3d/items?limit=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var features = payload!.RootElement.GetProperty("features").EnumerateArray().ToList();
        features.Should().NotBeEmpty();

        // Verify first feature has 2D coordinates
        var firstFeature = features[0];
        var geometry = firstFeature.GetProperty("geometry");
        var coordinates = geometry.GetProperty("coordinates").EnumerateArray().ToList();

        // Point should have only 2 coordinates [lon, lat]
        coordinates.Should().HaveCount(2);
    }

    [Fact]
    public async Task Features_3DPolygon_AllVerticesHaveZCoordinates()
    {
        // Arrange
        await SetupTest3DPolygonTableAsync();

        var metadata = new TestMetadataBuilder()
            .WithCatalog("polygon-3d-test", "3D Polygon Test", "Test 3D polygon features")
            .AddPostgresDataSource("db", _factory.PostgresConnectionString)
            .AddFeatureService("geo-svc", "Geo Service", "db", layers =>
            {
                layers.Add(new
                {
                    id = "parcels_3d",
                    title = "3D Parcels",
                    type = "table",
                    schema = "public",
                    table = "test_parcels_3d",
                    idField = "id",
                    geometryField = "geom",
                    extensions = new
                    {
                        elevation = new
                        {
                            source = "attribute",
                            elevationAttribute = "ground_elevation",
                            defaultElevation = 0.0
                        }
                    }
                });
            })
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/geo-svc/collections/parcels_3d/items?include3D=true&limit=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var features = payload!.RootElement.GetProperty("features").EnumerateArray().ToList();
        features.Should().NotBeEmpty();

        var firstFeature = features[0];
        var geometry = firstFeature.GetProperty("geometry");
        geometry.GetProperty("type").GetString().Should().Be("Polygon");

        var rings = geometry.GetProperty("coordinates").EnumerateArray().ToList();
        rings.Should().NotBeEmpty();

        // Check first ring (exterior)
        var exteriorRing = rings[0].EnumerateArray().ToList();
        exteriorRing.Should().HaveCountGreaterThan(2);

        // All vertices should have Z coordinate
        foreach (var vertex in exteriorRing)
        {
            var coords = vertex.EnumerateArray().ToList();
            coords.Should().HaveCount(3); // [lon, lat, z]
        }
    }

    [Fact]
    public async Task Features_3DWithDefaultElevation_UsesConfiguredDefault()
    {
        // Arrange
        await SetupTestTableWithoutElevationAsync();

        var metadata = new TestMetadataBuilder()
            .WithCatalog("default-elev-test", "Default Elevation Test", "Test default elevation")
            .AddPostgresDataSource("db", _factory.PostgresConnectionString)
            .AddFeatureService("geo-svc", "Geo Service", "db", layers =>
            {
                layers.Add(new
                {
                    id = "points_default",
                    title = "Points with Default Elevation",
                    type = "table",
                    schema = "public",
                    table = "test_points_no_elev",
                    idField = "id",
                    geometryField = "geom",
                    extensions = new
                    {
                        elevation = new
                        {
                            defaultElevation = 100.0 // Set default to 100m
                        }
                    }
                });
            })
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/geo-svc/collections/points_default/items?include3D=true&limit=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var features = payload!.RootElement.GetProperty("features").EnumerateArray().ToList();
        features.Should().NotBeEmpty();

        // All features should have Z coordinate = 100.0
        foreach (var feature in features)
        {
            var geometry = feature.GetProperty("geometry");
            var coordinates = geometry.GetProperty("coordinates").EnumerateArray().ToList();
            coordinates[2].GetDouble().Should().Be(100.0);
        }
    }

    private async Task SetupTest3DTableAsync()
    {
        await using var connection = new NpgsqlConnection(_factory.PostgresConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE EXTENSION IF NOT EXISTS postgis;

            DROP TABLE IF EXISTS test_buildings_3d;
            CREATE TABLE test_buildings_3d (
                id SERIAL PRIMARY KEY,
                name TEXT,
                base_elevation DOUBLE PRECISION,
                building_height DOUBLE PRECISION,
                geom GEOMETRY(Point, 4326)
            );

            INSERT INTO test_buildings_3d (name, base_elevation, building_height, geom)
            VALUES
                ('Ferry Building', 5.0, 75.0, ST_SetSRID(ST_MakePoint(-122.3933, 37.7955), 4326)),
                ('Transamerica Pyramid', 260.0, 85.0, ST_SetSRID(ST_MakePoint(-122.4029, 37.7952), 4326)),
                ('Salesforce Tower', 240.0, 326.0, ST_SetSRID(ST_MakePoint(-122.3968, 37.7897), 4326)),
                ('Coit Tower', 85.0, 64.0, ST_SetSRID(ST_MakePoint(-122.4058, 37.8024), 4326)),
                ('Palace of Fine Arts', 10.0, 40.0, ST_SetSRID(ST_MakePoint(-122.4484, 37.8030), 4326));
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SetupTest3DPolygonTableAsync()
    {
        await using var connection = new NpgsqlConnection(_factory.PostgresConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE EXTENSION IF NOT EXISTS postgis;

            DROP TABLE IF EXISTS test_parcels_3d;
            CREATE TABLE test_parcels_3d (
                id SERIAL PRIMARY KEY,
                parcel_id TEXT,
                ground_elevation DOUBLE PRECISION,
                geom GEOMETRY(Polygon, 4326)
            );

            INSERT INTO test_parcels_3d (parcel_id, ground_elevation, geom)
            VALUES (
                'PARCEL-001',
                50.0,
                ST_SetSRID(ST_MakePolygon(
                    ST_MakeLine(ARRAY[
                        ST_MakePoint(-122.42, 37.78),
                        ST_MakePoint(-122.40, 37.78),
                        ST_MakePoint(-122.40, 37.80),
                        ST_MakePoint(-122.42, 37.80),
                        ST_MakePoint(-122.42, 37.78)
                    ])
                ), 4326)
            );
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SetupTestTableWithoutElevationAsync()
    {
        await using var connection = new NpgsqlConnection(_factory.PostgresConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE EXTENSION IF NOT EXISTS postgis;

            DROP TABLE IF EXISTS test_points_no_elev;
            CREATE TABLE test_points_no_elev (
                id SERIAL PRIMARY KEY,
                name TEXT,
                geom GEOMETRY(Point, 4326)
            );

            INSERT INTO test_points_no_elev (name, geom)
            VALUES
                ('Point 1', ST_SetSRID(ST_MakePoint(-122.4, 37.8), 4326)),
                ('Point 2', ST_SetSRID(ST_MakePoint(-122.41, 37.81), 4326)),
                ('Point 3', ST_SetSRID(ST_MakePoint(-122.42, 37.82), 4326));
        ";
        await cmd.ExecuteNonQueryAsync();
    }
}

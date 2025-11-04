// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using Honua.Server.Core.Data.Postgres;
using Honua.Server.Core.Metadata;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Honua.Server.Integration.Tests.Data;

/// <summary>
/// Integration tests for PostgreSQL optimization functions.
/// These tests require a real PostgreSQL/PostGIS database using Docker.
/// </summary>
[Trait("Category", "PostgresOptimizations")]
[Trait("Category", "Integration")]
public class PostgresOptimizationsIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private NpgsqlConnection? _connection;
    private PostgresFunctionRepository? _repository;
    private PostgresConnectionManager? _connectionManager;
    private string _connectionString = string.Empty;
    private const string TestSchema = "test_optimizations";

    public async Task InitializeAsync()
    {
        // Start PostgreSQL container with PostGIS
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgis/postgis:16-3.4-alpine")
            .WithDatabase("honua_test")
            .WithUsername("postgres")
            .WithPassword("test")
            .WithCleanUp(true)
            .Build();

        await _postgresContainer.StartAsync();

        _connectionString = _postgresContainer.GetConnectionString();
        _connection = new NpgsqlConnection(_connectionString);
        await _connection.OpenAsync();

        // Run migrations to create optimized functions
        await RunMigrationAsync("014_PostgresOptimizations.sql");

        // Generate test data
        await RunTestDataScriptAsync();

        // Setup connection manager and repository
        var dataSources = new System.Collections.Generic.Dictionary<string, NpgsqlDataSource>();
        var metrics = new PostgresConnectionPoolMetrics(dataSources);
        var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        _connectionManager = new PostgresConnectionManager(metrics, memoryCache);
        _repository = new PostgresFunctionRepository(_connectionManager);
    }

    public async Task DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }

        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    private async Task RunMigrationAsync(string migrationFileName)
    {
        var migrationPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..",
            "src", "Honua.Server.Core", "Data", "Migrations",
            migrationFileName);

        if (!File.Exists(migrationPath))
        {
            // Try alternative path
            migrationPath = Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "..",
                "src", "Honua.Server.Core", "Data", "Migrations",
                migrationFileName);
        }

        var sql = await File.ReadAllTextAsync(migrationPath);
        await _connection!.ExecuteAsync(sql);
    }

    private async Task RunTestDataScriptAsync()
    {
        var scriptPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Data",
            "TestData_PostgresOptimizations.sql");

        if (!File.Exists(scriptPath))
        {
            // Try alternative path
            scriptPath = Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "TestData_PostgresOptimizations.sql");
        }

        var sql = await File.ReadAllTextAsync(scriptPath);
        await _connection!.ExecuteAsync(sql);
    }

    private DataSourceDefinition CreateTestDataSource()
    {
        return new DataSourceDefinition
        {
            Id = "test-postgres",
            Provider = "postgres",
            ConnectionString = _connectionString
        };
    }

    #region Function Existence Tests

    [Fact]
    public async Task AllOptimizationFunctions_ShouldExist()
    {
        // Arrange
        var expectedFunctions = new[]
        {
            "honua_get_features_optimized",
            "honua_get_mvt_tile",
            "honua_aggregate_features",
            "honua_spatial_query",
            "honua_cluster_points",
            "honua_fast_count",
            "honua_validate_and_repair_geometries"
        };

        // Act & Assert
        foreach (var functionName in expectedFunctions)
        {
            var exists = await FunctionExistsAsync(functionName);
            exists.Should().BeTrue($"Function {functionName} should exist in database");
        }
    }

    [Fact]
    public async Task AllOptimizationFunctions_ShouldBeParallelSafe()
    {
        // Arrange
        var parallelSafeFunctions = new[]
        {
            "honua_get_features_optimized",
            "honua_get_mvt_tile",
            "honua_aggregate_features",
            "honua_spatial_query",
            "honua_cluster_points",
            "honua_fast_count"
        };

        // Act & Assert
        foreach (var functionName in parallelSafeFunctions)
        {
            var isParallelSafe = await IsParallelSafeAsync(functionName);
            isParallelSafe.Should().BeTrue($"Function {functionName} should be PARALLEL SAFE");
        }
    }

    #endregion

    #region Feature Retrieval Tests

    [Fact]
    public async Task GetFeaturesOptimized_WithBbox_ShouldReturnFeatures()
    {
        // Arrange
        var dataSource = CreateTestDataSource();
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var bbox = factory.ToGeometry(new Envelope(-180, 180, -90, 90));

        // Act
        var features = new List<JsonDocument>();
        await foreach (var feature in _repository!.GetFeaturesOptimizedAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom",
            bbox,
            zoom: 10,
            limit: 100))
        {
            features.Add(feature);
        }

        // Assert
        features.Should().NotBeEmpty();
        features.Should().HaveCountLessThanOrEqualTo(100);
        features.Should().AllSatisfy(f =>
        {
            f.RootElement.GetProperty("type").GetString().Should().Be("Feature");
            f.RootElement.GetProperty("geometry").ValueKind.Should().Be(JsonValueKind.Object);
            f.RootElement.GetProperty("properties").ValueKind.Should().Be(JsonValueKind.Object);
        });
    }

    [Fact]
    public async Task GetFeaturesOptimized_WithSmallBbox_ShouldReturnFilteredFeatures()
    {
        // Arrange
        var dataSource = CreateTestDataSource();
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var bbox = factory.ToGeometry(new Envelope(0, 10, 0, 10));

        // Act
        var features = new List<JsonDocument>();
        await foreach (var feature in _repository!.GetFeaturesOptimizedAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom",
            bbox,
            zoom: 10,
            limit: 1000))
        {
            features.Add(feature);
        }

        // Get actual count from database for comparison
        var actualCount = await _connection!.ExecuteScalarAsync<int>($@"
            SELECT COUNT(*)
            FROM {TestSchema}.test_cities
            WHERE geom && ST_MakeEnvelope(0, 0, 10, 10, 4326)
              AND ST_Intersects(geom, ST_MakeEnvelope(0, 0, 10, 10, 4326))
        ");

        // Assert
        features.Count.Should().Be(actualCount);
    }

    [Theory]
    [InlineData(5)]   // Low zoom = heavy simplification
    [InlineData(10)]  // Medium zoom = light simplification
    [InlineData(15)]  // High zoom = minimal simplification
    public async Task GetFeaturesOptimized_WithDifferentZoomLevels_ShouldSimplifyGeometries(int zoom)
    {
        // Arrange
        var dataSource = CreateTestDataSource();
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var bbox = factory.ToGeometry(new Envelope(-180, 180, -90, 90));

        // Act
        var features = new List<JsonDocument>();
        await foreach (var feature in _repository!.GetFeaturesOptimizedAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom",
            bbox,
            zoom: zoom,
            limit: 10))
        {
            features.Add(feature);
        }

        // Assert
        features.Should().NotBeEmpty();
        // At low zoom levels, point geometries won't be simplified,
        // but polygons would be. This is a smoke test to ensure the function works.
    }

    #endregion

    #region MVT Tile Tests

    [Fact]
    public async Task GetMvtTile_WithValidCoordinates_ShouldGenerateTile()
    {
        // Arrange
        var dataSource = CreateTestDataSource();

        // Act
        var tileData = await _repository!.GetMvtTileAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom",
            z: 5,
            x: 16,
            y: 10);

        // Assert
        tileData.Should().NotBeNull();
        tileData.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetMvtTile_EmptyTile_ShouldReturnEmptyOrNullData()
    {
        // Arrange
        var dataSource = CreateTestDataSource();

        // Act
        var tileData = await _repository!.GetMvtTileAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom",
            z: 20, // Very high zoom in remote location
            x: 1000000,
            y: 1000000);

        // Assert - May be null or empty MVT
        if (tileData != null)
        {
            tileData.Should().BeEmpty("or contain empty MVT structure");
        }
    }

    [Theory]
    [InlineData(2048, 128)]
    [InlineData(4096, 256)]
    [InlineData(8192, 512)]
    public async Task GetMvtTile_WithCustomExtentAndBuffer_ShouldGenerateTile(int extent, int buffer)
    {
        // Arrange
        var dataSource = CreateTestDataSource();

        // Act
        var tileData = await _repository!.GetMvtTileAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom",
            z: 5,
            x: 16,
            y: 10,
            extent: extent,
            buffer: buffer);

        // Assert
        tileData.Should().NotBeNull();
    }

    #endregion

    #region Aggregation Tests

    [Fact]
    public async Task AggregateFeatures_WithoutBbox_ShouldReturnStatistics()
    {
        // Arrange
        var dataSource = CreateTestDataSource();

        // Act
        var result = await _repository!.AggregateFeaturesAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom");

        // Assert
        result.TotalCount.Should().BeGreaterThan(0);
        result.ExtentGeoJson.Should().NotBeNull();
    }

    [Fact]
    public async Task AggregateFeatures_WithBbox_ShouldReturnFilteredStatistics()
    {
        // Arrange
        var dataSource = CreateTestDataSource();
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var bbox = factory.ToGeometry(new Envelope(0, 50, 0, 50));

        // Act
        var result = await _repository!.AggregateFeaturesAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom",
            bbox: bbox);

        // Assert
        result.TotalCount.Should().BeGreaterThan(0);

        // Compare with direct query
        var directCount = await _connection!.ExecuteScalarAsync<long>($@"
            SELECT COUNT(*)
            FROM {TestSchema}.test_cities
            WHERE geom && ST_MakeEnvelope(0, 0, 50, 50, 4326)
              AND ST_Intersects(geom, ST_MakeEnvelope(0, 0, 50, 50, 4326))
        ");

        result.TotalCount.Should().Be(directCount);
    }

    [Fact]
    public async Task AggregateFeatures_WithGroupBy_ShouldReturnGroupedStatistics()
    {
        // Arrange
        var dataSource = CreateTestDataSource();

        // Act
        var result = await _repository!.AggregateFeaturesAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom",
            groupByColumn: "country");

        // Assert
        result.Groups.Should().NotBeEmpty();
        result.Groups.Should().AllSatisfy(g =>
        {
            g.GroupKey.Should().NotBeNullOrEmpty();
            g.Count.Should().BeGreaterThan(0);
        });
    }

    #endregion

    #region Spatial Query Tests

    [Theory]
    [InlineData("intersects")]
    [InlineData("contains")]
    [InlineData("within")]
    public async Task SpatialQuery_WithDifferentOperations_ShouldReturnResults(string operation)
    {
        // Arrange
        var dataSource = CreateTestDataSource();
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var queryGeometry = factory.ToGeometry(new Envelope(0, 10, 0, 10));

        // Act
        var results = new List<SpatialQueryResult>();
        await foreach (var result in _repository!.SpatialQueryAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom",
            queryGeometry,
            operation,
            limit: 100))
        {
            results.Add(result);
        }

        // Assert
        // Results may be empty depending on data, but query should execute successfully
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task SpatialQuery_WithDistanceOperation_ShouldReturnFeaturesWithDistance()
    {
        // Arrange
        var dataSource = CreateTestDataSource();
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var point = factory.CreatePoint(new Coordinate(0, 0));

        // Act
        var results = new List<SpatialQueryResult>();
        await foreach (var result in _repository!.SpatialQueryAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom",
            point,
            "distance",
            distance: 1000000, // 1000 km
            limit: 100))
        {
            results.Add(result);
        }

        // Assert
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(r =>
        {
            r.DistanceMeters.Should().NotBeNull();
            r.DistanceMeters!.Value.Should().BeLessThanOrEqualTo(1000000);
        });

        // Results should be ordered by distance
        for (int i = 0; i < results.Count - 1; i++)
        {
            results[i].DistanceMeters!.Value.Should().BeLessThanOrEqualTo(results[i + 1].DistanceMeters!.Value);
        }
    }

    [Fact]
    public async Task SpatialQuery_WithInvalidOperation_ShouldThrowException()
    {
        // Arrange
        var dataSource = CreateTestDataSource();
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var point = factory.CreatePoint(new Coordinate(0, 0));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await foreach (var _ in _repository!.SpatialQueryAsync(
                dataSource,
                $"{TestSchema}.test_cities",
                "geom",
                point,
                "invalid_operation"))
            {
                // Should not reach here
            }
        });
    }

    #endregion

    #region Fast Count Tests

    [Fact]
    public async Task FastCount_WithoutBbox_ShouldReturnTotalCount()
    {
        // Arrange
        var dataSource = CreateTestDataSource();

        // Act
        var count = await _repository!.FastCountAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom");

        // Assert
        count.Should().BeGreaterThan(0);

        // Verify against direct query
        var directCount = await _connection!.ExecuteScalarAsync<long>(
            $"SELECT COUNT(*) FROM {TestSchema}.test_cities");

        count.Should().Be(directCount);
    }

    [Fact]
    public async Task FastCount_WithBbox_ShouldReturnFilteredCount()
    {
        // Arrange
        var dataSource = CreateTestDataSource();
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var bbox = factory.ToGeometry(new Envelope(0, 50, 0, 50));

        // Act
        var count = await _repository!.FastCountAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom",
            bbox: bbox);

        // Assert
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FastCount_WithEstimate_ShouldReturnApproximateCount()
    {
        // Arrange
        var dataSource = CreateTestDataSource();

        // Act
        var estimatedCount = await _repository!.FastCountAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom",
            useEstimate: true);

        var exactCount = await _repository.FastCountAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom",
            useEstimate: false);

        // Assert
        estimatedCount.Should().BeGreaterThan(0);

        // Estimated count should be within reasonable range of exact count
        var difference = Math.Abs(estimatedCount - exactCount) / (double)exactCount;
        difference.Should().BeLessThan(0.2, "Estimate should be within 20% of actual count");
    }

    #endregion

    #region Clustering Tests

    [Fact]
    public async Task ClusterPoints_ShouldCreateClusters()
    {
        // Arrange
        var dataSource = CreateTestDataSource();
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var bbox = factory.ToGeometry(new Envelope(-180, 180, -90, 90));

        // Act
        var clusters = new List<ClusterResult>();
        await foreach (var cluster in _repository!.ClusterPointsAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom",
            bbox,
            clusterDistance: 500000)) // 500km
        {
            clusters.Add(cluster);
        }

        // Assert
        clusters.Should().NotBeEmpty();
        clusters.Should().AllSatisfy(c =>
        {
            c.ClusterId.Should().BeGreaterThanOrEqualTo(0);
            c.PointCount.Should().BeGreaterThan(0);
            c.CentroidGeoJson.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task ClusterPoints_WithSmallDistance_ShouldCreateMoreClusters()
    {
        // Arrange
        var dataSource = CreateTestDataSource();
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var bbox = factory.ToGeometry(new Envelope(0, 50, 0, 50));

        // Act - Small cluster distance
        var smallDistanceClusters = new List<ClusterResult>();
        await foreach (var cluster in _repository!.ClusterPointsAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom",
            bbox,
            clusterDistance: 100000)) // 100km
        {
            smallDistanceClusters.Add(cluster);
        }

        // Act - Large cluster distance
        var largeDistanceClusters = new List<ClusterResult>();
        await foreach (var cluster in _repository.ClusterPointsAsync(
            dataSource,
            $"{TestSchema}.test_cities",
            "geom",
            bbox,
            clusterDistance: 1000000)) // 1000km
        {
            largeDistanceClusters.Add(cluster);
        }

        // Assert
        // Smaller distance should generally create more (or equal) clusters
        smallDistanceClusters.Count.Should().BeGreaterThanOrEqualTo(largeDistanceClusters.Count);
    }

    #endregion

    #region Geometry Validation Tests

    [Fact]
    public async Task ValidateAndRepair_ShouldFixInvalidGeometries()
    {
        // Arrange
        var sql = $@"
            SELECT feature_id, was_invalid, error_message, repaired
            FROM honua_validate_and_repair_geometries(
                '{TestSchema}.test_invalid_geoms',
                'geom',
                'id'
            )
        ";

        // Act
        var results = (await _connection!.QueryAsync<dynamic>(sql)).ToList();

        // Assert
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(r =>
        {
            ((bool)r.was_invalid).Should().BeTrue();
            ((bool)r.repaired).Should().BeTrue();
        });

        // Verify geometries are now valid
        var invalidCount = await _connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM {TestSchema}.test_invalid_geoms WHERE NOT ST_IsValid(geom)");

        invalidCount.Should().Be(0, "All geometries should be valid after repair");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task GetFeaturesOptimized_EmptyTable_ShouldReturnEmpty()
    {
        // Arrange
        var dataSource = CreateTestDataSource();
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var bbox = factory.ToGeometry(new Envelope(-180, 180, -90, 90));

        // Act
        var features = new List<JsonDocument>();
        await foreach (var feature in _repository!.GetFeaturesOptimizedAsync(
            dataSource,
            $"{TestSchema}.test_empty_table",
            "geom",
            bbox))
        {
            features.Add(feature);
        }

        // Assert
        features.Should().BeEmpty();
    }

    [Fact]
    public async Task FastCount_EmptyTable_ShouldReturnZero()
    {
        // Arrange
        var dataSource = CreateTestDataSource();

        // Act
        var count = await _repository!.FastCountAsync(
            dataSource,
            $"{TestSchema}.test_empty_table",
            "geom");

        // Assert
        count.Should().Be(0);
    }

    #endregion

    #region Helper Methods

    private async Task<bool> FunctionExistsAsync(string functionName)
    {
        var sql = @"
            SELECT EXISTS (
                SELECT 1
                FROM pg_proc
                WHERE proname = @functionName
            )
        ";

        return await _connection!.ExecuteScalarAsync<bool>(sql, new { functionName });
    }

    private async Task<bool> IsParallelSafeAsync(string functionName)
    {
        var sql = @"
            SELECT proparallel = 's'
            FROM pg_proc
            WHERE proname = @functionName
        ";

        return await _connection!.ExecuteScalarAsync<bool>(sql, new { functionName });
    }

    #endregion
}

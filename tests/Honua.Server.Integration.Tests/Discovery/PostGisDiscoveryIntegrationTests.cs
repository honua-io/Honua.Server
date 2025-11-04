// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Honua.Server.Core.Data.Validation;
using Honua.Server.Core.Discovery;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Honua.Server.Integration.Tests.Discovery;

/// <summary>
/// Integration tests for PostGIS table discovery using a real PostgreSQL database.
/// Uses Testcontainers to spin up a PostGIS database for testing.
/// </summary>
public sealed class PostGisDiscoveryIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;
    private const string DataSourceId = "test-postgis";

    public async Task InitializeAsync()
    {
        // Start PostgreSQL container with PostGIS extension
        _container = new PostgreSqlBuilder()
            .WithImage("postgis/postgis:16-3.4")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .WithPortBinding(5432, true)
            .Build();

        await _container.StartAsync();

        _connectionString = _container.GetConnectionString();

        // Enable PostGIS extension
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS postgis;", connection);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task DiscoverTables_FindsAllGeometryTables()
    {
        // Arrange
        await CreateTestTablesAsync(new[]
        {
            ("public", "cities", "Point", 4326),
            ("public", "roads", "LineString", 4326),
            ("public", "parcels", "Polygon", 4326)
        });

        var service = CreateDiscoveryService();

        // Act
        var tables = await service.DiscoverTablesAsync(DataSourceId);

        // Assert
        var tableList = tables.ToList();
        Assert.Equal(3, tableList.Count);
        Assert.Contains(tableList, t => t.TableName == "cities" && t.GeometryType == "POINT");
        Assert.Contains(tableList, t => t.TableName == "roads" && t.GeometryType == "LINESTRING");
        Assert.Contains(tableList, t => t.TableName == "parcels" && t.GeometryType == "POLYGON");
    }

    [Fact]
    public async Task DiscoverTables_RespectsSchemaExclusions()
    {
        // Arrange
        await CreateTestTablesAsync(new[]
        {
            ("public", "public_table", "Point", 4326),
            ("topology", "topo_table", "Point", 4326)
        });

        // Create topology schema
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand("CREATE SCHEMA IF NOT EXISTS topology;", connection);
        await cmd.ExecuteNonQueryAsync();

        var options = new AutoDiscoveryOptions
        {
            ExcludeSchemas = new[] { "topology" }
        };

        var service = CreateDiscoveryService(options);

        // Act
        var tables = await service.DiscoverTablesAsync(DataSourceId);

        // Assert
        var tableList = tables.ToList();
        Assert.All(tableList, t => Assert.NotEqual("topology", t.Schema));
        Assert.Contains(tableList, t => t.TableName == "public_table");
    }

    [Fact]
    public async Task DiscoverTables_RespectsTablePatternExclusions()
    {
        // Arrange
        await CreateTestTablesAsync(new[]
        {
            ("public", "users", "Point", 4326),
            ("public", "temp_data", "Point", 4326),
            ("public", "_internal", "Point", 4326)
        });

        var options = new AutoDiscoveryOptions
        {
            ExcludeTablePatterns = new[] { "temp_*", "_*" }
        };

        var service = CreateDiscoveryService(options);

        // Act
        var tables = await service.DiscoverTablesAsync(DataSourceId);

        // Assert
        var tableList = tables.ToList();
        var single = Assert.Single(tableList);
        Assert.Equal("users", single.TableName);
    }

    [Fact]
    public async Task DiscoverTables_DetectsSpatialIndexes()
    {
        // Arrange
        await CreateTableWithSpatialIndexAsync("indexed_table");
        await CreateTableWithoutSpatialIndexAsync("unindexed_table");

        var service = CreateDiscoveryService();

        // Act
        var tables = await service.DiscoverTablesAsync(DataSourceId);

        // Assert
        var tableList = tables.ToList();
        Assert.Equal(2, tableList.Count);
        Assert.True(tableList.First(t => t.TableName == "indexed_table").HasSpatialIndex);
        Assert.False(tableList.First(t => t.TableName == "unindexed_table").HasSpatialIndex);
    }

    [Fact]
    public async Task DiscoverTables_RequiringSpatialIndex_FiltersCorrectly()
    {
        // Arrange
        await CreateTableWithSpatialIndexAsync("indexed");
        await CreateTableWithoutSpatialIndexAsync("unindexed");

        var options = new AutoDiscoveryOptions
        {
            RequireSpatialIndex = true
        };

        var service = CreateDiscoveryService(options);

        // Act
        var tables = await service.DiscoverTablesAsync(DataSourceId);

        // Assert
        var single = Assert.Single(tables);
        Assert.Equal("indexed", single.TableName);
    }

    [Fact]
    public async Task DiscoverTables_IncludesColumnMetadata()
    {
        // Arrange
        await CreateTableWithColumnsAsync();

        var service = CreateDiscoveryService();

        // Act
        var tables = await service.DiscoverTablesAsync(DataSourceId);

        // Assert
        var table = Assert.Single(tables);
        Assert.Equal(4, table.Columns.Count); // id, name, description, created_at (geom excluded)

        Assert.True(table.Columns.ContainsKey("id"));
        Assert.True(table.Columns["id"].IsPrimaryKey);

        Assert.True(table.Columns.ContainsKey("name"));
        Assert.False(table.Columns["name"].IsNullable);

        Assert.True(table.Columns.ContainsKey("description"));
        Assert.True(table.Columns["description"].IsNullable);
    }

    [Fact]
    public async Task DiscoverTables_ComputesExtent()
    {
        // Arrange
        await CreateTableWithDataAsync();

        var options = new AutoDiscoveryOptions
        {
            ComputeExtentOnDiscovery = true
        };

        var service = CreateDiscoveryService(options);

        // Act
        var tables = await service.DiscoverTablesAsync(DataSourceId);

        // Assert
        var table = Assert.Single(tables);
        Assert.NotNull(table.Extent);

        // Verify extent contains the points we inserted
        Assert.InRange(table.Extent.MinX, -123, -74);
        Assert.InRange(table.Extent.MaxX, -74, -73);
        Assert.InRange(table.Extent.MinY, 37, 42);
        Assert.InRange(table.Extent.MaxY, 41, 42);
    }

    [Fact]
    public async Task DiscoverTables_RespectsMaxTableLimit()
    {
        // Arrange - Create 15 tables
        var tablesToCreate = Enumerable.Range(0, 15)
            .Select(i => ("public", $"table_{i:D2}", "Point", 4326))
            .ToArray();

        await CreateTestTablesAsync(tablesToCreate);

        var options = new AutoDiscoveryOptions
        {
            MaxTables = 10
        };

        var service = CreateDiscoveryService(options);

        // Act
        var tables = await service.DiscoverTablesAsync(DataSourceId);

        // Assert
        Assert.Equal(10, tables.Count());
    }

    [Fact]
    public async Task DiscoverTable_FindsSpecificTable()
    {
        // Arrange
        await CreateTestTablesAsync(new[]
        {
            ("public", "cities", "Point", 4326)
        });

        var service = CreateDiscoveryService();

        // Act
        var table = await service.DiscoverTableAsync(DataSourceId, "public.cities");

        // Assert
        Assert.NotNull(table);
        Assert.Equal("public", table.Schema);
        Assert.Equal("cities", table.TableName);
        Assert.Equal("POINT", table.GeometryType);
        Assert.Equal(4326, table.SRID);
    }

    [Fact]
    public async Task DiscoverTable_WithoutSchema_UsesPublicSchema()
    {
        // Arrange
        await CreateTestTablesAsync(new[]
        {
            ("public", "cities", "Point", 4326)
        });

        var service = CreateDiscoveryService();

        // Act
        var table = await service.DiscoverTableAsync(DataSourceId, "cities");

        // Assert
        Assert.NotNull(table);
        Assert.Equal("public", table.Schema);
        Assert.Equal("cities", table.TableName);
    }

    [Fact]
    public async Task DiscoverTable_NonExistent_ReturnsNull()
    {
        // Arrange
        var service = CreateDiscoveryService();

        // Act
        var table = await service.DiscoverTableAsync(DataSourceId, "public.nonexistent");

        // Assert
        Assert.Null(table);
    }

    [Fact]
    public async Task DiscoverTables_SkipsTablesWithoutPrimaryKey()
    {
        // Arrange
        await CreateTableWithoutPrimaryKeyAsync("no_pk_table");

        var service = CreateDiscoveryService();

        // Act
        var tables = await service.DiscoverTablesAsync(DataSourceId);

        // Assert
        Assert.Empty(tables);
    }

    [Fact]
    public async Task DiscoverTables_IncludesEstimatedRowCount()
    {
        // Arrange
        await CreateTableWithDataAsync();

        var service = CreateDiscoveryService();

        // Act
        var tables = await service.DiscoverTablesAsync(DataSourceId);

        // Assert
        var table = Assert.Single(tables);
        Assert.True(table.EstimatedRowCount >= 0);
    }

    [Fact]
    public async Task DiscoverTables_IncludesTableDescription()
    {
        // Arrange
        await CreateTableWithDescriptionAsync();

        var service = CreateDiscoveryService();

        // Act
        var tables = await service.DiscoverTablesAsync(DataSourceId);

        // Assert
        var table = Assert.Single(tables);
        Assert.Equal("Test table for cities", table.Description);
    }

    private PostGisTableDiscoveryService CreateDiscoveryService(AutoDiscoveryOptions? options = null)
    {
        options ??= new AutoDiscoveryOptions();

        var dataSource = new DataSourceDefinition
        {
            Id = DataSourceId,
            Provider = "postgis",
            ConnectionString = _connectionString!
        };

        var metadataRegistry = new TestMetadataRegistry(dataSource);
        var schemaDiscovery = new PostgresSchemaDiscoveryService(NullLogger<PostgresSchemaDiscoveryService>.Instance);

        return new PostGisTableDiscoveryService(
            metadataRegistry,
            schemaDiscovery,
            Options.Create(options),
            NullLogger<PostGisTableDiscoveryService>.Instance);
    }

    private async Task CreateTestTablesAsync((string Schema, string Table, string GeomType, int SRID)[] tables)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var (schema, table, geomType, srid) in tables)
        {
            var sql = $@"
                CREATE TABLE IF NOT EXISTS {schema}.""{table}"" (
                    id SERIAL PRIMARY KEY,
                    name TEXT,
                    geom geometry({geomType}, {srid})
                );";

            await using var cmd = new NpgsqlCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task CreateTableWithSpatialIndexAsync(string tableName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = $@"
            CREATE TABLE IF NOT EXISTS public.""{tableName}"" (
                id SERIAL PRIMARY KEY,
                name TEXT,
                geom geometry(Point, 4326)
            );

            CREATE INDEX idx_{tableName}_geom ON public.""{tableName}"" USING GIST (geom);";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task CreateTableWithoutSpatialIndexAsync(string tableName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = $@"
            CREATE TABLE IF NOT EXISTS public.""{tableName}"" (
                id SERIAL PRIMARY KEY,
                name TEXT,
                geom geometry(Point, 4326)
            );";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task CreateTableWithColumnsAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            CREATE TABLE IF NOT EXISTS public.test_columns (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                name TEXT NOT NULL,
                description TEXT,
                created_at TIMESTAMP NOT NULL DEFAULT NOW(),
                geom geometry(Point, 4326)
            );";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task CreateTableWithDataAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            CREATE TABLE IF NOT EXISTS public.cities (
                id SERIAL PRIMARY KEY,
                name TEXT,
                geom geometry(Point, 4326)
            );

            INSERT INTO public.cities (name, geom) VALUES
                ('San Francisco', ST_SetSRID(ST_MakePoint(-122.4, 37.8), 4326)),
                ('New York', ST_SetSRID(ST_MakePoint(-74.0, 40.7), 4326)),
                ('Chicago', ST_SetSRID(ST_MakePoint(-87.6, 41.9), 4326));";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task CreateTableWithoutPrimaryKeyAsync(string tableName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = $@"
            CREATE TABLE IF NOT EXISTS public.""{tableName}"" (
                id INTEGER,
                name TEXT,
                geom geometry(Point, 4326)
            );";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task CreateTableWithDescriptionAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            CREATE TABLE IF NOT EXISTS public.cities_with_desc (
                id SERIAL PRIMARY KEY,
                name TEXT,
                geom geometry(Point, 4326)
            );

            COMMENT ON TABLE public.cities_with_desc IS 'Test table for cities';";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private sealed class TestMetadataRegistry : IMetadataRegistry
    {
        private readonly MetadataSnapshot _snapshot;

        public TestMetadataRegistry(DataSourceDefinition dataSource)
        {
            _snapshot = new MetadataSnapshot(
                new CatalogDefinition { Id = "test" },
                Array.Empty<FolderDefinition>(),
                new[] { dataSource },
                Array.Empty<ServiceDefinition>(),
                Array.Empty<LayerDefinition>());
        }

        public MetadataSnapshot Snapshot => _snapshot;
        public bool IsInitialized => true;

        public bool TryGetSnapshot(out MetadataSnapshot snapshot)
        {
            snapshot = _snapshot;
            return true;
        }

        public ValueTask<MetadataSnapshot> GetSnapshotAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(_snapshot);
        }

        public Task EnsureInitializedAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ReloadAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Update(MetadataSnapshot snapshot)
        {
        }

        public Task UpdateAsync(MetadataSnapshot snapshot, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Microsoft.Extensions.Primitives.IChangeToken GetChangeToken()
        {
            return new Microsoft.Extensions.Primitives.CancellationChangeToken(System.Threading.CancellationToken.None);
        }
    }
}

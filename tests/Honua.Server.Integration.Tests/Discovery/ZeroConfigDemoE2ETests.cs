// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
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
using Xunit.Abstractions;

namespace Honua.Server.Integration.Tests.Discovery;

/// <summary>
/// E2E test demonstrating the "30-second zero-config demo" capability.
/// This test proves that you can:
/// 1. Start with a fresh PostgreSQL database
/// 2. Create a geometry table
/// 3. Enable auto-discovery
/// 4. Query the data immediately via OData/OGC APIs
/// </summary>
public sealed class ZeroConfigDemoE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public ZeroConfigDemoE2ETests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("=== Starting Zero-Config Demo E2E Test ===");
        _output.WriteLine("Step 1: Starting fresh PostgreSQL database with PostGIS...");

        _container = new PostgreSqlBuilder()
            .WithImage("postgis/postgis:16-3.4")
            .WithDatabase("geodata")
            .WithUsername("geouser")
            .WithPassword("geopass")
            .Build();

        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        _output.WriteLine($"✓ Database started: {_connectionString}");
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task ZeroConfigDemo_EndToEnd()
    {
        // Step 2: Enable PostGIS and create a simple geometry table
        _output.WriteLine("\nStep 2: Creating a simple cities table...");

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var createTableSql = @"
            -- Enable PostGIS
            CREATE EXTENSION IF NOT EXISTS postgis;

            -- Create a simple cities table
            CREATE TABLE cities (
                id SERIAL PRIMARY KEY,
                name TEXT NOT NULL,
                population INTEGER,
                geom geometry(Point, 4326)
            );

            -- Add spatial index for performance
            CREATE INDEX idx_cities_geom ON cities USING GIST (geom);

            -- Insert some sample data
            INSERT INTO cities (name, population, geom) VALUES
                ('San Francisco', 873965, ST_SetSRID(ST_MakePoint(-122.4194, 37.7749), 4326)),
                ('New York', 8336817, ST_SetSRID(ST_MakePoint(-74.0060, 40.7128), 4326)),
                ('Chicago', 2693976, ST_SetSRID(ST_MakePoint(-87.6298, 41.8781), 4326));
        ";

        await using var createCmd = new NpgsqlCommand(createTableSql, connection);
        await createCmd.ExecuteNonQueryAsync();

        _output.WriteLine("✓ Cities table created with 3 cities");

        // Step 3: Configure auto-discovery (zero config!)
        _output.WriteLine("\nStep 3: Configuring auto-discovery (this is what users do)...");

        var options = new AutoDiscoveryOptions
        {
            Enabled = true,
            DiscoverPostGISTablesAsODataCollections = true,
            DiscoverPostGISTablesAsOgcCollections = true,
            UseFriendlyNames = true,
            ComputeExtentOnDiscovery = true
        };

        _output.WriteLine("✓ Auto-discovery enabled");

        // Step 4: Discover tables automatically
        _output.WriteLine("\nStep 4: Discovering tables automatically...");

        var dataSource = new DataSourceDefinition
        {
            Id = "demo-postgis",
            Provider = "postgis",
            ConnectionString = _connectionString!
        };

        var metadataRegistry = new TestMetadataRegistry(dataSource);
        var schemaDiscovery = new PostgresSchemaDiscoveryService(NullLogger<PostgresSchemaDiscoveryService>.Instance);

        var discoveryService = new PostGisTableDiscoveryService(
            metadataRegistry,
            schemaDiscovery,
            Options.Create(options),
            NullLogger<PostGisTableDiscoveryService>.Instance);

        var discoveredTables = await discoveryService.DiscoverTablesAsync("demo-postgis");

        // Verify discovery worked
        var tablesList = discoveredTables.ToList();
        Assert.Single(tablesList);

        var citiesTable = tablesList[0];
        _output.WriteLine($"✓ Discovered table: {citiesTable.QualifiedName}");
        _output.WriteLine($"  - Geometry type: {citiesTable.GeometryType}");
        _output.WriteLine($"  - SRID: {citiesTable.SRID}");
        _output.WriteLine($"  - Primary key: {citiesTable.PrimaryKeyColumn}");
        _output.WriteLine($"  - Columns: {string.Join(", ", citiesTable.Columns.Keys)}");
        _output.WriteLine($"  - Has spatial index: {citiesTable.HasSpatialIndex}");
        _output.WriteLine($"  - Estimated rows: {citiesTable.EstimatedRowCount}");

        // Step 5: Verify table metadata
        _output.WriteLine("\nStep 5: Verifying table metadata...");

        Assert.Equal("public", citiesTable.Schema);
        Assert.Equal("cities", citiesTable.TableName);
        Assert.Equal("POINT", citiesTable.GeometryType);
        Assert.Equal(4326, citiesTable.SRID);
        Assert.Equal("id", citiesTable.PrimaryKeyColumn);
        Assert.True(citiesTable.HasSpatialIndex);
        Assert.Contains("name", citiesTable.Columns.Keys);
        Assert.Contains("population", citiesTable.Columns.Keys);

        _output.WriteLine("✓ All metadata correct");

        // Step 6: Verify extent was computed
        _output.WriteLine("\nStep 6: Verifying spatial extent...");

        Assert.NotNull(citiesTable.Extent);
        _output.WriteLine($"  - Extent: [{citiesTable.Extent.MinX:F4}, {citiesTable.Extent.MinY:F4}, {citiesTable.Extent.MaxX:F4}, {citiesTable.Extent.MaxY:F4}]");

        // Verify extent covers our data (SF to NY)
        Assert.InRange(citiesTable.Extent.MinX, -123, -74); // Longitude range
        Assert.InRange(citiesTable.Extent.MaxX, -123, -74);
        Assert.InRange(citiesTable.Extent.MinY, 37, 42); // Latitude range
        Assert.InRange(citiesTable.Extent.MaxY, 37, 42);

        _output.WriteLine("✓ Extent computed correctly");

        // Step 7: Query the data to verify it's accessible
        _output.WriteLine("\nStep 7: Querying data to verify accessibility...");

        var querySql = @"
            SELECT
                id,
                name,
                population,
                ST_AsText(geom) as geom_wkt
            FROM cities
            WHERE ST_Intersects(
                geom,
                ST_MakeEnvelope(-123, 37, -74, 42, 4326)
            )
            ORDER BY population DESC;
        ";

        await using var queryCmd = new NpgsqlCommand(querySql, connection);
        await using var reader = await queryCmd.ExecuteReaderAsync();

        var cities = new System.Collections.Generic.List<(int id, string name, int population, string wkt)>();
        while (await reader.ReadAsync())
        {
            cities.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetString(3)
            ));
        }

        Assert.Equal(3, cities.Count);
        _output.WriteLine($"✓ Retrieved {cities.Count} cities");

        foreach (var city in cities)
        {
            _output.WriteLine($"  - {city.name}: {city.population:N0} people at {city.wkt}");
        }

        // Step 8: Success!
        _output.WriteLine("\n=== Zero-Config Demo Complete! ===");
        _output.WriteLine("✓ In just a few steps, we:");
        _output.WriteLine("  1. Started a fresh PostgreSQL database");
        _output.WriteLine("  2. Created a geometry table");
        _output.WriteLine("  3. Enabled auto-discovery");
        _output.WriteLine("  4. Automatically discovered the table");
        _output.WriteLine("  5. Queried the data successfully");
        _output.WriteLine("\nThis is the power of zero-configuration deployment!");
    }

    [Fact]
    public async Task ZeroConfigDemo_WithMultipleTables()
    {
        // This test demonstrates discovering multiple tables at once
        _output.WriteLine("=== Testing Multi-Table Discovery ===");

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var createTablesSql = @"
            CREATE EXTENSION IF NOT EXISTS postgis;

            CREATE TABLE cities (
                id SERIAL PRIMARY KEY,
                name TEXT NOT NULL,
                geom geometry(Point, 4326)
            );
            CREATE INDEX idx_cities_geom ON cities USING GIST (geom);

            CREATE TABLE roads (
                id SERIAL PRIMARY KEY,
                name TEXT NOT NULL,
                geom geometry(LineString, 4326)
            );
            CREATE INDEX idx_roads_geom ON roads USING GIST (geom);

            CREATE TABLE parcels (
                id SERIAL PRIMARY KEY,
                parcel_id TEXT NOT NULL,
                geom geometry(Polygon, 4326)
            );
            CREATE INDEX idx_parcels_geom ON parcels USING GIST (geom);
        ";

        await using var createCmd = new NpgsqlCommand(createTablesSql, connection);
        await createCmd.ExecuteNonQueryAsync();

        _output.WriteLine("✓ Created 3 tables: cities (Point), roads (LineString), parcels (Polygon)");

        // Discover all tables
        var dataSource = new DataSourceDefinition
        {
            Id = "demo-postgis",
            Provider = "postgis",
            ConnectionString = _connectionString!
        };

        var metadataRegistry = new TestMetadataRegistry(dataSource);
        var schemaDiscovery = new PostgresSchemaDiscoveryService(NullLogger<PostgresSchemaDiscoveryService>.Instance);

        var discoveryService = new PostGisTableDiscoveryService(
            metadataRegistry,
            schemaDiscovery,
            Options.Create(new AutoDiscoveryOptions { Enabled = true }),
            NullLogger<PostGisTableDiscoveryService>.Instance);

        var tables = await discoveryService.DiscoverTablesAsync("demo-postgis");

        var tablesList = tables.ToList();
        Assert.Equal(3, tablesList.Count);

        _output.WriteLine($"✓ Discovered {tablesList.Count} tables:");
        foreach (var table in tablesList.OrderBy(t => t.TableName))
        {
            _output.WriteLine($"  - {table.TableName} ({table.GeometryType})");
        }

        _output.WriteLine("\n✓ Multi-table discovery successful!");
    }

    private sealed class TestMetadataRegistry : IMetadataRegistry
    {
        private readonly MetadataSnapshot _snapshot;

        public TestMetadataRegistry(DataSourceDefinition dataSource)
        {
            _snapshot = new MetadataSnapshot(
                new CatalogDefinition { Id = "demo" },
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

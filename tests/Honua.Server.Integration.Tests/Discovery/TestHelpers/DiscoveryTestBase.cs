// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Honua.Server.Core.Discovery;
using Honua.Server.Core.Metadata;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Honua.Server.Integration.Tests.Discovery.TestHelpers;

/// <summary>
/// Base class for discovery integration tests that provides common setup and utilities.
/// </summary>
public abstract class DiscoveryTestBase : IAsyncLifetime
{
    protected PostgreSqlContainer? Container { get; private set; }
    protected string? ConnectionString { get; private set; }
    protected const string DefaultDataSourceId = "test-postgis";

    public virtual async Task InitializeAsync()
    {
        // Start PostgreSQL container with PostGIS
        Container = new PostgreSqlBuilder()
            .WithImage("postgis/postgis:16-3.4")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();

        await Container.StartAsync();
        ConnectionString = Container.GetConnectionString();

        // Enable PostGIS extension
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS postgis;", connection);
        await cmd.ExecuteNonQueryAsync();
    }

    public virtual async Task DisposeAsync()
    {
        if (Container != null)
        {
            await Container.DisposeAsync();
        }
    }

    /// <summary>
    /// Executes SQL script against the test database.
    /// </summary>
    protected async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Creates a test table with the specified geometry type.
    /// </summary>
    protected async Task CreateTestTableAsync(string schema, string tableName, string geometryType, int srid = 4326, bool withSpatialIndex = true, bool withPrimaryKey = true)
    {
        var pkClause = withPrimaryKey ? "PRIMARY KEY" : "";
        var indexSql = withSpatialIndex ? $"CREATE INDEX idx_{tableName}_geom ON {schema}.\"{tableName}\" USING GIST (geom);" : "";

        var sql = $@"
            CREATE TABLE IF NOT EXISTS {schema}.""{tableName}"" (
                id SERIAL {pkClause},
                name TEXT,
                geom geometry({geometryType}, {srid})
            );
            {indexSql}";

        await ExecuteSqlAsync(sql);
    }

    /// <summary>
    /// Creates a test metadata registry with a PostGIS data source.
    /// </summary>
    protected IMetadataRegistry CreateTestMetadataRegistry(string? dataSourceId = null)
    {
        dataSourceId ??= DefaultDataSourceId;

        var dataSource = new DataSourceDefinition
        {
            Id = dataSourceId,
            Provider = "postgis",
            ConnectionString = ConnectionString!
        };

        return new TestMetadataRegistry(dataSource);
    }

    /// <summary>
    /// Creates a test discovered table object.
    /// </summary>
    protected DiscoveredTable CreateTestDiscoveredTable(
        string schema = "public",
        string tableName = "test_table",
        string geometryType = "Point",
        int srid = 4326,
        bool hasSpatialIndex = true,
        Envelope? extent = null)
    {
        return new DiscoveredTable
        {
            Schema = schema,
            TableName = tableName,
            GeometryColumn = "geom",
            SRID = srid,
            GeometryType = geometryType,
            PrimaryKeyColumn = "id",
            Columns = new Dictionary<string, ColumnInfo>
            {
                ["id"] = new ColumnInfo
                {
                    Name = "id",
                    DataType = "int32",
                    StorageType = "integer",
                    IsPrimaryKey = true,
                    IsNullable = false
                },
                ["name"] = new ColumnInfo
                {
                    Name = "name",
                    DataType = "string",
                    StorageType = "text",
                    IsPrimaryKey = false,
                    IsNullable = true
                }
            },
            HasSpatialIndex = hasSpatialIndex,
            EstimatedRowCount = 0,
            Extent = extent
        };
    }

    /// <summary>
    /// Inserts test point data into a table.
    /// </summary>
    protected async Task InsertTestPointsAsync(string schema, string tableName, params (string name, double lon, double lat)[] points)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        foreach (var (name, lon, lat) in points)
        {
            var sql = $@"
                INSERT INTO {schema}.""{tableName}"" (name, geom)
                VALUES (@name, ST_SetSRID(ST_MakePoint(@lon, @lat), 4326));";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("name", name);
            cmd.Parameters.AddWithValue("lon", lon);
            cmd.Parameters.AddWithValue("lat", lat);
            await cmd.ExecuteNonQueryAsync();
        }
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

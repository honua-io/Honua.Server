using System;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Postgres;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Tests.Shared;
using Npgsql;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data.Postgres;

/// <summary>
/// PostgreSQL data store provider integration tests.
/// Uses transaction-based isolation via SharedPostgresFixture for fast, independent tests.
/// </summary>
[Collection("SharedPostgres")]
[Trait("Category", "Integration")]
[Trait("Feature", "Data")]
[Trait("Database", "Postgres")]
[Trait("Speed", "Slow")]
public sealed class PostgresDataStoreProviderTests : DataStoreProviderTestsBase<PostgresDataStoreProviderTests.TestFixture>, IAsyncLifetime
{
    private readonly SharedPostgresFixture _sharedFixture;
    private readonly TestFixture _testFixture;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public PostgresDataStoreProviderTests(SharedPostgresFixture sharedFixture)
    {
        _sharedFixture = sharedFixture;
        _testFixture = new TestFixture();
    }

    public async Task InitializeAsync()
    {
        if (!_sharedFixture.IsAvailable)
        {
            throw new SkipException("PostgreSQL test container is not available (Docker required).");
        }
        (_connection, _transaction) = await _sharedFixture.CreateTransactionScopeAsync();
        await PrepareDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
        }
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }

    protected override IDataStoreProvider CreateProvider() => new PostgresDataStoreProvider();
    protected override string ProviderName => "PostgreSQL";
    protected override (DataSourceDefinition, ServiceDefinition, LayerDefinition) GetMetadata() => _testFixture.CreateMetadata(_sharedFixture.ConnectionString);

    private async Task PrepareDatabaseAsync()
    {
        var commandText = @"
drop table if exists public.roads;
create table public.roads (
    road_id integer primary key,
    name text null,
    status text null,
    observed_at timestamptz null,
    geom geometry(Point, 4326) null
);

insert into public.roads (road_id, name, status, observed_at, geom)
values
(1001, 'Highway 26', 'open', now(), ST_SetSRID(ST_Point(-122.5, 45.5), 4326)),
(1002, 'Highway 101', 'closed', now(), ST_SetSRID(ST_Point(-122.1, 45.7), 4326));
";

        await using var command = new NpgsqlCommand(commandText, _connection, _transaction)
        {
            CommandTimeout = 120
        };

        await command.ExecuteNonQueryAsync();
    }

    public sealed class TestFixture
    {
        public (DataSourceDefinition DataSource, ServiceDefinition Service, LayerDefinition Layer) CreateMetadata(string connectionString)
        {
            var dataSource = new DataSourceDefinition
            {
                Id = "postgis-primary",
                Provider = "postgis",
                ConnectionString = connectionString
            };

            var service = new ServiceDefinition
            {
                Id = "roads",
                Title = "Road Centerlines",
                FolderId = "transportation",
                ServiceType = "feature",
                DataSourceId = dataSource.Id,
                Enabled = true,
                Description = "Transportation datasets",
                Ogc = new OgcServiceDefinition
                {
                    DefaultCrs = "EPSG:4326",
                    ItemLimit = 1000
                },
                Layers = Array.Empty<LayerDefinition>()
            };

            var layer = new LayerDefinition
            {
                Id = "roads-primary",
                ServiceId = service.Id,
                Title = "Primary Roads",
                GeometryType = "Point",
                IdField = "road_id",
                DisplayField = "name",
                GeometryField = "geom",
                Storage = new LayerStorageDefinition
                {
                    Table = "public.roads",
                    GeometryColumn = "geom",
                    PrimaryKey = "road_id",
                    TemporalColumn = "observed_at",
                    Srid = 4326
                },
                Fields = new[]
                {
                    new FieldDefinition { Name = "road_id", DataType = "int", StorageType = "integer", Nullable = false },
                    new FieldDefinition { Name = "name", DataType = "string", StorageType = "text" },
                    new FieldDefinition { Name = "status", DataType = "string", StorageType = "text" },
                    new FieldDefinition { Name = "observed_at", DataType = "datetimeoffset", StorageType = "timestamptz" },
                    new FieldDefinition { Name = "geom", DataType = "geometry", StorageType = "geometry" }
                }
            };

            return (dataSource, service, layer);
        }
    }
}

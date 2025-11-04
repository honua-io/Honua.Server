using System;
using System.IO;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Sqlite;
using Honua.Server.Core.Metadata;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data.Sqlite;

/// <summary>
/// SQLite data store provider integration tests.
/// Uses SpatiaLite extension for spatial operations.
/// </summary>
[Collection("DatabaseTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "Data")]
[Trait("Database", "SQLite")]
[Trait("Speed", "Slow")]
public class SqliteDataStoreProviderTests : DataStoreProviderTestsBase<SqliteDataStoreProviderTests.SqliteFixture>, IDisposable
{
    private readonly SqliteFixture _fixture;
    private readonly string _databasePath;

    public SqliteDataStoreProviderTests()
    {
        _fixture = new SqliteFixture();
        _databasePath = Path.Combine(Path.GetTempPath(), $"honua-sqlite-provider-{Guid.NewGuid():N}.db");

        if (!_fixture.ShouldSkip)
        {
            SeedDatabase(_databasePath);
        }
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    protected override IDataStoreProvider CreateProvider() => new SqliteDataStoreProvider();
    protected override string ProviderName => "SQLite";
    protected override (DataSourceDefinition, ServiceDefinition, LayerDefinition) GetMetadata() => _fixture.CreateMetadata(_databasePath);
    protected override bool ShouldSkip => _fixture.ShouldSkip;
    protected override string? SkipReason => _fixture.SkipReason;

    private static void SeedDatabase(string databasePath)
    {
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }

        var connectionString = SpatialiteTestHelper.BuildConnectionString(databasePath);
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        SpatialiteTestHelper.ConfigureConnection(connection);

        using (var createCommand = connection.CreateCommand())
        {
            createCommand.CommandText = """
CREATE TABLE "roads_primary" (
    "road_id" INTEGER PRIMARY KEY,
    "name" TEXT,
    "status" TEXT,
    "observed_at" TEXT
);
""";
            createCommand.ExecuteNonQuery();
        }

        using (var addGeometry = connection.CreateCommand())
        {
            addGeometry.CommandText = "SELECT AddGeometryColumn('roads_primary', 'geom', 3857, 'POINT', 2);";
            addGeometry.ExecuteNonQuery();
        }

        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = """
INSERT INTO "roads_primary" (road_id, name, status, observed_at, geom)
VALUES (@id, @name, @status, @observed_at, ST_Transform(GeomFromText(@wkt4326, 4326), 3857));
""";
        insertCommand.Parameters.Add(new SqliteParameter("@id", 1001));
        insertCommand.Parameters.Add(new SqliteParameter("@name", "Sunset Highway"));
        insertCommand.Parameters.Add(new SqliteParameter("@status", "open"));
        insertCommand.Parameters.Add(new SqliteParameter("@observed_at", DateTime.UtcNow.ToString("O")));
        insertCommand.Parameters.Add(new SqliteParameter("@wkt4326", "POINT(-122.5 45.5)"));
        insertCommand.ExecuteNonQuery();

        insertCommand.Parameters["@id"].Value = 1002;
        insertCommand.Parameters["@name"].Value = "Pacific Avenue";
        insertCommand.Parameters["@status"].Value = "planned";
        insertCommand.Parameters["@observed_at"].Value = DateTime.UtcNow.ToString("O");
        insertCommand.Parameters["@wkt4326"].Value = "POINT(-122.1 45.7)";
        insertCommand.ExecuteNonQuery();
    }

    public sealed class SqliteFixture
    {
        private string? _skipReason;

        public SqliteFixture()
        {
            try
            {
                if (!SpatialiteTestHelper.EnsureAvailable(out _skipReason))
                {
                    ShouldSkip = true;
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or SqliteException)
            {
                _skipReason = ex.Message;
                ShouldSkip = true;
            }
        }

        public bool ShouldSkip { get; }
        public string? SkipReason => _skipReason ?? "SpatiaLite extension is unavailable.";

        public (DataSourceDefinition DataSource, ServiceDefinition Service, LayerDefinition Layer) CreateMetadata(string databasePath)
        {
            var connectionString = SpatialiteTestHelper.BuildConnectionString(databasePath);

            var dataSource = new DataSourceDefinition
            {
                Id = "sqlite-primary",
                Provider = "sqlite",
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
                    DefaultCrs = "EPSG:4326"
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
                    Table = "roads_primary",
                    GeometryColumn = "geom",
                    PrimaryKey = "road_id",
                    TemporalColumn = "observed_at",
                    Srid = 3857
                },
                Fields = new[]
                {
                    new FieldDefinition { Name = "road_id", DataType = "int", StorageType = "integer", Nullable = false },
                    new FieldDefinition { Name = "name", DataType = "string", StorageType = "text" },
                    new FieldDefinition { Name = "status", DataType = "string", StorageType = "text" },
                    new FieldDefinition { Name = "observed_at", DataType = "datetime", StorageType = "text" },
                    new FieldDefinition { Name = "geom", DataType = "geometry", StorageType = "geometry" }
                }
            };

            return (dataSource, service, layer);
        }
    }
}



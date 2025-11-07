using System;
using System.IO;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.DuckDB;
using Honua.Server.Core.Metadata;
using DuckDB.NET.Data;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data.DuckDB;

/// <summary>
/// DuckDB data store provider integration tests.
/// Uses DuckDB spatial extension for spatial operations.
/// </summary>
[Collection("DatabaseTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "Data")]
[Trait("Database", "DuckDB")]
[Trait("Speed", "Slow")]
public class DuckDBDataStoreProviderTests : DataStoreProviderTestsBase<DuckDBDataStoreProviderTests.DuckDBFixture>, IDisposable
{
    private readonly DuckDBFixture _fixture;
    private readonly string _databasePath;

    public DuckDBDataStoreProviderTests()
    {
        _fixture = new DuckDBFixture();
        _databasePath = Path.Combine(Path.GetTempPath(), $"honua-duckdb-provider-{Guid.NewGuid():N}.duckdb");

        if (!_fixture.ShouldSkip)
        {
            SeedDatabase(_databasePath);
        }
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            try
            {
                File.Delete(_databasePath);
            }
            catch
            {
                // DuckDB might hold the file lock briefly
            }
        }

        // Also clean up WAL files if they exist
        var walPath = _databasePath + ".wal";
        if (File.Exists(walPath))
        {
            try
            {
                File.Delete(walPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    protected override IDataStoreProvider CreateProvider() => new DuckDBDataStoreProvider();
    protected override string ProviderName => "DuckDB";
    protected override (DataSourceDefinition, ServiceDefinition, LayerDefinition) GetMetadata() => _fixture.CreateMetadata(_databasePath);
    protected override bool ShouldSkip => _fixture.ShouldSkip;
    protected override string? SkipReason => _fixture.SkipReason;

    private static void SeedDatabase(string databasePath)
    {
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }

        var connectionString = $"DataSource={databasePath}";
        using var connection = new DuckDBConnection(connectionString);
        connection.Open();

        // Install and load spatial extension
        using (var loadExtension = connection.CreateCommand())
        {
            loadExtension.CommandText = "INSTALL spatial; LOAD spatial;";
            loadExtension.ExecuteNonQuery();
        }

        // Create table
        using (var createCommand = connection.CreateCommand())
        {
            createCommand.CommandText = """
                CREATE TABLE roads_primary (
                    road_id INTEGER PRIMARY KEY,
                    name VARCHAR,
                    status VARCHAR,
                    observed_at TIMESTAMP,
                    geom GEOMETRY
                );
                """;
            createCommand.ExecuteNonQuery();
        }

        // Insert test data
        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = """
            INSERT INTO roads_primary (road_id, name, status, observed_at, geom)
            VALUES ($1, $2, $3, $4, ST_Transform(ST_GeomFromText($5, 4326), 3857));
            """;

        // First record
        insertCommand.Parameters.Add(new DuckDBParameter(1001));
        insertCommand.Parameters.Add(new DuckDBParameter("Sunset Highway"));
        insertCommand.Parameters.Add(new DuckDBParameter("open"));
        insertCommand.Parameters.Add(new DuckDBParameter(DateTime.UtcNow));
        insertCommand.Parameters.Add(new DuckDBParameter("POINT(-122.5 45.5)"));
        insertCommand.ExecuteNonQuery();

        // Clear and add second record
        insertCommand.Parameters.Clear();
        insertCommand.Parameters.Add(new DuckDBParameter(1002));
        insertCommand.Parameters.Add(new DuckDBParameter("Pacific Avenue"));
        insertCommand.Parameters.Add(new DuckDBParameter("planned"));
        insertCommand.Parameters.Add(new DuckDBParameter(DateTime.UtcNow));
        insertCommand.Parameters.Add(new DuckDBParameter("POINT(-122.1 45.7)"));
        insertCommand.ExecuteNonQuery();
    }

    public sealed class DuckDBFixture
    {
        private string? _skipReason;

        public DuckDBFixture()
        {
            try
            {
                // Test if DuckDB is available
                var testPath = Path.Combine(Path.GetTempPath(), $"honua-duckdb-test-{Guid.NewGuid():N}.duckdb");
                try
                {
                    using var connection = new DuckDBConnection($"DataSource={testPath}");
                    connection.Open();

                    // Test spatial extension
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "INSTALL spatial; LOAD spatial; SELECT ST_Point(0, 0);";
                    cmd.ExecuteScalar();
                }
                finally
                {
                    if (File.Exists(testPath))
                    {
                        try
                        {
                            File.Delete(testPath);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or InvalidOperationException)
            {
                _skipReason = $"DuckDB is unavailable: {ex.Message}";
                ShouldSkip = true;
            }
            catch (Exception ex)
            {
                _skipReason = $"Failed to initialize DuckDB: {ex.Message}";
                ShouldSkip = true;
            }
        }

        public bool ShouldSkip { get; }
        public string? SkipReason => _skipReason ?? "DuckDB is unavailable.";

        public (DataSourceDefinition DataSource, ServiceDefinition Service, LayerDefinition Layer) CreateMetadata(string databasePath)
        {
            var connectionString = $"DataSource={databasePath}";

            var dataSource = new DataSourceDefinition
            {
                Id = "duckdb-primary",
                Provider = "duckdb",
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
                    new FieldDefinition { Name = "name", DataType = "string", StorageType = "varchar" },
                    new FieldDefinition { Name = "status", DataType = "string", StorageType = "varchar" },
                    new FieldDefinition { Name = "observed_at", DataType = "datetime", StorageType = "timestamp" },
                    new FieldDefinition { Name = "geom", DataType = "geometry", StorageType = "geometry" }
                }
            };

            return (dataSource, service, layer);
        }
    }
}

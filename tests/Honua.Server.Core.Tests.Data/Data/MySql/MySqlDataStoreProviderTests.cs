using System;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.MySql;
using Honua.Server.Core.Metadata;
using Testcontainers.MySql;
using Xunit;
using MySqlConnector;

namespace Honua.Server.Core.Tests.Data.Data.MySql;

/// <summary>
/// MySQL data store provider integration tests.
/// Uses MySQL container with spatial extensions for testing.
/// </summary>
[Collection("DatabaseTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "Data")]
[Trait("Database", "MySQL")]
[Trait("Speed", "Slow")]
public sealed class MySqlDataStoreProviderTests : DataStoreProviderTestsBase<MySqlDataStoreProviderFixture>
{
    private readonly MySqlDataStoreProviderFixture _fixture;

    public MySqlDataStoreProviderTests(MySqlDataStoreProviderFixture fixture)
    {
        _fixture = fixture;
    }

    protected override IDataStoreProvider CreateProvider() => new MySqlDataStoreProvider();
    protected override string ProviderName => "MySQL";
    protected override (DataSourceDefinition, ServiceDefinition, LayerDefinition) GetMetadata() => _fixture.CreateMetadata();
    protected override bool ShouldSkip => _fixture.ShouldSkip;
    protected override string? SkipReason => _fixture.SkipReason;
}

public sealed class MySqlDataStoreProviderFixture : IAsyncLifetime
{
    private MySqlContainer? _container;
    private bool _skip;
    private string? _skipReason;

    public MySqlDataStoreProviderFixture()
    {
        try
        {
            _container = new MySqlBuilder()
                .WithImage("mysql:8.4")
                .WithDatabase("honua_core")
                .WithUsername("honua")
                .WithPassword("honua_pass")
                .WithCleanUp(true)
                .Build();
        }
        catch (Exception ex)
        {
            _skip = true;
            _skipReason = $"MySQL container unavailable: {ex.Message}";
        }
    }

    public string ConnectionString => _container?.GetConnectionString() ?? throw new InvalidOperationException("MySQL container is not available.");
    public bool ShouldSkip => _skip;
    public string? SkipReason => _skipReason;

    public async Task InitializeAsync()
    {
        if (_skip || _container is null)
        {
            return;
        }

        try
        {
            await _container.StartAsync();
            await WaitForReadyAsync();
            await PrepareDatabaseAsync();
        }
        catch (Exception ex)
        {
            _skip = true;
            _skipReason = ex.Message;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    internal (DataSourceDefinition DataSource, ServiceDefinition Service, LayerDefinition Layer) CreateMetadata()
    {
        if (_container is null)
        {
            throw new InvalidOperationException("MySQL container is not available.");
        }

        var dataSource = new DataSourceDefinition
        {
            Id = "mysql-primary",
            Provider = "mysql",
            ConnectionString = ConnectionString
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
                Table = "roads",
                GeometryColumn = "geom",
                PrimaryKey = "road_id",
                TemporalColumn = "observed_at",
                Srid = 3857
            },
            Fields = new[]
            {
                new FieldDefinition { Name = "road_id", DataType = "int", StorageType = "int", Nullable = false },
                new FieldDefinition { Name = "name", DataType = "string", StorageType = "varchar" },
                new FieldDefinition { Name = "status", DataType = "string", StorageType = "varchar" },
                new FieldDefinition { Name = "observed_at", DataType = "datetime", StorageType = "datetime" },
                new FieldDefinition { Name = "geom", DataType = "geometry", StorageType = "geometry" }
            }
        };

        return (dataSource, service, layer);
    }

    private async Task WaitForReadyAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                await using var connection = new MySqlConnection(ConnectionString);
                await connection.OpenAsync();
                return;
            }
            catch
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        throw new InvalidOperationException("MySQL container did not become ready in time.");
    }

    private async Task PrepareDatabaseAsync()
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "DROP TABLE IF EXISTS roads;";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
CREATE TABLE roads (
    road_id INT PRIMARY KEY AUTO_INCREMENT,
    name VARCHAR(100),
    status VARCHAR(50),
    observed_at DATETIME(6),
    geom GEOMETRY NOT NULL SRID 3857
);
""";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
INSERT INTO roads (road_id, name, status, observed_at, geom)
VALUES
    (1001, 'Meridian Trail', 'open', '2022-01-01 00:00:00', ST_Transform(ST_GeomFromText('POINT(-122.5 45.5)', 4326), 3857)),
    (1002, 'Equatorial Drive', 'planned', '2022-01-02 00:00:00', ST_Transform(ST_GeomFromText('POINT(-70.0 5.0)', 4326), 3857));
""";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }
}

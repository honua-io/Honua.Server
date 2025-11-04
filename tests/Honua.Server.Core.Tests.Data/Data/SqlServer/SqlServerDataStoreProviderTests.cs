using System;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.SqlServer;
using Honua.Server.Core.Metadata;
using Testcontainers.MsSql;
using Xunit;
using Microsoft.Data.SqlClient;

namespace Honua.Server.Core.Tests.Data.Data.SqlServer;

/// <summary>
/// SQL Server data store provider integration tests.
/// Uses SQL Server 2022 container with spatial support.
/// </summary>
[Collection("DatabaseTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "Data")]
[Trait("Database", "SQLServer")]
[Trait("Speed", "Slow")]
public sealed class SqlServerDataStoreProviderTests : DataStoreProviderTestsBase<SqlServerDataStoreProviderFixture>
{
    private readonly SqlServerDataStoreProviderFixture _fixture;

    public SqlServerDataStoreProviderTests(SqlServerDataStoreProviderFixture fixture)
    {
        _fixture = fixture;
    }

    protected override IDataStoreProvider CreateProvider() => new SqlServerDataStoreProvider();
    protected override string ProviderName => "SQL Server";
    protected override (DataSourceDefinition, ServiceDefinition, LayerDefinition) GetMetadata() => _fixture.CreateMetadata();
    protected override bool ShouldSkip => _fixture.ShouldSkip;
    protected override string? SkipReason => _fixture.SkipReason;
}

public sealed class SqlServerDataStoreProviderFixture : IAsyncLifetime
{
    private const string DatabaseName = "honua_core";

    private MsSqlContainer? _container;
    private bool _skip;
    private string? _catalogConnectionString;
    private string? _skipReason;

    public SqlServerDataStoreProviderFixture()
    {
        var enabled = Environment.GetEnvironmentVariable("HONUA_ENABLE_SQLSERVER_TESTS");
        if (!string.Equals(enabled, "1", StringComparison.OrdinalIgnoreCase) && !string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            _skip = true;
            _skipReason = "SQL Server integration tests disabled. Set HONUA_ENABLE_SQLSERVER_TESTS=1 to enable.";
            return;
        }

        try
        {
            _container = new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                .WithPassword("Your_password123")
                .WithEnvironment("MSSQL_PID", "Developer")
                .WithEnvironment("ACCEPT_EULA", "Y")
                .WithCleanUp(true)
                .Build();
        }
        catch (Exception ex)
        {
            _skip = true;
            _skipReason = $"SQL Server container unavailable: {ex.Message}";
        }
    }

    public string ConnectionString => _catalogConnectionString ?? _container?.GetConnectionString() ?? throw new InvalidOperationException("SQL Server container is not available.");
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
            await EnsureDatabaseAsync();
            var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
            {
                InitialCatalog = DatabaseName
            };
            _catalogConnectionString = builder.ConnectionString;
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
        var dataSource = new DataSourceDefinition
        {
            Id = "sqlserver-primary",
            Provider = "sqlserver",
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
                Table = "dbo.Roads",
                GeometryColumn = "geom",
                PrimaryKey = "road_id",
                TemporalColumn = "observed_at",
                Srid = 4326
            },
            Fields = new[]
            {
                new FieldDefinition { Name = "road_id", DataType = "int", StorageType = "int", Nullable = false },
                new FieldDefinition { Name = "name", DataType = "string", StorageType = "nvarchar" },
                new FieldDefinition { Name = "status", DataType = "string", StorageType = "nvarchar" },
                new FieldDefinition { Name = "observed_at", DataType = "datetimeoffset", StorageType = "datetimeoffset" },
                new FieldDefinition { Name = "geom", DataType = "geometry", StorageType = "geometry" }
            }
        };

        return (dataSource, service, layer);
    }

    private async Task EnsureDatabaseAsync()
    {
#pragma warning disable CS8602 // False positive - null check is present
        var connectionString = _container.GetConnectionString() ?? throw new InvalidOperationException("Connection string is null");
#pragma warning restore CS8602
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var commandText = $"IF DB_ID('{DatabaseName}') IS NULL BEGIN CREATE DATABASE [{DatabaseName}]; END;";
        await using var command = new SqlCommand(commandText, connection);
        command.CommandTimeout = 60;
        await command.ExecuteNonQueryAsync();
    }

    private async Task PrepareDatabaseAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        var commandText = @"
if object_id('dbo.Roads', 'U') is not null drop table dbo.Roads;
create table dbo.Roads (
    road_id int not null primary key,
    name nvarchar(200) null,
    status nvarchar(20) null,
    observed_at datetimeoffset null,
    geom geometry null
);

insert into dbo.Roads (road_id, name, status, observed_at, geom)
values
(1001, 'Highway 26', 'open', SYSDATETIMEOFFSET(), geometry::STGeomFromText('POINT(-122.5 45.5)', 4326)),
(1002, 'Highway 101', 'closed', SYSDATETIMEOFFSET(), geometry::STGeomFromText('POINT(-122.1 45.7)', 4326));
";
        await using var command = new SqlCommand(commandText, connection);
        command.CommandTimeout = 60;
        await command.ExecuteNonQueryAsync();
    }
}

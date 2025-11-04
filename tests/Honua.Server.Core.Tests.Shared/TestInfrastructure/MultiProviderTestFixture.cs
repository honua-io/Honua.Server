using System.Data;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using Xunit;
using Xunit.Sdk;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// Test fixture that provides multiple database providers (SQLite, PostgreSQL, MySQL, SQL Server)
/// with identical test data for comprehensive provider × API × format matrix testing.
/// </summary>
public sealed class MultiProviderTestFixture : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private IContainer? _mysqlContainer;

    private string? _sqlitePath;

    public Dictionary<string, TestDatabaseInfo> Providers { get; } = new();

    public const string SqliteProvider = "sqlite";
    public const string PostgresProvider = "postgres";
    public const string MySqlProvider = "mysql";

    public async Task InitializeAsync()
    {
        // Initialize SQLite (fastest, always available)
        await InitializeSqliteAsync();

        // Initialize PostgreSQL (via Testcontainers)
        await InitializePostgresAsync();

        // Initialize MySQL (via Testcontainers)
        await InitializeMySqlAsync();

        // Seed all providers with identical test data
        await SeedAllProvidersAsync();
    }

    private async Task InitializeSqliteAsync()
    {
        _sqlitePath = Path.Combine(Path.GetTempPath(), $"honua-test-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={_sqlitePath}";

        Providers[SqliteProvider] = new TestDatabaseInfo
        {
            ProviderName = SqliteProvider,
            ConnectionString = connectionString,
            ProviderType = "sqlite"
        };

        // Initialize database schema
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        // Create extension loading if available (optional for TEXT-based storage)
        try
        {
            using var extCmd = connection.CreateCommand();
            extCmd.CommandText = "SELECT load_extension('mod_spatialite');";
            await extCmd.ExecuteNonQueryAsync();
            Providers[SqliteProvider].SupportsSpatialExtension = true;
        }
        catch
        {
            // SpatiaLite not available, will use TEXT-based storage
            Providers[SqliteProvider].SupportsSpatialExtension = false;
        }
    }

    private async Task InitializePostgresAsync()
    {
        try
        {
            _postgresContainer = new ContainerBuilder()
                .WithImage("postgis/postgis:16-3.4")
                .WithPortBinding(5432, true)
                .WithEnvironment("POSTGRES_USER", "honua_test")
                .WithEnvironment("POSTGRES_PASSWORD", "honua_test_password")
                .WithEnvironment("POSTGRES_DB", "honua_test")
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilCommandIsCompleted("pg_isready", "-U", "honua_test"))
                .Build();

            await _postgresContainer.StartAsync();

            var port = _postgresContainer.GetMappedPublicPort(5432);
            var connectionString = $"Host=localhost;Port={port};Database=honua_test;Username=honua_test;Password=honua_test_password;";

            Providers[PostgresProvider] = new TestDatabaseInfo
            {
                ProviderName = PostgresProvider,
                ConnectionString = connectionString,
                ProviderType = "postgres",
                SupportsSpatialExtension = true // PostGIS
            };

            // Enable PostGIS
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS postgis;";
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[MultiProviderTestFixture] Unable to start PostgreSQL test container: {ex.Message}");
            _postgresContainer = null;
        }
    }

    private async Task InitializeMySqlAsync()
    {
        try
        {
            _mysqlContainer = new ContainerBuilder()
                .WithImage("mysql:8.0")
                .WithPortBinding(3306, true)
                .WithEnvironment("MYSQL_ROOT_PASSWORD", "honua_test_password")
                .WithEnvironment("MYSQL_DATABASE", "honua_test")
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilCommandIsCompleted("/bin/sh", "-c", "mysqladmin ping -h localhost -uroot -phonua_test_password || exit 1"))
                .Build();

            await _mysqlContainer.StartAsync();

            var port = _mysqlContainer.GetMappedPublicPort(3306);
            var connectionString = $"Server=localhost;Port={port};Database=honua_test;User=root;Password=honua_test_password;";

            Providers[MySqlProvider] = new TestDatabaseInfo
            {
                ProviderName = MySqlProvider,
                ConnectionString = connectionString,
                ProviderType = "mysql",
                SupportsSpatialExtension = true // MySQL spatial types
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[MultiProviderTestFixture] Unable to start MySQL test container: {ex.Message}");
            _mysqlContainer = null;
        }
    }

    private async Task SeedAllProvidersAsync()
    {
        foreach (var provider in Providers.Values)
        {
            await SeedProviderAsync(provider);
        }
    }

    private async Task SeedProviderAsync(TestDatabaseInfo provider)
    {
        switch (provider.ProviderType)
        {
            case "sqlite":
                await SeedSqliteAsync(provider);
                break;
            case "postgres":
                await SeedPostgresAsync(provider);
                break;
            case "mysql":
                await SeedMySqlAsync(provider);
                break;
            default:
                throw new NotSupportedException($"Provider {provider.ProviderType} not supported");
        }
    }

    private async Task SeedSqliteAsync(TestDatabaseInfo provider)
    {
        using var connection = new SqliteConnection(provider.ConnectionString);
        await connection.OpenAsync();

        var featureId = 1;
        foreach (var (type, scenario) in GeometryTestData.GetAllTestCombinations())
        {
            var tableName = GetTableName(type, scenario);
            var geometry = GeometryTestData.GetTestGeometry(type, scenario);
            var wkt = GeometryTestData.ToWkt(geometry);

            // Create table
            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = $@"
                CREATE TABLE IF NOT EXISTS {tableName} (
                    feature_id INTEGER PRIMARY KEY,
                    name TEXT,
                    geometry_type TEXT,
                    category TEXT,
                    priority INTEGER,
                    active INTEGER,
                    created_at TEXT,
                    measurement REAL,
                    description TEXT,
                    geom TEXT
                );";
            await createCmd.ExecuteNonQueryAsync();

            // Insert test feature
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = $@"
                INSERT INTO {tableName} (feature_id, name, geometry_type, category, priority, active, created_at, measurement, description, geom)
                VALUES (@id, @name, @geom_type, @category, @priority, @active, @created_at, @measurement, @description, @geom);";

            var attrs = GeometryTestData.GetTestAttributes(type, featureId++);
            insertCmd.Parameters.AddWithValue("@id", attrs["feature_id"]!);
            insertCmd.Parameters.AddWithValue("@name", attrs["name"]!);
            insertCmd.Parameters.AddWithValue("@geom_type", attrs["geometry_type"]!);
            insertCmd.Parameters.AddWithValue("@category", attrs["category"]!);
            insertCmd.Parameters.AddWithValue("@priority", attrs["priority"]!);
            insertCmd.Parameters.AddWithValue("@active", (bool)attrs["active"]! ? 1 : 0);
            insertCmd.Parameters.AddWithValue("@created_at", ((DateTime)attrs["created_at"]!).ToString("o"));
            insertCmd.Parameters.AddWithValue("@measurement", attrs["measurement"]!);
            insertCmd.Parameters.AddWithValue("@description", attrs["description"]!);
            insertCmd.Parameters.AddWithValue("@geom", GeometryTestData.ToGeoJson(geometry));

            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    private async Task SeedPostgresAsync(TestDatabaseInfo provider)
    {
        using var connection = new NpgsqlConnection(provider.ConnectionString);
        await connection.OpenAsync();

        var featureId = 1;
        foreach (var (type, scenario) in GeometryTestData.GetAllTestCombinations())
        {
            var tableName = GetTableName(type, scenario);
            var geometry = GeometryTestData.GetTestGeometry(type, scenario);
            var wkt = GeometryTestData.ToWkt(geometry);

            // Create table with PostGIS geometry
            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = $@"
                CREATE TABLE IF NOT EXISTS {tableName} (
                    feature_id INTEGER PRIMARY KEY,
                    name TEXT,
                    geometry_type TEXT,
                    category TEXT,
                    priority INTEGER,
                    active BOOLEAN,
                    created_at TIMESTAMP,
                    measurement DOUBLE PRECISION,
                    description TEXT,
                    geom GEOMETRY(Geometry, 4326)
                );";
            await createCmd.ExecuteNonQueryAsync();

            // Insert test feature using ST_GeomFromText
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = $@"
                INSERT INTO {tableName} (feature_id, name, geometry_type, category, priority, active, created_at, measurement, description, geom)
                VALUES (@id, @name, @geom_type, @category, @priority, @active, @created_at, @measurement, @description, ST_GeomFromText(@wkt, 4326))
                ON CONFLICT (feature_id) DO NOTHING;";

            var attrs = GeometryTestData.GetTestAttributes(type, featureId++);
            insertCmd.Parameters.AddWithValue("@id", attrs["feature_id"]!);
            insertCmd.Parameters.AddWithValue("@name", attrs["name"]!);
            insertCmd.Parameters.AddWithValue("@geom_type", attrs["geometry_type"]!);
            insertCmd.Parameters.AddWithValue("@category", attrs["category"]!);
            insertCmd.Parameters.AddWithValue("@priority", attrs["priority"]!);
            insertCmd.Parameters.AddWithValue("@active", (bool)attrs["active"]!);
            insertCmd.Parameters.AddWithValue("@created_at", (DateTime)attrs["created_at"]!);
            insertCmd.Parameters.AddWithValue("@measurement", attrs["measurement"]!);
            insertCmd.Parameters.AddWithValue("@description", attrs["description"]!);
            insertCmd.Parameters.AddWithValue("@wkt", wkt);

            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    private async Task SeedMySqlAsync(TestDatabaseInfo provider)
    {
        using var connection = new MySqlConnection(provider.ConnectionString);
        await connection.OpenAsync();

        var featureId = 1;
        foreach (var (type, scenario) in GeometryTestData.GetAllTestCombinations())
        {
            var tableName = GetTableName(type, scenario);
            var geometry = GeometryTestData.GetTestGeometry(type, scenario);
            var wkt = GeometryTestData.ToWkt(geometry);

            // Create table with MySQL geometry
            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = $@"
                CREATE TABLE IF NOT EXISTS {tableName} (
                    feature_id INTEGER PRIMARY KEY,
                    name TEXT,
                    geometry_type TEXT,
                    category TEXT,
                    priority INTEGER,
                    active BOOLEAN,
                    created_at DATETIME,
                    measurement DOUBLE,
                    description TEXT,
                    geom GEOMETRY NOT NULL SRID 4326
                );";
            await createCmd.ExecuteNonQueryAsync();

            // Insert test feature using ST_GeomFromText
            // MySQL 8.0 with SRID 4326 expects lat/lon order by default, but WKT is lon/lat
            // Use 'axis-order=long-lat' to force XY (lon/lat) interpretation
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = $@"
                INSERT IGNORE INTO {tableName} (feature_id, name, geometry_type, category, priority, active, created_at, measurement, description, geom)
                VALUES (@id, @name, @geom_type, @category, @priority, @active, @created_at, @measurement, @description, ST_GeomFromText(@wkt, 4326, 'axis-order=long-lat'));";

            var attrs = GeometryTestData.GetTestAttributes(type, featureId++);
            insertCmd.Parameters.AddWithValue("@id", attrs["feature_id"]!);
            insertCmd.Parameters.AddWithValue("@name", attrs["name"]!);
            insertCmd.Parameters.AddWithValue("@geom_type", attrs["geometry_type"]!);
            insertCmd.Parameters.AddWithValue("@category", attrs["category"]!);
            insertCmd.Parameters.AddWithValue("@priority", attrs["priority"]!);
            insertCmd.Parameters.AddWithValue("@active", (bool)attrs["active"]!);
            insertCmd.Parameters.AddWithValue("@created_at", (DateTime)attrs["created_at"]!);
            insertCmd.Parameters.AddWithValue("@measurement", attrs["measurement"]!);
            insertCmd.Parameters.AddWithValue("@description", attrs["description"]!);
            insertCmd.Parameters.AddWithValue("@wkt", wkt);

            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    private static string GetTableName(GeometryTestData.GeometryType type, GeometryTestData.GeodeticScenario scenario)
    {
        // For Simple scenario, use simple table name
        if (scenario == GeometryTestData.GeodeticScenario.Simple)
        {
            return $"test_{type.ToString().ToLowerInvariant()}";
        }

        // For other scenarios, include scenario in table name to avoid conflicts
        return $"test_{type.ToString().ToLowerInvariant()}_{scenario.ToString().ToLowerInvariant()}";
    }

    public async Task DisposeAsync()
    {
        // Cleanup containers
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }

        if (_mysqlContainer != null)
        {
            await _mysqlContainer.DisposeAsync();
        }

        // Cleanup SQLite file
        if (_sqlitePath != null && File.Exists(_sqlitePath))
        {
            File.Delete(_sqlitePath);
        }
    }

    /// <summary>
    /// Get metadata definition for a specific geometry type and provider.
    /// </summary>
    public (DataSourceDefinition DataSource, ServiceDefinition Service, LayerDefinition Layer) GetMetadata(
        string providerName,
        GeometryTestData.GeometryType geometryType,
        GeometryTestData.GeodeticScenario scenario = GeometryTestData.GeodeticScenario.Simple)
    {
        if (!Providers.TryGetValue(providerName, out var provider))
        {
            throw new SkipException($"Provider '{providerName}' is not available (Docker required).");
        }
        var tableName = GetTableName(geometryType, scenario);

        var dataSource = new DataSourceDefinition
        {
            Id = $"test-{providerName}",
            Provider = provider.ProviderType,
            ConnectionString = provider.ConnectionString
        };

        var service = new ServiceDefinition
        {
            Id = $"test-service-{providerName}",
            Title = $"Test Service ({providerName})",
            FolderId = "test",
            ServiceType = "feature",
            DataSourceId = dataSource.Id,
            Enabled = true,
            Ogc = new OgcServiceDefinition
            {
                DefaultCrs = "EPSG:4326"
            },
            Layers = Array.Empty<LayerDefinition>()
        };

        var layer = new LayerDefinition
        {
            Id = $"test-{geometryType.ToString().ToLowerInvariant()}",
            ServiceId = service.Id,
            Title = $"{geometryType} Test Layer",
            GeometryType = geometryType.ToString(),
            IdField = "feature_id",
            DisplayField = "name",
            GeometryField = "geom",
            Crs = new[] { "EPSG:4326" },
            Storage = new LayerStorageDefinition
            {
                Table = tableName,
                GeometryColumn = "geom",
                PrimaryKey = "feature_id",
                Srid = 4326,
                Crs = "EPSG:4326"
            },
            Fields = new[]
            {
                new FieldDefinition { Name = "feature_id", DataType = "int64", Nullable = false, Editable = false },
                new FieldDefinition { Name = "name", DataType = "string", Nullable = true },
                new FieldDefinition { Name = "geometry_type", DataType = "string", Nullable = true },
                new FieldDefinition { Name = "category", DataType = "string", Nullable = true },
                new FieldDefinition { Name = "priority", DataType = "int32", Nullable = true },
                new FieldDefinition { Name = "active", DataType = "boolean", Nullable = true },
                new FieldDefinition { Name = "created_at", DataType = "datetime", Nullable = true },
                new FieldDefinition { Name = "measurement", DataType = "double", Nullable = true },
                new FieldDefinition { Name = "description", DataType = "string", Nullable = true }
            }
        };

        return (dataSource, service, layer);
    }
}

public sealed class TestDatabaseInfo
{
    public required string ProviderName { get; init; }
    public required string ConnectionString { get; init; }
    public required string ProviderType { get; init; }
    public bool SupportsSpatialExtension { get; set; }
}

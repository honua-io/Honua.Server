// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.MySql;
using Testcontainers.Redis;
using Xunit;

namespace Honua.Server.Integration.Tests.Fixtures;

/// <summary>
/// Provides TestContainers-based database instances for integration testing.
/// This fixture creates real PostgreSQL (with PostGIS), MySQL, and Redis containers
/// to enable thorough integration testing without mocking database operations.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private MySqlContainer? _mySqlContainer;
    private RedisContainer? _redisContainer;

    /// <summary>
    /// PostgreSQL connection string (includes PostGIS extension).
    /// </summary>
    public string PostgresConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// MySQL connection string with spatial extensions enabled.
    /// </summary>
    public string MySqlConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Redis connection string.
    /// </summary>
    public string RedisConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Indicates whether the PostgreSQL container is ready for use.
    /// </summary>
    public bool IsPostgresReady { get; private set; }

    /// <summary>
    /// Indicates whether the MySQL container is ready for use.
    /// </summary>
    public bool IsMySqlReady { get; private set; }

    /// <summary>
    /// Indicates whether the Redis container is ready for use.
    /// </summary>
    public bool IsRedisReady { get; private set; }

    public async Task InitializeAsync()
    {
        // Initialize PostgreSQL with PostGIS
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgis/postgis:16-3.4")
            .WithDatabase("honua_test")
            .WithUsername("postgres")
            .WithPassword("test")
            .WithCleanUp(true)
            .Build();

        // Initialize MySQL
        _mySqlContainer = new MySqlBuilder()
            .WithImage("mysql:8.0")
            .WithDatabase("honua_test")
            .WithUsername("root")
            .WithPassword("test")
            .WithCleanUp(true)
            .Build();

        // Initialize Redis
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithCleanUp(true)
            .Build();

        // Start all containers in parallel for faster test initialization
        var postgresTask = StartPostgresAsync();
        var mysqlTask = StartMySqlAsync();
        var redisTask = StartRedisAsync();

        await Task.WhenAll(postgresTask, mysqlTask, redisTask);
    }

    private async Task StartPostgresAsync()
    {
        try
        {
            await _postgresContainer!.StartAsync();
            PostgresConnectionString = _postgresContainer.GetConnectionString();

            // Initialize database schema and test data
            await InitializePostgresSchemaAsync();

            IsPostgresReady = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start PostgreSQL container: {ex.Message}");
            IsPostgresReady = false;
        }
    }

    private async Task InitializePostgresSchemaAsync()
    {
        await using var connection = new NpgsqlConnection(PostgresConnectionString);
        await connection.OpenAsync();

        // Enable PostGIS extension
        await using var enablePostgisCmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS postgis;", connection);
        await enablePostgisCmd.ExecuteNonQueryAsync();

        // Create features table for testing
        var createTableSql = @"
            DROP TABLE IF EXISTS features CASCADE;
            CREATE TABLE features (
                id SERIAL PRIMARY KEY,
                name VARCHAR(255) NOT NULL,
                description TEXT,
                category VARCHAR(100),
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                geom GEOMETRY(Geometry, 4326)
            );

            -- Create spatial index
            CREATE INDEX IF NOT EXISTS idx_features_geom ON features USING GIST(geom);
        ";

        await using var createTableCmd = new NpgsqlCommand(createTableSql, connection);
        await createTableCmd.ExecuteNonQueryAsync();

        // Insert test data
        var insertDataSql = @"
            INSERT INTO features (name, description, category, geom) VALUES
            ('Test Point 1', 'A test point feature', 'poi', ST_SetSRID(ST_MakePoint(-122.4194, 37.7749), 4326)),
            ('Test Point 2', 'Another test point', 'poi', ST_SetSRID(ST_MakePoint(-118.2437, 34.0522), 4326)),
            ('Test Point 3', 'Third test point', 'landmark', ST_SetSRID(ST_MakePoint(-73.9857, 40.7484), 4326)),
            ('Test Polygon 1', 'A test polygon', 'boundary', ST_SetSRID(ST_GeomFromText('POLYGON((-122.5 37.5, -122.5 38.0, -122.0 38.0, -122.0 37.5, -122.5 37.5))'), 4326)),
            ('Test LineString 1', 'A test line', 'road', ST_SetSRID(ST_GeomFromText('LINESTRING(-122.4 37.7, -122.5 37.8, -122.6 37.9)'), 4326));
        ";

        await using var insertDataCmd = new NpgsqlCommand(insertDataSql, connection);
        await insertDataCmd.ExecuteNonQueryAsync();

        Console.WriteLine("PostgreSQL test schema and data initialized successfully");
    }

    private async Task StartMySqlAsync()
    {
        try
        {
            await _mySqlContainer!.StartAsync();
            MySqlConnectionString = _mySqlContainer.GetConnectionString();
            IsMySqlReady = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start MySQL container: {ex.Message}");
            IsMySqlReady = false;
        }
    }

    private async Task StartRedisAsync()
    {
        try
        {
            await _redisContainer!.StartAsync();
            RedisConnectionString = _redisContainer.GetConnectionString();
            IsRedisReady = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start Redis container: {ex.Message}");
            IsRedisReady = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }

        if (_mySqlContainer != null)
        {
            await _mySqlContainer.DisposeAsync();
        }

        if (_redisContainer != null)
        {
            await _redisContainer.DisposeAsync();
        }
    }
}

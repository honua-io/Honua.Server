// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
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
            IsPostgresReady = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start PostgreSQL container: {ex.Message}");
            IsPostgresReady = false;
        }
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

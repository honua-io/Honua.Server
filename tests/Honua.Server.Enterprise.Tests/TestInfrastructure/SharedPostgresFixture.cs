// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Data;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Honua.Server.Enterprise.Tests.TestInfrastructure;

/// <summary>
/// Shared PostgreSQL container fixture that uses a single container for all tests in the collection.
/// Uses transaction-based isolation to ensure test independence while maximizing performance.
/// </summary>
[CollectionDefinition("SharedPostgres")]
public class SharedPostgresCollection : ICollectionFixture<SharedPostgresFixture>
{
}

public sealed class SharedPostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;
    private bool _isAvailable;
    private string? _failureReason;

    public string ConnectionString
        => _isAvailable ? _connectionString! : throw new InvalidOperationException($"PostgreSQL test container is not available. Reason: {_failureReason ?? "Unknown"}");

    public bool IsAvailable => _isAvailable;

    public string? FailureReason => _failureReason;

    public async Task InitializeAsync()
    {
        try
        {
            // Check Docker availability first
            if (!await CheckDockerAvailabilityAsync())
            {
                _failureReason = "Docker is not available or not running. Please ensure Docker is installed and running.";
                _isAvailable = false;
                Console.Error.WriteLine($"[SharedPostgresFixture] {_failureReason}");
                return;
            }

            _container = new PostgreSqlBuilder()
                .WithImage("postgis/postgis:16-3.4")
                .WithDatabase("honua_test")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
                .WithStartupCallback((container, ct) =>
                {
                    Console.WriteLine("[SharedPostgresFixture] PostgreSQL container started, configuring...");
                    return Task.CompletedTask;
                })
                .WithCleanUp(true)
                .Build();

            Console.WriteLine("[SharedPostgresFixture] Starting PostgreSQL container...");
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();

            // Give PostgreSQL extra time to fully initialize
            Console.WriteLine("[SharedPostgresFixture] Waiting for PostgreSQL to fully initialize...");
            await Task.Delay(1000);

            // Enable PostGIS extension (with retry for connection timing issues)
            const int maxRetries = 10;
            Exception? lastException = null;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    await using var connection = new NpgsqlConnection(_connectionString);
                    await connection.OpenAsync();

                    // Verify we can actually query the database
                    await using var testCmd = connection.CreateCommand();
                    testCmd.CommandText = "SELECT version();";
                    await testCmd.ExecuteScalarAsync();

                    // Now create PostGIS extension
                    await using var cmd = connection.CreateCommand();
                    cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS postgis;";
                    await cmd.ExecuteNonQueryAsync();

                    _isAvailable = true;
                    Console.WriteLine($"[SharedPostgresFixture] PostgreSQL container started successfully on attempt {i + 1}");
                    return; // Success!
                }
                catch (Exception ex) when (i < maxRetries - 1)
                {
                    lastException = ex;
                    // Wait a bit before retrying (exponential backoff with cap)
                    var delayMs = Math.Min(300 * (i + 1), 2000); // 300ms, 600ms, 900ms, ..., max 2000ms
                    Console.WriteLine($"[SharedPostgresFixture] PostGIS extension creation failed on attempt {i + 1}/{maxRetries} ({ex.Message}), retrying in {delayMs}ms...");
                    await Task.Delay(delayMs);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }
            }

            // If we get here, all retries failed
            _failureReason = $"Failed to enable PostGIS extension after {maxRetries} attempts. Last error: {lastException?.Message}";
            _isAvailable = false;
            Console.Error.WriteLine($"[SharedPostgresFixture] {_failureReason}");
        }
        catch (Exception ex)
        {
            _failureReason = ex.Message;
            Console.Error.WriteLine($"[SharedPostgresFixture] Unable to start PostgreSQL test container: {ex.Message}");
            Console.Error.WriteLine($"[SharedPostgresFixture] Stack trace: {ex.StackTrace}");
            _isAvailable = false;
        }
    }

    /// <summary>
    /// Checks if Docker is available by attempting to create a simple test container.
    /// </summary>
    private static async Task<bool> CheckDockerAvailabilityAsync()
    {
        try
        {
            var testContainer = new ContainerBuilder()
                .WithImage("alpine:latest")
                .WithCommand("echo", "test")
                .WithWaitStrategy(Wait.ForUnixContainer())
                .Build();

            await testContainer.StartAsync();
            await testContainer.DisposeAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container != null && _isAvailable)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates a transaction scope for test isolation.
    /// Call BeginTransaction at the start of your test and Rollback at the end.
    /// </summary>
    public async Task<NpgsqlConnection> CreateConnectionAsync()
    {
        if (!_isAvailable)
        {
            throw new InvalidOperationException("PostgreSQL test container is not available.");
        }

        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>
    /// Creates a connection with an active transaction for test isolation.
    /// </summary>
    public async Task<(NpgsqlConnection Connection, NpgsqlTransaction Transaction)> CreateTransactionScopeAsync()
    {
        if (!_isAvailable)
        {
            throw new InvalidOperationException("PostgreSQL test container is not available.");
        }

        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        return (connection, transaction);
    }
}

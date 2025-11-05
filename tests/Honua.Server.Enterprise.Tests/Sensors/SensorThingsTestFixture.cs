// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Honua.Server.Enterprise.Tests.TestInfrastructure;
using Npgsql;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Sensors;

/// <summary>
/// Test fixture for SensorThings API tests that sets up the database schema.
/// Uses the shared PostgreSQL container and creates the SensorThings tables in a transaction scope.
/// </summary>
[CollectionDefinition("SensorThings")]
public class SensorThingsCollection : ICollectionFixture<SensorThingsTestFixture>
{
}

public sealed class SensorThingsTestFixture : IAsyncLifetime
{
    private readonly SharedPostgresFixture _postgresFixture;
    private bool _schemaInitialized;

    public SensorThingsTestFixture()
    {
        _postgresFixture = new SharedPostgresFixture();
    }

    public string ConnectionString => _postgresFixture.ConnectionString;
    public bool IsAvailable => _postgresFixture.IsAvailable && _schemaInitialized;

    public async Task InitializeAsync()
    {
        try
        {
            // Initialize PostgreSQL container
            await _postgresFixture.InitializeAsync();

            if (!_postgresFixture.IsAvailable)
            {
                _schemaInitialized = false;
                return;
            }

            // Run the SensorThings schema migration
            await InitializeSensorThingsSchemaAsync();
            _schemaInitialized = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SensorThingsTestFixture] Failed to initialize: {ex.Message}");
            _schemaInitialized = false;
        }
    }

    private async Task InitializeSensorThingsSchemaAsync()
    {
        // Read the migration SQL file
        var migrationPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..", // Navigate up from test output directory
            "src", "Honua.Server.Enterprise", "Sensors", "Data", "Migrations", "001_InitialSchema.sql"
        );

        // Fallback: try relative to solution root
        if (!File.Exists(migrationPath))
        {
            migrationPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "src", "Honua.Server.Enterprise", "Sensors", "Data", "Migrations", "001_InitialSchema.sql"
            );
        }

        if (!File.Exists(migrationPath))
        {
            throw new FileNotFoundException(
                $"Could not find SensorThings migration file. Looked at: {migrationPath}",
                migrationPath);
        }

        var migrationSql = await File.ReadAllTextAsync(migrationPath);

        await using var connection = await _postgresFixture.CreateConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = migrationSql;
        command.CommandTimeout = 120; // 2 minutes for schema creation

        Console.WriteLine("[SensorThingsTestFixture] Running SensorThings schema migration...");
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("[SensorThingsTestFixture] Schema migration completed successfully");
    }

    public async Task DisposeAsync()
    {
        await _postgresFixture.DisposeAsync();
    }

    /// <summary>
    /// Creates a connection for test use.
    /// Each test should create its own transaction for isolation.
    /// </summary>
    public async Task<NpgsqlConnection> CreateConnectionAsync()
    {
        return await _postgresFixture.CreateConnectionAsync();
    }

    /// <summary>
    /// Creates a connection with an active transaction for test isolation.
    /// Tests should rollback the transaction at the end to ensure isolation.
    /// </summary>
    public async Task<(NpgsqlConnection Connection, NpgsqlTransaction Transaction)> CreateTransactionScopeAsync()
    {
        return await _postgresFixture.CreateTransactionScopeAsync();
    }
}

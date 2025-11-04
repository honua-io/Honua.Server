using System;
using System.Data;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Honua.Server.Core.Tests.Shared;

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

    public string ConnectionString
        => _isAvailable ? _connectionString! : throw new InvalidOperationException("PostgreSQL test container is not available.");

    public bool IsAvailable => _isAvailable;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgis/postgis:16-3.4")
                .WithDatabase("honua_test")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
                .WithCleanUp(true)
                .Build();

            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS postgis;";
            await cmd.ExecuteNonQueryAsync();

            _isAvailable = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SharedPostgresFixture] Unable to start PostgreSQL test container: {ex.Message}");
            Console.Error.WriteLine($"[SharedPostgresFixture] Stack trace: {ex.StackTrace}");
            _isAvailable = false;
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

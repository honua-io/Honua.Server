// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using MySqlConnector;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace Honua.Cli.AI.Services.Execution;

/// <summary>
/// Provides safe database operations using proper database drivers instead of shell execution.
/// This eliminates command injection vulnerabilities that can occur with shell-based SQL execution.
/// Supports PostgreSQL, MySQL, SQL Server, and SQLite.
/// </summary>
public class DatabaseService
{
    /// <summary>
    /// Supported database types
    /// </summary>
    public enum DatabaseType
    {
        PostgreSQL,
        MySQL,
        SqlServer,
        SQLite
    }

    /// <summary>
    /// Parse database type from string
    /// </summary>
    private static DatabaseType ParseDatabaseType(string dbType)
    {
        return dbType.ToLowerInvariant() switch
        {
            "postgres" or "postgresql" or "pg" => DatabaseType.PostgreSQL,
            "mysql" or "mariadb" => DatabaseType.MySQL,
            "sqlserver" or "mssql" or "sql" => DatabaseType.SqlServer,
            "sqlite" or "sqlite3" => DatabaseType.SQLite,
            _ => throw new ArgumentException($"Unsupported database type: {dbType}. Supported types: postgres, mysql, sqlserver, sqlite")
        };
    }

    /// <summary>
    /// Create a database connection based on the database type
    /// </summary>
    private static DbConnection CreateConnection(string connectionString, DatabaseType dbType)
    {
        return dbType switch
        {
            DatabaseType.PostgreSQL => new NpgsqlConnection(connectionString),
            DatabaseType.MySQL => new MySqlConnection(connectionString),
            DatabaseType.SqlServer => new SqlConnection(connectionString),
            DatabaseType.SQLite => new SqliteConnection(connectionString),
            _ => throw new ArgumentException($"Unsupported database type: {dbType}")
        };
    }
    /// <summary>
    /// Executes DDL SQL against a database using the appropriate driver.
    /// This method is safe from command injection as it uses proper database drivers.
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="sql">DDL SQL to execute (will be validated)</param>
    /// <param name="dbType">Database type (postgres, mysql, sqlserver, sqlite)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success message or error details</returns>
    /// <exception cref="ArgumentException">Thrown if SQL validation fails</exception>
    /// <exception cref="InvalidOperationException">Thrown if database operation fails</exception>
    public async Task<string> ExecuteDdlAsync(string connectionString, string sql, string dbType = "postgres", CancellationToken cancellationToken = default)
    {
        // Validate connection string
        CommandArgumentValidator.ValidateConnectionString(connectionString, nameof(connectionString));

        // Validate SQL is DDL only - uses allowlist approach
        CommandArgumentValidator.ValidateDDLStatement(sql, nameof(sql));

        var databaseType = ParseDatabaseType(dbType);

        try
        {
            await using var connection = CreateConnection(connectionString, databaseType);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 30; // 30 second timeout for DDL operations

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            return $"Command completed successfully. Rows affected: {rowsAffected}";
        }
        catch (DbException dbEx)
        {
            throw new InvalidOperationException($"{databaseType} error: {dbEx.Message}", dbEx);
        }
    }

    /// <summary>
    /// Executes a SELECT query against a database and returns the results as a string.
    /// Safe from SQL injection when used with validated input.
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="sql">SELECT query to execute</param>
    /// <param name="dbType">Database type (postgres, mysql, sqlserver, sqlite)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Query results as formatted string</returns>
    public async Task<string> ExecuteQueryAsync(string connectionString, string sql, string dbType = "postgres", CancellationToken cancellationToken = default)
    {
        // Validate connection string
        CommandArgumentValidator.ValidateConnectionString(connectionString, nameof(connectionString));

        // Validate SQL - must be SELECT only
        var normalized = sql.Trim().ToUpperInvariant();
        if (!normalized.StartsWith("SELECT"))
        {
            throw new ArgumentException("Only SELECT queries are allowed in ExecuteQueryAsync", nameof(sql));
        }

        // Run general SQL validation
        CommandArgumentValidator.ValidateSQL(sql, nameof(sql));

        var databaseType = ParseDatabaseType(dbType);

        try
        {
            await using var connection = CreateConnection(connectionString, databaseType);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 30;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var result = new System.Text.StringBuilder();
            var hasRows = false;

            while (await reader.ReadAsync(cancellationToken))
            {
                hasRows = true;
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (i > 0) result.Append(" | ");
                    result.Append(reader.GetValue(i));
                }
                result.AppendLine();
            }

            return hasRows ? result.ToString().Trim() : "No results returned";
        }
        catch (DbException dbEx)
        {
            throw new InvalidOperationException($"{databaseType} error: {dbEx.Message}", dbEx);
        }
    }

    /// <summary>
    /// Creates a PostgreSQL database with PostGIS extensions.
    /// Uses safe, validated inputs to prevent injection attacks.
    /// </summary>
    /// <param name="adminConnectionString">Connection string to postgres database (admin access)</param>
    /// <param name="databaseName">Name of database to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Connection string for the new database</returns>
    public async Task<string> CreatePostGisDatabaseAsync(
        string adminConnectionString,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        CommandArgumentValidator.ValidateConnectionString(adminConnectionString, nameof(adminConnectionString));
        CommandArgumentValidator.ValidateDatabaseName(databaseName, nameof(databaseName));

        try
        {
            // Create database using the admin connection
            var createDbSql = $"CREATE DATABASE {databaseName}";
            await ExecuteDdlAsync(adminConnectionString, createDbSql, "postgres", cancellationToken);

            // Build connection string for new database
            var builder = new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                Database = databaseName
            };
            var newDbConnectionString = builder.ToString();

            // Add PostGIS extensions to the new database
            await ExecuteDdlAsync(newDbConnectionString, "CREATE EXTENSION IF NOT EXISTS postgis", "postgres", cancellationToken);
            await ExecuteDdlAsync(newDbConnectionString, "CREATE EXTENSION IF NOT EXISTS postgis_topology", "postgres", cancellationToken);

            // Verify PostGIS installation
            var version = await ExecuteQueryAsync(newDbConnectionString, "SELECT PostGIS_version()", "postgres", cancellationToken);

            return $"Database '{databaseName}' created successfully. PostGIS version: {version}";
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new InvalidOperationException($"Failed to create PostGIS database: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Tests database connection.
    /// </summary>
    /// <param name="connectionString">Connection string to test</param>
    /// <param name="dbType">Database type (postgres, mysql, sqlserver, sqlite)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connection successful</returns>
    public async Task<bool> TestConnectionAsync(string connectionString, string dbType = "postgres", CancellationToken cancellationToken = default)
    {
        CommandArgumentValidator.ValidateConnectionString(connectionString, nameof(connectionString));

        var databaseType = ParseDatabaseType(dbType);

        try
        {
            await using var connection = CreateConnection(connectionString, databaseType);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

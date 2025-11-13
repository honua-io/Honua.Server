// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Honua.Server.Observability.HealthChecks;

/// <summary>
/// Health check for PostgreSQL database connectivity and status.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly string connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseHealthCheck"/> class.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    public DatabaseHealthCheck(string connectionString)
    {
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Checks the health of the PostgreSQL database connection.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous health check operation.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(this.connectionString);
            await connection.OpenAsync(cancellationToken);

            // Check database version
            var version = await connection.QuerySingleAsync<string>(
                "SELECT version()",
                cancellationToken);

            // Check schema migrations (if table exists)
            var migrationCount = 0;
            var tableExists = await connection.QuerySingleAsync<bool>(
                @"SELECT EXISTS (
                    SELECT FROM information_schema.tables
                    WHERE table_schema = 'public'
                    AND table_name = 'schema_migrations'
                )",
                cancellationToken);

            if (tableExists)
            {
                migrationCount = await connection.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM schema_migrations",
                    cancellationToken);
            }

            // Connection pool status
            // NpgsqlConnection.ClearPool returns void, so we can't capture it
            // Just note that the connection is working

            var data = new Dictionary<string, object>
            {
                { "version", version },
                { "migrations_applied", migrationCount },
                { "connection_state", connection.State.ToString() },
            };

            return HealthCheckResult.Healthy(
                $"Database connected successfully. {migrationCount} migrations applied.",
                data);
        }
        catch (NpgsqlException ex)
        {
            return HealthCheckResult.Unhealthy(
                $"Database connection failed: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    { "error_code", ex.ErrorCode },
                    { "sql_state", ex.SqlState ?? "unknown" },
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Database health check failed",
                ex);
        }
    }
}

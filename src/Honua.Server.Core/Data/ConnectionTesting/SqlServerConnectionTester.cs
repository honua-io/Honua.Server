// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Data.ConnectionTesting;

/// <summary>
/// Connection tester for SQL Server databases.
/// Tests connectivity and retrieves version and spatial type support information.
/// </summary>
public sealed class SqlServerConnectionTester : IConnectionTester
{
    private readonly IConnectionStringEncryptionService? _encryptionService;
    private readonly ILogger<SqlServerConnectionTester> _logger;

    public SqlServerConnectionTester(
        IConnectionStringEncryptionService? encryptionService = null,
        ILogger<SqlServerConnectionTester>? logger = null)
    {
        _encryptionService = encryptionService;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SqlServerConnectionTester>.Instance;
    }

    public string ProviderType => "sqlserver";

    public async Task<ConnectionTestResult> TestConnectionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(dataSource.ConnectionString))
            {
                return new ConnectionTestResult
                {
                    Success = false,
                    Message = "Connection string is empty",
                    ErrorDetails = "Data source configuration is missing a connection string",
                    ErrorType = "configuration",
                    ResponseTime = stopwatch.Elapsed
                };
            }

            // Decrypt connection string if needed
            var connectionString = _encryptionService != null
                ? await _encryptionService.DecryptAsync(dataSource.ConnectionString, cancellationToken).ConfigureAwait(false)
                : dataSource.ConnectionString;

            await using var connection = new SqlConnection(connectionString);

            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Test basic query and get version
            await using var versionCmd = new SqlCommand("SELECT @@VERSION;", connection)
            {
                CommandTimeout = 10
            };
            var version = await versionCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;

            // Check for spatial type support (geography/geometry types)
            bool hasSpatialSupport = false;
            try
            {
                await using var spatialCmd = new SqlCommand(
                    @"SELECT COUNT(*)
                      FROM sys.types
                      WHERE name IN ('geography', 'geometry');",
                    connection)
                {
                    CommandTimeout = 10
                };
                var result = await spatialCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                hasSpatialSupport = result != null && Convert.ToInt32(result) >= 2;
            }
            catch (SqlException ex)
            {
                _logger.LogDebug(ex, "Unable to check spatial support in database {Database}", connection.Database);
            }

            // Get SQL Server edition
            string? edition = null;
            try
            {
                await using var editionCmd = new SqlCommand("SELECT SERVERPROPERTY('Edition');", connection)
                {
                    CommandTimeout = 10
                };
                edition = await editionCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            }
            catch (SqlException ex)
            {
                _logger.LogDebug(ex, "Unable to retrieve SQL Server edition");
            }

            stopwatch.Stop();

            var metadata = new Dictionary<string, object?>
            {
                ["database"] = connection.Database,
                ["server"] = connection.DataSource,
                ["serverVersion"] = connection.ServerVersion,
                ["spatialSupport"] = hasSpatialSupport
            };

            if (version != null)
            {
                metadata["version"] = version.Split('\n')[0]; // First line contains the main version info
            }

            if (edition != null)
            {
                metadata["edition"] = edition;
            }

            return new ConnectionTestResult
            {
                Success = true,
                Message = "Successfully connected to SQL Server database",
                ResponseTime = stopwatch.Elapsed,
                Metadata = metadata
            };
        }
        catch (SqlException ex)
        {
            stopwatch.Stop();

            var errorType = DetermineSqlServerErrorType(ex);
            var errorDetails = FormatSqlServerError(ex);

            _logger.LogWarning(ex,
                "SQL Server connection test failed for data source {DataSourceId}: {ErrorType}",
                dataSource.Id, errorType);

            return new ConnectionTestResult
            {
                Success = false,
                Message = "Failed to connect to SQL Server database",
                ErrorDetails = errorDetails,
                ErrorType = errorType,
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();

            _logger.LogWarning(ex,
                "SQL Server connection test timed out for data source {DataSourceId}",
                dataSource.Id);

            return new ConnectionTestResult
            {
                Success = false,
                Message = "Connection attempt timed out",
                ErrorDetails = "The database server did not respond within the timeout period. Check network connectivity and server availability.",
                ErrorType = "timeout",
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();

            return new ConnectionTestResult
            {
                Success = false,
                Message = "Connection test was cancelled",
                ErrorDetails = "The connection test was cancelled before completion",
                ErrorType = "cancelled",
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "Unexpected error during SQL Server connection test for data source {DataSourceId}",
                dataSource.Id);

            return new ConnectionTestResult
            {
                Success = false,
                Message = "Connection test failed with unexpected error",
                ErrorDetails = ex.Message,
                ErrorType = "unknown",
                ResponseTime = stopwatch.Elapsed
            };
        }
    }

    private static string DetermineSqlServerErrorType(SqlException ex)
    {
        // SQL Server error codes: https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/database-engine-events-and-errors
        return ex.Number switch
        {
            // Authentication errors
            18456 => "authentication", // Login failed
            18470 => "authentication", // Login failed (disabled)
            18486 => "authentication", // Login failed (wrong password)
            18487 => "authentication", // Login failed (password expired)
            18488 => "authentication", // Login failed (password must be changed)

            // Database errors
            4060 => "configuration", // Cannot open database
            911 => "configuration", // Database does not exist

            // Connection/network errors
            -1 => "timeout", // Connection timeout
            -2 => "network", // Network error
            53 => "network", // Named Pipes Provider error (could not open connection)
            10053 => "network", // Transport-level error
            10054 => "network", // Connection forcibly closed
            10060 => "network", // Connection timeout
            10061 => "network", // Connection refused
            64 => "network", // Error from server on connection attempt

            // Server errors
            17809 => "server", // Could not connect (server may be too busy)
            17810 => "server", // Could not connect (server may be too busy)

            _ => "database"
        };
    }

    private static string FormatSqlServerError(SqlException ex)
    {
        return ex.Number switch
        {
            18456 or 18470 or 18486 or 18487 or 18488 =>
                $"Authentication failed: {ex.Message}. Check username and password.",
            4060 or 911 =>
                $"Database does not exist or cannot be accessed: {ex.Message}. Verify the database name and permissions.",
            -1 or 10060 =>
                "Connection timed out. Check network connectivity and ensure the SQL Server service is running.",
            -2 or 53 or 10053 or 10054 or 10061 or 64 =>
                $"Network connection failed: {ex.Message}. Check server name, port, and network connectivity.",
            17809 or 17810 =>
                $"Server too busy: {ex.Message}. The server may be overloaded or unavailable.",
            _ => $"{ex.Message} (Error {ex.Number})"
        };
    }
}

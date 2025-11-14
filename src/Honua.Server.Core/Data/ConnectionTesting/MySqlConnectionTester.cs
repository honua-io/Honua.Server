// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Honua.Server.Core.Data.ConnectionTesting;

/// <summary>
/// Connection tester for MySQL/MariaDB databases.
/// Tests connectivity and retrieves version and spatial extension information.
/// </summary>
public sealed class MySqlConnectionTester : IConnectionTester
{
    private readonly IConnectionStringEncryptionService? _encryptionService;
    private readonly ILogger<MySqlConnectionTester> _logger;

    public MySqlConnectionTester(
        IConnectionStringEncryptionService? encryptionService = null,
        ILogger<MySqlConnectionTester>? logger = null)
    {
        _encryptionService = encryptionService;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MySqlConnectionTester>.Instance;
    }

    public string ProviderType => "mysql";

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
            var connectionString = _encryptionService?.DecryptConnectionString(dataSource.ConnectionString)
                ?? dataSource.ConnectionString;

            await using var connection = new MySqlConnection(connectionString);

            // Set a reasonable timeout for connection testing
            connection.DefaultCommandTimeout = 10;

            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Test basic query and get version
            await using var versionCmd = new MySqlCommand("SELECT VERSION();", connection);
            var version = await versionCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;

            // Check for spatial support (available in MySQL 5.7+ and all MariaDB versions)
            bool hasSpatialSupport = false;
            try
            {
                await using var spatialCmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM information_schema.plugins WHERE PLUGIN_NAME = 'InnoDB' AND PLUGIN_STATUS = 'ACTIVE';",
                    connection);
                var result = await spatialCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                hasSpatialSupport = result != null && Convert.ToInt32(result) > 0;
            }
            catch (MySqlException ex)
            {
                _logger.LogDebug(ex, "Unable to check spatial support in database {Database}", connection.Database);
            }

            stopwatch.Stop();

            var isMariaDb = version?.Contains("MariaDB", StringComparison.OrdinalIgnoreCase) ?? false;

            var metadata = new Dictionary<string, object?>
            {
                ["database"] = connection.Database,
                ["host"] = connection.DataSource,
                ["serverVersion"] = connection.ServerVersion,
                ["isMariaDb"] = isMariaDb,
                ["spatialSupport"] = hasSpatialSupport
            };

            if (version != null)
            {
                metadata["version"] = version;
            }

            return new ConnectionTestResult
            {
                Success = true,
                Message = isMariaDb
                    ? "Successfully connected to MariaDB database"
                    : "Successfully connected to MySQL database",
                ResponseTime = stopwatch.Elapsed,
                Metadata = metadata
            };
        }
        catch (MySqlException ex)
        {
            stopwatch.Stop();

            var errorType = DetermineMySqlErrorType(ex);
            var errorDetails = FormatMySqlError(ex);

            _logger.LogWarning(ex,
                "MySQL connection test failed for data source {DataSourceId}: {ErrorType}",
                dataSource.Id, errorType);

            return new ConnectionTestResult
            {
                Success = false,
                Message = "Failed to connect to MySQL database",
                ErrorDetails = errorDetails,
                ErrorType = errorType,
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();

            _logger.LogWarning(ex,
                "MySQL connection test timed out for data source {DataSourceId}",
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
                "Unexpected error during MySQL connection test for data source {DataSourceId}",
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

    private static string DetermineMySqlErrorType(MySqlException ex)
    {
        // MySQL error codes: https://dev.mysql.com/doc/mysql-errors/8.0/en/server-error-reference.html
        return ex.Number switch
        {
            // Authentication errors
            1045 => "authentication", // ER_ACCESS_DENIED_ERROR
            1044 => "authentication", // ER_DBACCESS_DENIED_ERROR
            1142 => "authentication", // ER_TABLEACCESS_DENIED_ERROR

            // Database/schema errors
            1049 => "configuration", // ER_BAD_DB_ERROR (unknown database)
            1146 => "configuration", // ER_NO_SUCH_TABLE

            // Connection errors
            2002 => "network", // CR_CONNECTION_ERROR (can't connect to MySQL server)
            2003 => "network", // CR_CONN_HOST_ERROR (can't connect to MySQL server on host)
            2013 => "network", // CR_SERVER_LOST (lost connection during query)
            2006 => "network", // CR_SERVER_GONE_ERROR (MySQL server has gone away)

            // Server errors
            1040 => "server", // ER_CON_COUNT_ERROR (too many connections)
            1203 => "server", // ER_TOO_MANY_USER_CONNECTIONS

            _ => "database"
        };
    }

    private static string FormatMySqlError(MySqlException ex)
    {
        return ex.Number switch
        {
            1045 or 1044 or 1142 => $"Authentication failed: {ex.Message}. Check username and password.",
            1049 => $"Database does not exist: {ex.Message}. Verify the database name in the connection string.",
            2002 or 2003 or 2013 or 2006 =>
                $"Network connection failed: {ex.Message}. Check host, port, and network connectivity.",
            1040 or 1203 => $"Too many connections: {ex.Message}. The database has reached its connection limit.",
            _ => $"{ex.Message} (Code: {ex.Number})"
        };
    }
}

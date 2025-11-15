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
using Npgsql;

namespace Honua.Server.Core.Data.ConnectionTesting;

/// <summary>
/// Connection tester for PostgreSQL/PostGIS databases.
/// Tests connectivity and retrieves version and PostGIS extension information.
/// </summary>
public sealed class PostgresConnectionTester : IConnectionTester
{
    private readonly IConnectionStringEncryptionService? _encryptionService;
    private readonly ILogger<PostgresConnectionTester> _logger;

    public PostgresConnectionTester(
        IConnectionStringEncryptionService? encryptionService = null,
        ILogger<PostgresConnectionTester>? logger = null)
    {
        _encryptionService = encryptionService;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PostgresConnectionTester>.Instance;
    }

    public string ProviderType => "postgis";

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

            await using var connection = new NpgsqlConnection(connectionString);

            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Test basic query and get version
            await using var versionCmd = new NpgsqlCommand("SELECT version();", connection);
            var version = await versionCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;

            // Check for PostGIS extension
            string? postgisVersion = null;
            try
            {
                await using var postgisCmd = new NpgsqlCommand(
                    "SELECT PostGIS_Version();",
                    connection);
                postgisVersion = await postgisCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            }
            catch (PostgresException)
            {
                // PostGIS not installed - this is fine, just means no spatial support
                _logger.LogDebug("PostGIS extension not found in database {Database}", connection.Database);
            }

            stopwatch.Stop();

            var metadata = new Dictionary<string, object?>
            {
                ["database"] = connection.Database,
                ["host"] = connection.Host,
                ["port"] = connection.Port,
                ["serverVersion"] = connection.PostgreSqlVersion.ToString(),
                ["postgisInstalled"] = postgisVersion != null
            };

            if (version != null)
            {
                metadata["version"] = version;
            }

            if (postgisVersion != null)
            {
                metadata["postgisVersion"] = postgisVersion;
            }

            return new ConnectionTestResult
            {
                Success = true,
                Message = postgisVersion != null
                    ? "Successfully connected to PostgreSQL database with PostGIS"
                    : "Successfully connected to PostgreSQL database (PostGIS not installed)",
                ResponseTime = stopwatch.Elapsed,
                Metadata = metadata
            };
        }
        catch (PostgresException ex)
        {
            stopwatch.Stop();

            var errorType = DeterminePostgresErrorType(ex);
            var errorDetails = FormatPostgresError(ex);

            _logger.LogWarning(ex,
                "PostgreSQL connection test failed for data source {DataSourceId}: {ErrorType}",
                dataSource.Id, errorType);

            return new ConnectionTestResult
            {
                Success = false,
                Message = "Failed to connect to PostgreSQL database",
                ErrorDetails = errorDetails,
                ErrorType = errorType,
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();

            _logger.LogWarning(ex,
                "PostgreSQL connection test timed out for data source {DataSourceId}",
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
                "Unexpected error during PostgreSQL connection test for data source {DataSourceId}",
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

    private static string DeterminePostgresErrorType(PostgresException ex)
    {
        // PostgreSQL error codes: https://www.postgresql.org/docs/current/errcodes-appendix.html
        return ex.SqlState switch
        {
            // Class 28 - Invalid Authorization Specification
            "28000" => "authentication", // invalid_authorization_specification
            "28P01" => "authentication", // invalid_password

            // Class 3D - Invalid Catalog Name
            "3D000" => "configuration", // invalid_catalog_name (database does not exist)

            // Class 08 - Connection Exception
            "08000" => "network", // connection_exception
            "08003" => "network", // connection_does_not_exist
            "08006" => "network", // connection_failure
            "08001" => "network", // sqlclient_unable_to_establish_sqlconnection
            "08004" => "network", // sqlserver_rejected_establishment_of_sqlconnection

            // Class 53 - Insufficient Resources
            "53300" => "server", // too_many_connections

            _ => "database"
        };
    }

    private static string FormatPostgresError(PostgresException ex)
    {
        return ex.SqlState switch
        {
            "28000" or "28P01" => $"Authentication failed: {ex.MessageText}. Check username and password.",
            "3D000" => $"Database does not exist: {ex.MessageText}. Verify the database name in the connection string.",
            "08000" or "08003" or "08006" or "08001" or "08004" =>
                $"Network connection failed: {ex.MessageText}. Check host, port, and network connectivity.",
            "53300" => $"Too many connections: {ex.MessageText}. The database has reached its connection limit.",
            _ => $"{ex.MessageText} (Code: {ex.SqlState})"
        };
    }
}

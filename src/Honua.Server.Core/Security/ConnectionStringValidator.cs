// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Security;

/// <summary>
/// Validates connection strings to prevent SQL injection and malformed input.
/// Provides basic security checks before connection strings are parsed by database drivers.
/// </summary>
public static class ConnectionStringValidator
{
    /// <summary>
    /// Validates a connection string for common security issues.
    /// </summary>
    /// <param name="connectionString">The connection string to validate.</param>
    /// <param name="providerType">Optional provider type for provider-specific validation.</param>
    /// <exception cref="ArgumentException">Thrown when the connection string is invalid or contains security issues.</exception>
    public static void Validate(string connectionString, string? providerType = null)
    {
        // Basic validation
        if (connectionString.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));
        }

        // Reject obvious SQL injection attempts in connection strings
        // SQL comment indicators should never appear in connection strings
        if (connectionString.Contains(";--", StringComparison.Ordinal))
        {
            throw new ArgumentException("Connection string contains invalid characters (SQL comment)", nameof(connectionString));
        }

        if (connectionString.Contains("/*", StringComparison.Ordinal))
        {
            throw new ArgumentException("Connection string contains invalid characters (SQL comment)", nameof(connectionString));
        }

        // Reject connection strings with suspicious patterns
        // Single quotes are typically not valid in connection strings (double quotes or no quotes are used)
        // Exception: password values may contain single quotes in some providers
        var quotedValuePattern = connectionString.Contains("Password='", StringComparison.OrdinalIgnoreCase) ||
                                 connectionString.Contains("Pwd='", StringComparison.OrdinalIgnoreCase);

        if (connectionString.Contains("'", StringComparison.Ordinal) && !quotedValuePattern)
        {
            throw new ArgumentException("Connection string contains unexpected single quotes", nameof(connectionString));
        }

        // Reject null bytes (common attack vector)
        if (connectionString.Contains('\0'))
        {
            throw new ArgumentException("Connection string contains null bytes", nameof(connectionString));
        }

        // Reject newlines and carriage returns (can be used for header injection)
        if (connectionString.Contains('\n') || connectionString.Contains('\r'))
        {
            throw new ArgumentException("Connection string contains newline characters", nameof(connectionString));
        }

        // Length validation - connection strings should be reasonable
        // Most connection strings are under 1KB; anything larger is suspicious
        const int MaxConnectionStringLength = 4096;
        if (connectionString.Length > MaxConnectionStringLength)
        {
            throw new ArgumentException(
                $"Connection string exceeds maximum length of {MaxConnectionStringLength} characters",
                nameof(connectionString));
        }

        // Provider-specific validation
        if (providerType.HasValue())
        {
            ValidateProviderSpecific(connectionString, providerType);
        }
    }

    /// <summary>
    /// Validates provider-specific connection string requirements.
    /// </summary>
    private static void ValidateProviderSpecific(string connectionString, string providerType)
    {
        switch (providerType.ToLowerInvariant())
        {
            case "postgis":
            case "postgres":
            case "postgresql":
                ValidatePostgresConnectionString(connectionString);
                break;

            case "mysql":
                ValidateMySqlConnectionString(connectionString);
                break;

            case "sqlserver":
            case "mssql":
                ValidateSqlServerConnectionString(connectionString);
                break;

            case "sqlite":
                ValidateSqliteConnectionString(connectionString);
                break;

            // Unknown provider types are allowed but receive no specific validation
        }
    }

    private static void ValidatePostgresConnectionString(string connectionString)
    {
        // PostgreSQL connection strings should contain Host or Server
        var hasHost = connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
                      connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                      connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                      connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

        if (!hasHost && !connectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("PostgreSQL connection string must specify a host", nameof(connectionString));
        }
    }

    private static void ValidateMySqlConnectionString(string connectionString)
    {
        // MySQL connection strings should contain Server or Host
        var hasServer = connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                        connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
                        connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase);

        if (!hasServer && !connectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("MySQL connection string must specify a server", nameof(connectionString));
        }
    }

    private static void ValidateSqlServerConnectionString(string connectionString)
    {
        // SQL Server connection strings should contain Server or Data Source
        var hasServer = connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                        connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase);

        if (!hasServer && !connectionString.Contains("(local)", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("SQL Server connection string must specify a server", nameof(connectionString));
        }
    }

    private static void ValidateSqliteConnectionString(string connectionString)
    {
        // SQLite connection strings should contain Data Source or filename
        var hasDataSource = connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
                           connectionString.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
                           connectionString.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase) ||
                           connectionString.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase) ||
                           connectionString.Contains(":memory:", StringComparison.OrdinalIgnoreCase);

        if (!hasDataSource)
        {
            throw new ArgumentException("SQLite connection string must specify a data source", nameof(connectionString));
        }
    }
}

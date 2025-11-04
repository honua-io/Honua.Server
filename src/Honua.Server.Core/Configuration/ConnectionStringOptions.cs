// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Configuration options for connection strings.
/// </summary>
public sealed class ConnectionStringOptions
{
    public const string SectionName = "ConnectionStrings";

    public string? Redis { get; set; }
    public string? DefaultConnection { get; set; }
    public string? Postgres { get; set; }
    public string? SqlServer { get; set; }
    public string? MySql { get; set; }
}

/// <summary>
/// Validates connection string configuration.
/// </summary>
public sealed class ConnectionStringOptionsValidator : IValidateOptions<ConnectionStringOptions>
{
    public ValidateOptionsResult Validate(string? name, ConnectionStringOptions options)
    {
        var failures = new List<string>();

        // Validate Redis connection string if provided
        if (options.Redis.HasValue())
        {
            if (!IsValidRedisConnectionString(options.Redis))
            {
                failures.Add(
                    $"Redis connection string is invalid. Expected format: 'host:port[,password=xxx][,ssl=true]'. " +
                    $"Example: 'localhost:6379' or 'redis.example.com:6380,password=secret,ssl=true'");
            }
        }

        // Validate PostgreSQL connection string if provided
        if (options.Postgres.HasValue())
        {
            if (!IsValidPostgresConnectionString(options.Postgres))
            {
                failures.Add(
                    $"PostgreSQL connection string is invalid. Expected format includes 'Host', 'Database', 'Username'. " +
                    $"Example: 'Host=localhost;Database=honua;Username=postgres;Password=secret'");
            }
        }

        // Validate SQL Server connection string if provided
        if (options.SqlServer.HasValue())
        {
            if (!IsValidSqlServerConnectionString(options.SqlServer))
            {
                failures.Add(
                    $"SQL Server connection string is invalid. Expected format includes 'Server' and 'Database'. " +
                    $"Example: 'Server=localhost;Database=honua;User Id=sa;Password=secret' or use Integrated Security");
            }
        }

        // Validate MySQL connection string if provided
        if (options.MySql.HasValue())
        {
            if (!IsValidMySqlConnectionString(options.MySql))
            {
                failures.Add(
                    $"MySQL connection string is invalid. Expected format includes 'Server' and 'Database'. " +
                    $"Example: 'Server=localhost;Database=honua;User=root;Password=secret'");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static bool IsValidRedisConnectionString(string connectionString)
    {
        // Basic validation: should contain host:port pattern
        if (connectionString.IsNullOrWhiteSpace())
            return false;

        // Split by comma to get connection parameters
        var parts = connectionString.Split(',');
        if (parts.Length == 0)
            return false;

        // First part should be host:port
        var hostPort = parts[0].Trim();
        if (!hostPort.Contains(':'))
            return false;

        return true;
    }

    private static bool IsValidPostgresConnectionString(string connectionString)
    {
        if (connectionString.IsNullOrWhiteSpace())
            return false;

        // Should contain at least Host and Database
        return connectionString.Contains("Host=", System.StringComparison.OrdinalIgnoreCase) &&
               connectionString.Contains("Database=", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidSqlServerConnectionString(string connectionString)
    {
        if (connectionString.IsNullOrWhiteSpace())
            return false;

        // Should contain at least Server and Database
        return connectionString.Contains("Server=", System.StringComparison.OrdinalIgnoreCase) &&
               connectionString.Contains("Database=", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidMySqlConnectionString(string connectionString)
    {
        if (connectionString.IsNullOrWhiteSpace())
            return false;

        // Should contain at least Server and Database
        return connectionString.Contains("Server=", System.StringComparison.OrdinalIgnoreCase) &&
               connectionString.Contains("Database=", System.StringComparison.OrdinalIgnoreCase);
    }
}

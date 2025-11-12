// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;

namespace Honua.Server.Core.Configuration.V2.Introspection;

/// <summary>
/// Factory for creating appropriate schema readers based on database provider.
/// </summary>
public static class SchemaReaderFactory
{
    /// <summary>
    /// Creates a schema reader for the specified provider.
    /// </summary>
    /// <param name="provider">Database provider (postgresql, sqlite, sqlserver, mysql).</param>
    /// <returns>Schema reader instance.</returns>
    /// <exception cref="NotSupportedException">Thrown if provider is not supported.</exception>
    public static ISchemaReader CreateReader(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "postgresql" or "postgres" or "npgsql" => new PostgreSqlSchemaReader(),
            "sqlite" or "sqlite3" => new SqliteSchemaReader(),
            _ => throw new NotSupportedException($"Database provider '{provider}' is not supported for introspection. Supported providers: postgresql, sqlite")
        };
    }

    /// <summary>
    /// Detects the provider from a connection string.
    /// </summary>
    /// <param name="connectionString">Database connection string.</param>
    /// <returns>Detected provider name, or null if unable to detect.</returns>
    public static string? DetectProvider(string connectionString)
    {
        var lower = connectionString.ToLowerInvariant();

        // SQLite detection
        if (lower.Contains("data source") && (lower.EndsWith(".db") || lower.EndsWith(".sqlite") || lower.EndsWith(".db3") || lower.Contains(":memory:")))
        {
            return "sqlite";
        }

        // PostgreSQL detection
        if (lower.Contains("host=") || lower.Contains("server=") && (lower.Contains("port=5432") || lower.Contains("database=")))
        {
            return "postgresql";
        }

        // SQL Server detection
        if (lower.Contains("server=") && (lower.Contains("database=") || lower.Contains("initial catalog=")))
        {
            return "sqlserver";
        }

        // MySQL detection
        if (lower.Contains("server=") && lower.Contains("port=3306"))
        {
            return "mysql";
        }

        return null;
    }

    /// <summary>
    /// Gets all supported provider names.
    /// </summary>
    public static string[] GetSupportedProviders()
    {
        return new[] { "postgresql", "sqlite" };
    }
}

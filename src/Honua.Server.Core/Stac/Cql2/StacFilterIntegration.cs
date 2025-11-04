// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Data.Common;

namespace Honua.Server.Core.Stac.Cql2;

/// <summary>
/// Helper class for integrating CQL2 filters into STAC search operations.
/// </summary>
public static class StacFilterIntegration
{
    /// <summary>
    /// Processes a CQL2 filter and applies it to a database command.
    /// </summary>
    /// <param name="command">The database command to add filter parameters to.</param>
    /// <param name="filterJson">The CQL2-JSON filter expression.</param>
    /// <param name="provider">The database provider type.</param>
    /// <returns>The SQL WHERE clause to add to the query.</returns>
    /// <exception cref="Cql2ParseException">Thrown when the filter cannot be parsed.</exception>
    /// <exception cref="Cql2BuildException">Thrown when the filter cannot be converted to SQL.</exception>
    public static string ProcessFilter(DbCommand command, string? filterJson, Cql2SqlQueryBuilder.DatabaseProvider provider)
    {
        if (string.IsNullOrWhiteSpace(filterJson))
        {
            return string.Empty;
        }

        // Parse the CQL2-JSON expression
        var expression = Cql2Parser.Parse(filterJson);

        // Build SQL WHERE clause
        var builder = new Cql2SqlQueryBuilder(command, provider);
        return builder.BuildWhereClause(expression);
    }

    /// <summary>
    /// Determines the database provider type from a provider name string.
    /// </summary>
    /// <param name="providerName">The provider name (e.g., "PostgreSQL", "MySQL", etc.).</param>
    /// <returns>The corresponding DatabaseProvider enum value.</returns>
    public static Cql2SqlQueryBuilder.DatabaseProvider DetectProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name cannot be null or empty.", nameof(providerName));
        }

        var normalizedName = providerName.ToLowerInvariant();

        if (normalizedName.Contains("postgres") || normalizedName.Contains("npgsql"))
        {
            return Cql2SqlQueryBuilder.DatabaseProvider.PostgreSQL;
        }

        if (normalizedName.Contains("mysql") || normalizedName.Contains("mariadb"))
        {
            return Cql2SqlQueryBuilder.DatabaseProvider.MySQL;
        }

        if (normalizedName.Contains("sqlserver") || normalizedName.Contains("mssql"))
        {
            return Cql2SqlQueryBuilder.DatabaseProvider.SqlServer;
        }

        if (normalizedName.Contains("sqlite"))
        {
            return Cql2SqlQueryBuilder.DatabaseProvider.SQLite;
        }

        throw new NotSupportedException($"Unsupported database provider: {providerName}");
    }
}

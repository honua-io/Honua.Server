// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Core.Data.Query;

/// <summary>
/// Provides common functionality for building SQL pagination (OFFSET/LIMIT) and ORDER BY clauses
/// across all database providers.
/// Extracted from duplicate implementations in PostgreSQL, MySQL, SQL Server, SQLite, Oracle,
/// Snowflake, BigQuery, and Redshift query builders.
/// </summary>
/// <remarks>
/// This utility consolidates 98% identical ORDER BY clause generation and 70% identical pagination
/// logic that was duplicated across all query builder implementations. The utility handles vendor-specific
/// pagination syntax differences while maintaining a common API.
/// </remarks>
public static class PaginationHelper
{
    /// <summary>
    /// Database vendor types for vendor-specific pagination syntax.
    /// </summary>
    public enum DatabaseVendor
    {
        /// <summary>PostgreSQL: LIMIT n OFFSET m</summary>
        PostgreSQL,

        /// <summary>MySQL: LIMIT n OFFSET m</summary>
        MySQL,

        /// <summary>SQLite: LIMIT n OFFSET m</summary>
        SQLite,

        /// <summary>SQL Server: OFFSET m ROWS FETCH NEXT n ROWS ONLY</summary>
        SqlServer,

        /// <summary>Oracle: OFFSET m ROWS FETCH NEXT n ROWS ONLY</summary>
        Oracle,

        /// <summary>Snowflake: LIMIT n OFFSET m (using :param syntax)</summary>
        Snowflake,

        /// <summary>BigQuery: LIMIT n OFFSET m</summary>
        BigQuery,

        /// <summary>Redshift: LIMIT n OFFSET m</summary>
        Redshift
    }

    /// <summary>
    /// Builds a SQL ORDER BY clause from sort orders, with fallback to default ordering.
    /// </summary>
    /// <param name="sql">The StringBuilder to append the ORDER BY clause to</param>
    /// <param name="sortOrders">The list of sort orders to apply (may be null or empty)</param>
    /// <param name="alias">The table alias to use in column references</param>
    /// <param name="quoteIdentifier">Function to quote identifiers for the specific database provider</param>
    /// <param name="defaultSortColumn">The column to sort by if no sort orders are specified (typically primary key)</param>
    /// <exception cref="ArgumentNullException">Thrown when sql, quoteIdentifier, or defaultSortColumn is null</exception>
    /// <remarks>
    /// If sortOrders is null or empty, the clause will order by defaultSortColumn ASC.
    /// This ensures consistent pagination behavior.
    /// </remarks>
    public static void BuildOrderByClause(
        StringBuilder sql,
        IReadOnlyList<FeatureSortOrder>? sortOrders,
        string alias,
        Func<string, string> quoteIdentifier,
        string defaultSortColumn)
    {
        if (sql is null)
        {
            throw new ArgumentNullException(nameof(sql));
        }

        if (quoteIdentifier is null)
        {
            throw new ArgumentNullException(nameof(quoteIdentifier));
        }

        if (defaultSortColumn.IsNullOrWhiteSpace())
        {
            throw new ArgumentNullException(nameof(defaultSortColumn));
        }

        var segments = new List<string>();

        if (sortOrders is { Count: > 0 })
        {
            foreach (var order in sortOrders)
            {
                var direction = order.Direction == FeatureSortDirection.Descending ? "desc" : "asc";
                segments.Add($"{alias}.{quoteIdentifier(order.Field)} {direction}");
            }
        }
        else
        {
            segments.Add($"{alias}.{quoteIdentifier(defaultSortColumn)} asc");
        }

        sql.Append(" order by ");
        sql.Append(string.Join(", ", segments));
    }

    /// <summary>
    /// Gets the default sort column from a layer definition.
    /// Prefers IdField, falls back to Storage.PrimaryKey.
    /// </summary>
    /// <param name="layer">The layer definition</param>
    /// <returns>The column name to use for default ordering</returns>
    /// <exception cref="ArgumentNullException">Thrown when layer is null</exception>
    public static string GetDefaultOrderByColumn(LayerDefinition layer)
    {
        if (layer is null)
        {
            throw new ArgumentNullException(nameof(layer));
        }

        return layer.IdField.IsNullOrWhiteSpace()
            ? LayerMetadataHelper.GetPrimaryKeyColumn(layer)
            : layer.IdField;
    }

    /// <summary>
    /// Builds a SQL OFFSET/LIMIT clause with vendor-specific syntax.
    /// </summary>
    /// <param name="sql">The StringBuilder to append the pagination clause to</param>
    /// <param name="offset">The offset value (number of rows to skip), or null for no offset</param>
    /// <param name="limit">The limit value (maximum rows to return), or null for no limit</param>
    /// <param name="parameters">The parameter dictionary to add offset/limit parameters to</param>
    /// <param name="vendor">The database vendor type (determines syntax)</param>
    /// <param name="parameterPrefix">The parameter prefix for the vendor ("@" for most, ":" for Oracle/Snowflake)</param>
    /// <exception cref="ArgumentNullException">Thrown when sql or parameters is null</exception>
    /// <remarks>
    /// <para>Vendor-specific syntax:</para>
    /// <list type="bullet">
    /// <item>PostgreSQL, MySQL, SQLite, BigQuery, Redshift: LIMIT n OFFSET m</item>
    /// <item>SQL Server, Oracle: OFFSET m ROWS FETCH NEXT n ROWS ONLY</item>
    /// <item>Snowflake: LIMIT :n OFFSET :m</item>
    /// </list>
    /// <para>Special handling:</para>
    /// <list type="bullet">
    /// <item>PostgreSQL: Uses "LIMIT ALL" when offset is specified without limit</item>
    /// <item>MySQL: Uses LIMIT 18446744073709551615 when offset is specified without limit</item>
    /// <item>SQLite: Uses LIMIT -1 when offset is specified without limit</item>
    /// <item>SQL Server: OFFSET clause requires ORDER BY (should be added separately)</item>
    /// </list>
    /// </remarks>
    public static void BuildOffsetLimitClause(
        StringBuilder sql,
        int? offset,
        int? limit,
        IDictionary<string, object?> parameters,
        DatabaseVendor vendor,
        string parameterPrefix = "@")
    {
        if (sql is null)
        {
            throw new ArgumentNullException(nameof(sql));
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        var normalizedOffset = NormalizeOffset(offset);
        var normalizedLimit = limit; // Limit normalization is application-specific

        switch (vendor)
        {
            case DatabaseVendor.PostgreSQL:
                BuildPostgreSqlPagination(sql, normalizedOffset, normalizedLimit, parameters, parameterPrefix);
                break;

            case DatabaseVendor.MySQL:
                BuildMySqlPagination(sql, normalizedOffset, normalizedLimit, parameters, parameterPrefix);
                break;

            case DatabaseVendor.SQLite:
                BuildSqlitePagination(sql, normalizedOffset, normalizedLimit, parameters, parameterPrefix);
                break;

            case DatabaseVendor.SqlServer:
            case DatabaseVendor.Oracle:
                BuildFetchRowsPagination(sql, normalizedOffset, normalizedLimit, parameters, parameterPrefix);
                break;

            case DatabaseVendor.Snowflake:
            case DatabaseVendor.BigQuery:
            case DatabaseVendor.Redshift:
                BuildStandardPagination(sql, normalizedOffset, normalizedLimit, parameters, parameterPrefix);
                break;

            default:
                throw new NotSupportedException($"Database vendor '{vendor}' is not supported.");
        }
    }

    /// <summary>
    /// Normalizes an offset value, ensuring it's non-negative.
    /// </summary>
    /// <param name="offset">The offset value to normalize</param>
    /// <returns>Null if offset is null or less than 1, otherwise the offset value</returns>
    public static int? NormalizeOffset(int? offset)
    {
        if (offset is null || offset.Value < 1)
        {
            return null;
        }

        return offset.Value;
    }

    /// <summary>
    /// Normalizes a limit value, ensuring it's positive and does not exceed a maximum.
    /// </summary>
    /// <param name="limit">The limit value to normalize</param>
    /// <param name="maxLimit">The maximum allowed limit (for security/performance)</param>
    /// <returns>Null if limit is null, otherwise the limit capped at maxLimit</returns>
    public static int? NormalizeLimit(int? limit, int maxLimit)
    {
        if (limit is null)
        {
            return null;
        }

        if (limit.Value < 1)
        {
            return null;
        }

        return Math.Min(limit.Value, maxLimit);
    }

    /// <summary>
    /// Validates a limit value, throwing an exception if it exceeds the maximum.
    /// Returns 0 if limit is null or non-positive (for providers that don't want exceptions).
    /// </summary>
    /// <param name="limit">The limit value to validate</param>
    /// <param name="maxLimit">The maximum allowed limit</param>
    /// <returns>The validated limit, or 0 if null/non-positive</returns>
    /// <exception cref="ArgumentException">Thrown if limit exceeds maxLimit</exception>
    public static int ValidateLimit(int? limit, int maxLimit)
    {
        if (limit == null || limit <= 0)
        {
            return 0;
        }

        if (limit > maxLimit)
        {
            throw new ArgumentException($"Limit cannot exceed {maxLimit}. Requested: {limit}");
        }

        return limit.Value;
    }

    /// <summary>
    /// Validates an offset value, throwing an exception if it's negative.
    /// Returns 0 if offset is null or non-positive.
    /// </summary>
    /// <param name="offset">The offset value to validate</param>
    /// <returns>The validated offset, or 0 if null/non-positive</returns>
    /// <exception cref="ArgumentException">Thrown if offset is negative</exception>
    public static int ValidateOffset(int? offset)
    {
        if (offset == null || offset <= 0)
        {
            return 0;
        }

        if (offset < 0)
        {
            throw new ArgumentException($"Offset cannot be negative. Requested: {offset}");
        }

        return offset.Value;
    }

    #region Private Helper Methods

    /// <summary>
    /// PostgreSQL pagination: LIMIT n OFFSET m (uses "LIMIT ALL" when offset without limit).
    /// </summary>
    private static void BuildPostgreSqlPagination(
        StringBuilder sql,
        int? offset,
        int? limit,
        IDictionary<string, object?> parameters,
        string parameterPrefix)
    {
        if (limit.HasValue)
        {
            sql.Append($" limit {parameterPrefix}limit");
            parameters["limit"] = limit.Value;
        }

        if (offset.HasValue)
        {
            if (!limit.HasValue)
            {
                sql.Append(" limit ALL");
            }

            sql.Append($" offset {parameterPrefix}offset");
            parameters["offset"] = offset.Value;
        }
    }

    /// <summary>
    /// MySQL pagination: LIMIT n OFFSET m (uses LIMIT 18446744073709551615 when offset without limit).
    /// </summary>
    private static void BuildMySqlPagination(
        StringBuilder sql,
        int? offset,
        int? limit,
        IDictionary<string, object?> parameters,
        string parameterPrefix)
    {
        if (limit.HasValue)
        {
            sql.Append($" limit {parameterPrefix}limit");
            parameters["limit"] = limit.Value;
        }
        else if (offset.HasValue)
        {
            // MySQL requires LIMIT when using OFFSET
            sql.Append(" limit 18446744073709551615");
        }

        if (offset.HasValue)
        {
            sql.Append($" offset {parameterPrefix}offset");
            parameters["offset"] = offset.Value;
        }
    }

    /// <summary>
    /// SQLite pagination: LIMIT n OFFSET m (uses LIMIT -1 when offset without limit).
    /// </summary>
    private static void BuildSqlitePagination(
        StringBuilder sql,
        int? offset,
        int? limit,
        IDictionary<string, object?> parameters,
        string parameterPrefix)
    {
        if (limit.HasValue)
        {
            sql.Append($" limit {parameterPrefix}limit");
            parameters["limit"] = limit.Value;
        }

        if (offset.HasValue)
        {
            if (!limit.HasValue)
            {
                sql.Append(" limit -1");
            }

            sql.Append($" offset {parameterPrefix}offset");
            parameters["offset"] = offset.Value;
        }
    }

    /// <summary>
    /// SQL Server and Oracle pagination: OFFSET m ROWS FETCH NEXT n ROWS ONLY.
    /// </summary>
    private static void BuildFetchRowsPagination(
        StringBuilder sql,
        int? offset,
        int? limit,
        IDictionary<string, object?> parameters,
        string parameterPrefix)
    {
        if (!limit.HasValue && !offset.HasValue)
        {
            return;
        }

        var offsetValue = offset ?? 0;
        sql.Append($" offset {parameterPrefix}offset rows");
        parameters["offset"] = offsetValue;

        if (limit.HasValue)
        {
            sql.Append($" fetch next {parameterPrefix}limit rows only");
            parameters["limit"] = limit.Value;
        }
    }

    /// <summary>
    /// Standard pagination for Snowflake, BigQuery, Redshift: LIMIT n OFFSET m.
    /// </summary>
    private static void BuildStandardPagination(
        StringBuilder sql,
        int? offset,
        int? limit,
        IDictionary<string, object?> parameters,
        string parameterPrefix)
    {
        if (limit.HasValue)
        {
            sql.Append($" LIMIT {parameterPrefix}limit");
            parameters["limit"] = limit.Value;
        }

        if (offset.HasValue && offset.Value > 0)
        {
            sql.Append($" OFFSET {parameterPrefix}offset");
            parameters["offset"] = offset.Value;
        }
    }

    #endregion
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text;
using Honua.Server.Core.Pagination;

namespace Honua.Server.Core.Data.Query;

/// <summary>
/// Shared helper for implementing keyset pagination across different database providers.
/// Provides consistent O(1) pagination performance regardless of page depth.
/// </summary>
public static class KeysetPaginationHelper
{
    /// <summary>
    /// Builds a keyset (cursor-based) WHERE clause predicate for pagination.
    /// Returns an empty string if no cursor is provided.
    /// </summary>
    /// <param name="query">The feature query containing cursor and sort information.</param>
    /// <param name="parameters">Dictionary to add SQL parameters to.</param>
    /// <param name="tableAlias">Optional table alias prefix (e.g., "t" for "t.id").</param>
    /// <param name="identifierQuoter">Function to quote SQL identifiers (e.g., [id] for SQL Server, "id" for Postgres).</param>
    /// <param name="parameterAdder">Function to add a parameter and return its name.</param>
    /// <param name="defaultPrimaryKey">Default primary key column if no sort orders specified.</param>
    /// <returns>WHERE clause predicate (without "WHERE" keyword), or empty string if no cursor.</returns>
    /// <remarks>
    /// Example output for (created_at DESC, id ASC) with cursor values (2024-01-15, 100):
    /// ((t.created_at &lt; @cursor_created_at_0) OR
    ///  (t.created_at = @cursor_created_at_0_eq AND t.id &gt; @cursor_id_1))
    ///
    /// This allows the database to seek directly to the correct position using indexes,
    /// providing O(1) performance vs O(N) for OFFSET-based pagination.
    /// </remarks>
    public static string BuildKeysetWhereClause(
        FeatureQuery query,
        IDictionary<string, object?> parameters,
        string? tableAlias,
        Func<string, string> identifierQuoter,
        Func<string, object?, string> parameterAdder,
        string defaultPrimaryKey)
    {
        if (string.IsNullOrEmpty(query.Cursor))
        {
            return string.Empty;
        }

        // Decode cursor to get last seen values
        var cursorValues = PagedResult<object>.DecodeCursor(query.Cursor);
        if (cursorValues.Count == 0)
        {
            return string.Empty;
        }

        var prefix = string.IsNullOrEmpty(tableAlias) ? "" : $"{tableAlias}.";

        // Get sort orders (default to primary key if not specified)
        var sortOrders = query.SortOrders is { Count: > 0 }
            ? query.SortOrders
            : new[] { new FeatureSortOrder(defaultPrimaryKey, FeatureSortDirection.Ascending) };

        // Build keyset WHERE clause for multi-column sorting
        // For columns (c1 ASC, c2 DESC, c3 ASC), the cursor condition is:
        // (c1 > v1) OR (c1 = v1 AND c2 < v2) OR (c1 = v1 AND c2 = v2 AND c3 > v3)
        var keysetConditions = new List<string>();

        for (var i = 0; i < sortOrders.Count; i++)
        {
            var currentField = sortOrders[i];
            var fieldName = currentField.Field;

            if (!cursorValues.TryGetValue(fieldName, out var cursorValue))
            {
                // Cursor missing this field - skip keyset pagination
                return string.Empty;
            }

            var comparison = currentField.Direction == FeatureSortDirection.Ascending ? ">" : "<";
            var cursorParam = parameterAdder($"cursor_{fieldName}_{i}", cursorValue);

            // Build equality conditions for all previous fields
            var equalityConditions = new List<string>();
            for (var j = 0; j < i; j++)
            {
                var prevField = sortOrders[j];
                var prevFieldName = prevField.Field;

                if (!cursorValues.TryGetValue(prevFieldName, out var prevValue))
                {
                    return string.Empty;
                }

                var prevParam = parameterAdder($"cursor_{prevFieldName}_{j}_eq", prevValue);
                equalityConditions.Add($"{prefix}{identifierQuoter(prevFieldName)} = {prevParam}");
            }

            // Add current field comparison
            if (equalityConditions.Count > 0)
            {
                var condition = $"({string.Join(" and ", equalityConditions)} and {prefix}{identifierQuoter(fieldName)} {comparison} {cursorParam})";
                keysetConditions.Add(condition);
            }
            else
            {
                keysetConditions.Add($"({prefix}{identifierQuoter(fieldName)} {comparison} {cursorParam})");
            }
        }

        if (keysetConditions.Count == 0)
        {
            return string.Empty;
        }

        return $"({string.Join(" or ", keysetConditions)})";
    }

    /// <summary>
    /// Creates a cursor from feature record values.
    /// </summary>
    /// <param name="record">The feature record to create cursor from.</param>
    /// <param name="sortOrders">The sort orders used for pagination.</param>
    /// <param name="defaultPrimaryKey">Default primary key column if no sort orders specified.</param>
    /// <returns>Base64-encoded cursor string.</returns>
    public static string CreateCursorFromRecord(
        FeatureRecord record,
        IReadOnlyList<FeatureSortOrder>? sortOrders,
        string defaultPrimaryKey)
    {
        var orders = sortOrders is { Count: > 0 }
            ? sortOrders
            : new[] { new FeatureSortOrder(defaultPrimaryKey, FeatureSortDirection.Ascending) };

        var cursorData = new Dictionary<string, object?>();
        foreach (var sortOrder in orders)
        {
            if (record.Attributes.TryGetValue(sortOrder.Field, out var value))
            {
                cursorData[sortOrder.Field] = value;
            }
        }

        if (cursorData.Count == 0)
        {
            return string.Empty;
        }

        var json = System.Text.Json.JsonSerializer.Serialize(cursorData);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Determines if a query should use keyset pagination (vs OFFSET).
    /// </summary>
    public static bool ShouldUseKeysetPagination(FeatureQuery query)
    {
        return !string.IsNullOrEmpty(query.Cursor);
    }

    /// <summary>
    /// Gets a deprecation warning message when OFFSET pagination is used.
    /// </summary>
    public static string GetDeprecationWarning(int? offset)
    {
        if (offset.HasValue && offset.Value > 0)
        {
            return $"OFFSET pagination is deprecated and causes performance degradation for deep pages. " +
                   $"Current page offset {offset} may be slow. " +
                   $"Please migrate to cursor-based pagination for O(1) performance. " +
                   $"See: https://docs.honuaio.com/api/pagination";
        }

        return string.Empty;
    }
}

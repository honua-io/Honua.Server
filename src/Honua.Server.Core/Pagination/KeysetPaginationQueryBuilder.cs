// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;

namespace Honua.Server.Core.Pagination;

/// <summary>
/// Builds SQL WHERE clauses for keyset pagination to achieve constant-time performance.
/// Supports multiple sort columns with mixed ascending/descending directions.
/// </summary>
public static class KeysetPaginationQueryBuilder
{
    /// <summary>
    /// Builds a WHERE clause for keyset pagination that replaces OFFSET-based pagination.
    /// This provides O(1) performance regardless of page depth, whereas OFFSET is O(N).
    /// </summary>
    /// <param name="command">The database command to add parameters to.</param>
    /// <param name="options">The keyset pagination options including cursor and sort fields.</param>
    /// <param name="tableAlias">Optional table alias prefix for column names (e.g., "t." for "t.id").</param>
    /// <returns>SQL WHERE clause (without the "WHERE" keyword), or empty string if no cursor.</returns>
    /// <remarks>
    /// Example transformations:
    ///
    /// Single column (id):
    ///   FROM: WHERE ... ORDER BY id LIMIT 100 OFFSET 10000
    ///   TO:   WHERE ... AND id &gt; @cursor_id ORDER BY id LIMIT 100
    ///   Performance: 100x faster for page 100, 1000x faster for page 1000
    ///
    /// Multiple columns (created_at DESC, id ASC):
    ///   FROM: WHERE ... ORDER BY created_at DESC, id ASC LIMIT 100 OFFSET 10000
    ///   TO:   WHERE ... AND (created_at &lt; @cursor_created_at OR
    ///                        (created_at = @cursor_created_at AND id &gt; @cursor_id))
    ///         ORDER BY created_at DESC, id ASC LIMIT 100
    ///
    /// This uses indexed columns for filtering, enabling the database to seek directly to
    /// the correct position rather than scanning all previous rows.
    /// </remarks>
    public static string BuildWhereClause(
        DbCommand command,
        KeysetPaginationOptions options,
        string? tableAlias = null)
    {
        if (string.IsNullOrEmpty(options.Cursor) || options.SortFields.Count == 0)
        {
            return string.Empty;
        }

        var cursorValues = PagedResult<object>.DecodeCursor(options.Cursor);
        if (cursorValues.Count == 0)
        {
            return string.Empty;
        }

        var prefix = string.IsNullOrEmpty(tableAlias) ? "" : $"{tableAlias}.";
        var conditions = new List<string>();

        // Build a compound condition for multi-column keyset pagination
        // For columns (c1 ASC, c2 DESC, c3 ASC), the cursor condition is:
        // (c1 > v1) OR (c1 = v1 AND c2 < v2) OR (c1 = v1 AND c2 = v2 AND c3 > v3)

        for (var i = 0; i < options.SortFields.Count; i++)
        {
            var currentField = options.SortFields[i];

            if (!cursorValues.TryGetValue(currentField.FieldName, out var cursorValue))
            {
                // Cursor missing this field - skip pagination
                return string.Empty;
            }

            var paramName = $"@cursor_{currentField.FieldName}_{i}";
            var paramNameEq = $"@cursor_{currentField.FieldName}_{i}_eq";

            // Determine comparison operator based on direction and pagination direction
            var comparisonOp = GetComparisonOperator(currentField.Direction, options.Direction);

            // Build equality conditions for all previous fields
            var equalityConditions = new List<string>();
            for (var j = 0; j < i; j++)
            {
                var prevField = options.SortFields[j];
                if (!cursorValues.TryGetValue(prevField.FieldName, out var prevValue))
                {
                    return string.Empty;
                }

                var prevParamName = $"@cursor_{prevField.FieldName}_{j}_eq";
                AddParameter(command, prevParamName, prevValue);
                equalityConditions.Add($"{prefix}{prevField.FieldName} = {prevParamName}");
            }

            // Add current field comparison
            AddParameter(command, paramName, cursorValue);

            if (equalityConditions.Count > 0)
            {
                // For non-first columns: (prev1 = val1 AND prev2 = val2 AND ... AND currentCol > currentVal)
                var condition = $"({string.Join(" AND ", equalityConditions)} AND {prefix}{currentField.FieldName} {comparisonOp} {paramName})";
                conditions.Add(condition);
            }
            else
            {
                // For first column: (col1 > val1)
                conditions.Add($"({prefix}{currentField.FieldName} {comparisonOp} {paramName})");
            }
        }

        // Combine all conditions with OR
        return string.Join(" OR ", conditions);
    }

    /// <summary>
    /// Builds the ORDER BY clause from sort fields.
    /// </summary>
    public static string BuildOrderByClause(
        IReadOnlyList<KeysetSortField> sortFields,
        string? tableAlias = null)
    {
        if (sortFields.Count == 0)
        {
            return string.Empty;
        }

        var prefix = string.IsNullOrEmpty(tableAlias) ? "" : $"{tableAlias}.";
        var orderByClauses = sortFields.Select(field =>
        {
            var direction = field.Direction == KeysetSortDirection.Ascending ? "ASC" : "DESC";
            return $"{prefix}{field.FieldName} {direction}";
        });

        return $"ORDER BY {string.Join(", ", orderByClauses)}";
    }

    /// <summary>
    /// Validates that sort fields include a unique identifier for stable pagination.
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateSortFields(IReadOnlyList<KeysetSortField> sortFields)
    {
        if (sortFields.Count == 0)
        {
            return (false, "At least one sort field is required");
        }

        var hasUniqueField = sortFields.Any(f => f.IsUnique);
        if (!hasUniqueField)
        {
            return (false, "Sort fields must include at least one unique identifier (e.g., 'id') to ensure stable pagination");
        }

        // Check for duplicate field names
        var fieldNames = sortFields.Select(f => f.FieldName).ToList();
        if (fieldNames.Count != fieldNames.Distinct().Count())
        {
            return (false, "Sort fields contain duplicate field names");
        }

        return (true, null);
    }

    /// <summary>
    /// Creates default sort fields for a table with common patterns.
    /// </summary>
    public static IReadOnlyList<KeysetSortField> CreateDefaultSortFields(params (string FieldName, KeysetSortDirection Direction, bool IsUnique)[] fields)
    {
        return fields.Select(f => new KeysetSortField
        {
            FieldName = f.FieldName,
            Direction = f.Direction,
            IsUnique = f.IsUnique
        }).ToList();
    }

    private static string GetComparisonOperator(KeysetSortDirection sortDirection, KeysetDirection paginationDirection)
    {
        // For forward pagination:
        //   - ASC columns use >
        //   - DESC columns use <
        // For backward pagination (reverse):
        //   - ASC columns use <
        //   - DESC columns use >

        if (paginationDirection == KeysetDirection.Forward)
        {
            return sortDirection == KeysetSortDirection.Ascending ? ">" : "<";
        }
        else
        {
            return sortDirection == KeysetSortDirection.Ascending ? "<" : ">";
        }
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}

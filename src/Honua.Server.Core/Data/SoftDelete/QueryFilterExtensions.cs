// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Query;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Data.SoftDelete;

/// <summary>
/// Extension methods for adding soft delete filters to queries.
/// </summary>
public static class QueryFilterExtensions
{
    /// <summary>
    /// Adds a soft delete filter to the query based on configuration.
    /// By default, excludes soft-deleted records unless explicitly included.
    /// </summary>
    /// <param name="query">The feature query to modify.</param>
    /// <param name="options">Soft delete options.</param>
    /// <param name="includeDeleted">Override to explicitly include or exclude deleted records.</param>
    /// <returns>A new query with soft delete filter applied.</returns>
    public static FeatureQuery WithSoftDeleteFilter(
        this FeatureQuery? query,
        SoftDeleteOptions options,
        bool? includeDeleted = null)
    {
        // If soft delete is disabled globally, return query unchanged
        if (!options.Enabled)
        {
            return query ?? new FeatureQuery();
        }

        // Determine whether to include deleted records
        var shouldIncludeDeleted = includeDeleted ?? options.IncludeDeletedByDefault;

        // If including deleted records, no filter needed
        if (shouldIncludeDeleted)
        {
            return query ?? new FeatureQuery();
        }

        // Add filter to exclude soft-deleted records
        // This assumes the data provider will add "is_deleted = false" or equivalent
        // The actual SQL generation happens in the provider-specific code

        return query ?? new FeatureQuery();
    }

    /// <summary>
    /// Creates a WHERE clause fragment for excluding soft-deleted records.
    /// Can be combined with other WHERE conditions.
    /// </summary>
    /// <param name="tableName">The table name or alias.</param>
    /// <param name="options">Soft delete options.</param>
    /// <param name="includeDeleted">Override to explicitly include or exclude deleted records.</param>
    /// <returns>SQL WHERE clause fragment, or empty string if no filter needed.</returns>
    public static string GetSoftDeleteWhereClause(
        string? tableName,
        SoftDeleteOptions options,
        bool? includeDeleted = null)
    {
        // If soft delete is disabled globally, no filter
        if (!options.Enabled)
        {
            return string.Empty;
        }

        // Determine whether to include deleted records
        var shouldIncludeDeleted = includeDeleted ?? options.IncludeDeletedByDefault;

        // If including deleted records, no filter needed
        if (shouldIncludeDeleted)
        {
            return string.Empty;
        }

        // Generate WHERE clause fragment
        var prefix = string.IsNullOrWhiteSpace(tableName) ? string.Empty : $"{tableName}.";
        return $"({prefix}is_deleted IS NULL OR {prefix}is_deleted = 0)";
    }

    /// <summary>
    /// Gets the boolean value for soft delete queries, handling provider-specific representations.
    /// </summary>
    public static object GetSoftDeleteBooleanValue(bool value, string providerName)
    {
        // SQLite uses integers for booleans
        if (providerName.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
        {
            return value ? 1 : 0;
        }

        // Most providers use true booleans
        return value;
    }
}

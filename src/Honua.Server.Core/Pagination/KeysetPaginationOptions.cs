// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Core.Pagination;

/// <summary>
/// Options for keyset (cursor-based) pagination that provides constant-time performance regardless of page depth.
/// Unlike OFFSET pagination which scans all previous rows, keyset pagination uses a WHERE clause based on
/// the last seen value, resulting in consistent O(1) performance for all pages.
/// </summary>
/// <remarks>
/// Performance characteristics:
/// - OFFSET pagination: Page N takes O(N) time due to scanning N*pageSize rows
/// - Keyset pagination: All pages take O(1) time using indexed WHERE clauses
///
/// Example: For a dataset with 1M records and page size 100:
/// - OFFSET page 1: ~10ms
/// - OFFSET page 100: ~1000ms (100x slower)
/// - OFFSET page 1000: ~10000ms (1000x slower)
/// - Keyset page 1: ~10ms
/// - Keyset page 100: ~10ms (constant time!)
/// - Keyset page 1000: ~10ms (constant time!)
/// </remarks>
public sealed class KeysetPaginationOptions
{
    /// <summary>
    /// The cursor representing the last seen record from the previous page.
    /// Format is typically base64-encoded JSON containing sort column values and unique identifier.
    /// Example: eyJpZCI6MTAwLCJuYW1lIjoiSm9obiJ9 (decoded: {"id":100,"name":"John"})
    /// </summary>
    public string? Cursor { get; init; }

    /// <summary>
    /// Maximum number of records to return per page. Default is 100.
    /// Should be between 1 and 10000 to prevent excessive memory usage.
    /// </summary>
    public int Limit { get; init; } = 100;

    /// <summary>
    /// Sort fields to order results by. Must include at least one field, and the last field
    /// should be a unique identifier (like "id") to ensure stable pagination.
    /// </summary>
    /// <remarks>
    /// Examples:
    /// - Single field: [("id", Ascending)]
    /// - Multiple fields: [("created_at", Descending), ("id", Ascending)]
    /// - Complex: [("priority", Descending), ("name", Ascending), ("id", Ascending)]
    ///
    /// The final field MUST be unique to prevent items being skipped or duplicated when
    /// records have identical values in earlier sort fields.
    /// </remarks>
    public IReadOnlyList<KeysetSortField> SortFields { get; init; } = Array.Empty<KeysetSortField>();

    /// <summary>
    /// Direction for pagination. Forward is default, Backward enables previous-page navigation.
    /// </summary>
    public KeysetDirection Direction { get; init; } = KeysetDirection.Forward;

    /// <summary>
    /// Validates the pagination options and returns any validation errors.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Limit < 1)
        {
            errors.Add("Limit must be at least 1");
        }
        else if (Limit > 10000)
        {
            errors.Add("Limit must not exceed 10000");
        }

        if (SortFields.Count == 0)
        {
            errors.Add("At least one sort field is required for keyset pagination");
        }

        // Validate cursor format if present
        if (!string.IsNullOrEmpty(Cursor))
        {
            try
            {
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Cursor));
                if (string.IsNullOrWhiteSpace(decoded))
                {
                    errors.Add("Cursor is not a valid base64-encoded string");
                }
            }
            catch (FormatException)
            {
                errors.Add("Cursor is not a valid base64-encoded string");
            }
        }

        return errors;
    }
}

/// <summary>
/// Represents a field to sort by in keyset pagination.
/// </summary>
public sealed record KeysetSortField
{
    /// <summary>
    /// The field name to sort by (e.g., "id", "created_at", "name").
    /// </summary>
    public required string FieldName { get; init; }

    /// <summary>
    /// The sort direction (Ascending or Descending).
    /// </summary>
    public KeysetSortDirection Direction { get; init; } = KeysetSortDirection.Ascending;

    /// <summary>
    /// Whether this field is the unique identifier for stable pagination.
    /// At least one field in the sort list must be marked as unique.
    /// </summary>
    public bool IsUnique { get; init; }
}

/// <summary>
/// Sort direction for keyset pagination fields.
/// </summary>
public enum KeysetSortDirection
{
    Ascending,
    Descending
}

/// <summary>
/// Direction for keyset pagination (forward or backward through results).
/// </summary>
public enum KeysetDirection
{
    /// <summary>
    /// Navigate forward through results (next page).
    /// </summary>
    Forward,

    /// <summary>
    /// Navigate backward through results (previous page).
    /// </summary>
    Backward
}

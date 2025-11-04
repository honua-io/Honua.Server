// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Honua.Server.Core.Pagination;

/// <summary>
/// Represents a paginated result set using keyset (cursor-based) pagination.
/// Provides constant-time performance for all pages regardless of depth.
/// </summary>
/// <typeparam name="T">The type of items in the result set.</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>
    /// The items in this page of results.
    /// </summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>
    /// The cursor for fetching the next page of results.
    /// Null if this is the last page.
    /// This is an opaque base64-encoded cursor that should be passed as-is to the next request.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// The cursor for fetching the previous page of results.
    /// Null if this is the first page.
    /// This is an opaque base64-encoded cursor that should be passed as-is to the previous request.
    /// </summary>
    public string? PreviousCursor { get; init; }

    /// <summary>
    /// Total number of items matching the query (optional, may be -1 if counting is disabled).
    /// Note: For very large datasets, getting an exact count can be expensive.
    /// Consider using -1 to skip counting and rely on cursor-based navigation.
    /// </summary>
    public long TotalCount { get; init; } = -1;

    /// <summary>
    /// Whether there are more results after this page.
    /// </summary>
    public bool HasNextPage => NextCursor != null;

    /// <summary>
    /// Whether there are results before this page.
    /// </summary>
    public bool HasPreviousPage => PreviousCursor != null;

    /// <summary>
    /// Number of items in this page.
    /// </summary>
    public int Count => Items.Count;

    /// <summary>
    /// Creates a cursor from the last item in the result set for forward pagination.
    /// </summary>
    /// <param name="lastItem">The last item in the current page.</param>
    /// <param name="sortFields">The sort fields used for pagination.</param>
    /// <param name="valueExtractor">Function to extract field values from items.</param>
    /// <returns>Base64-encoded cursor string.</returns>
    public static string CreateCursor(T lastItem, IReadOnlyList<KeysetSortField> sortFields, Func<T, string, object?> valueExtractor)
    {
        var cursorData = new Dictionary<string, object?>();

        foreach (var sortField in sortFields)
        {
            var value = valueExtractor(lastItem, sortField.FieldName);
            cursorData[sortField.FieldName] = value;
        }

        var json = JsonSerializer.Serialize(cursorData);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Decodes a cursor into its constituent field values.
    /// </summary>
    /// <param name="cursor">The base64-encoded cursor string.</param>
    /// <returns>Dictionary of field names to values.</returns>
    public static IReadOnlyDictionary<string, object?> DecodeCursor(string cursor)
    {
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

            if (values == null)
            {
                return new Dictionary<string, object?>();
            }

            // Convert JsonElement to appropriate types
            var result = new Dictionary<string, object?>();
            foreach (var (key, jsonElement) in values)
            {
                result[key] = ConvertJsonElement(jsonElement);
            }

            return result;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            // Invalid cursor - return empty dictionary
            return new Dictionary<string, object?>();
        }
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }
}

/// <summary>
/// Extension methods for building PagedResult instances.
/// </summary>
public static class PagedResultExtensions
{
    /// <summary>
    /// Creates a PagedResult from a list of items and pagination context.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="items">The items for this page.</param>
    /// <param name="hasMore">Whether there are more items after this page.</param>
    /// <param name="hasPrevious">Whether there are items before this page.</param>
    /// <param name="sortFields">Sort fields used for pagination.</param>
    /// <param name="valueExtractor">Function to extract field values from items.</param>
    /// <param name="totalCount">Total count (optional, -1 if unknown).</param>
    /// <returns>A PagedResult instance.</returns>
    public static PagedResult<T> ToPagedResult<T>(
        this IReadOnlyList<T> items,
        bool hasMore,
        bool hasPrevious,
        IReadOnlyList<KeysetSortField> sortFields,
        Func<T, string, object?> valueExtractor,
        long totalCount = -1)
    {
        string? nextCursor = null;
        string? previousCursor = null;

        if (hasMore && items.Count > 0)
        {
            nextCursor = PagedResult<T>.CreateCursor(items[items.Count - 1], sortFields, valueExtractor);
        }

        if (hasPrevious && items.Count > 0)
        {
            previousCursor = PagedResult<T>.CreateCursor(items[0], sortFields, valueExtractor);
        }

        return new PagedResult<T>
        {
            Items = items,
            NextCursor = nextCursor,
            PreviousCursor = previousCursor,
            TotalCount = totalCount
        };
    }
}

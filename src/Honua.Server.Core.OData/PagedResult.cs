// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.OData;

/// <summary>
/// Represents a paged collection of items with optional total count.
/// Used by OData query results to support pagination and $count.
/// </summary>
/// <typeparam name="T">The type of items in the collection</typeparam>
public sealed record PagedResult<T>
{
    /// <summary>
    /// The items in this page of results.
    /// </summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>
    /// The total count of items across all pages (optional).
    /// Only populated when $count=true is requested.
    /// </summary>
    public long? TotalCount { get; init; }

    /// <summary>
    /// Creates a paged result with just items (no count).
    /// </summary>
    public static PagedResult<T> Create(IReadOnlyList<T> items) =>
        new() { Items = items, TotalCount = null };

    /// <summary>
    /// Creates a paged result with items and total count.
    /// </summary>
    public static PagedResult<T> Create(IReadOnlyList<T> items, long totalCount) =>
        new() { Items = items, TotalCount = totalCount };
}

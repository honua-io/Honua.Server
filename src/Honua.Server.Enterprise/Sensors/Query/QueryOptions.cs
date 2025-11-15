// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Enterprise.Sensors.Query;

/// <summary>
/// Options for querying SensorThings entities.
/// Maps to OData query parameters like $filter, $expand, $orderby, etc.
/// </summary>
public sealed record QueryOptions
{
    /// <summary>
    /// Filter expression to apply ($filter).
    /// </summary>
    public FilterExpression? Filter { get; init; }

    /// <summary>
    /// Collection of order by clauses ($orderby).
    /// </summary>
    public IReadOnlyList<OrderBy>? OrderBy { get; init; }

    /// <summary>
    /// Maximum number of entities to return ($top).
    /// Default: 100
    /// </summary>
    public int? Top { get; init; } = 100;

    /// <summary>
    /// Number of entities to skip ($skip).
    /// Used for pagination.
    /// NOTE: For large offsets (>1000), use Cursor instead for better performance.
    /// </summary>
    public int? Skip { get; init; } = 0;

    /// <summary>
    /// Cursor for pagination (phenomenon_time in ISO 8601 format).
    /// More efficient than Skip for large datasets.
    /// Format: ISO 8601 timestamp (e.g., "2024-11-14T10:30:00Z")
    /// </summary>
    public string? Cursor { get; init; }

    /// <summary>
    /// Whether to include the total count in the response ($count).
    /// </summary>
    public bool Count { get; init; } = false;

    /// <summary>
    /// Navigation properties to expand ($expand).
    /// </summary>
    public ExpandOptions? Expand { get; init; }

    /// <summary>
    /// Properties to select ($select).
    /// If null, all properties are returned.
    /// </summary>
    public IReadOnlyList<string>? Select { get; init; }
}

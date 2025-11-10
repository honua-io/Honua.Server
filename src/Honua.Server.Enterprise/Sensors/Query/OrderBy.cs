// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Enterprise.Sensors.Query;

/// <summary>
/// Represents an ORDER BY clause for sorting query results.
/// </summary>
public sealed record OrderBy
{
    /// <summary>
    /// The property name to sort by.
    /// Example: "phenomenonTime", "name", "id"
    /// </summary>
    public string Property { get; init; } = default!;

    /// <summary>
    /// The sort direction.
    /// </summary>
    public SortDirection Direction { get; init; } = SortDirection.Ascending;
}

/// <summary>
/// Sort direction for ORDER BY clauses.
/// </summary>
public enum SortDirection
{
    /// <summary>
    /// Ascending order (A-Z, 0-9, oldest to newest).
    /// </summary>
    Ascending,

    /// <summary>
    /// Descending order (Z-A, 9-0, newest to oldest).
    /// </summary>
    Descending
}

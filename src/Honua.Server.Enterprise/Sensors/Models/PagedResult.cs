// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json.Serialization;

namespace Honua.Server.Enterprise.Sensors.Models;

/// <summary>
/// Represents a paged collection of entities returned from the SensorThings API.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public sealed record PagedResult<T>
{
    /// <summary>
    /// The OData context URL for this collection.
    /// </summary>
    [JsonPropertyName("@odata.context")]
    public string? Context { get; init; }

    /// <summary>
    /// The total count of entities (when $count=true is requested).
    /// </summary>
    [JsonPropertyName("@odata.count")]
    public long? TotalCount { get; init; }

    /// <summary>
    /// The URL to retrieve the next page of results.
    /// </summary>
    [JsonPropertyName("@odata.nextLink")]
    public string? NextLink { get; init; }

    /// <summary>
    /// The collection of entities in this page.
    /// </summary>
    [JsonPropertyName("value")]
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
}

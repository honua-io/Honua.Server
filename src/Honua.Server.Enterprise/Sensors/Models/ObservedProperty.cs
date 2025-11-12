// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Enterprise.Sensors.Models;

/// <summary>
/// Represents an ObservedProperty entity in the OGC SensorThings API.
/// An ObservedProperty specifies the phenomenon of an Observation.
/// </summary>
public sealed record ObservedProperty
{
    /// <summary>
    /// Unique identifier for the ObservedProperty.
    /// </summary>
    public string Id { get; init; } = default!;

    /// <summary>
    /// A self link to this entity.
    /// </summary>
    public string? SelfLink { get; init; }

    /// <summary>
    /// A property provides a label for ObservedProperty entity.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// The description of the ObservedProperty entity.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// The URI of the ObservedProperty. Dereferencing this URI should result in a representation of
    /// the definition of the ObservedProperty.
    /// </summary>
    public string Definition { get; init; } = default!;

    /// <summary>
    /// A JSON Object containing user-annotated properties as key-value pairs.
    /// </summary>
    public Dictionary<string, object>? Properties { get; init; }

    // Navigation properties

    /// <summary>
    /// The Datastreams associated with this ObservedProperty.
    /// </summary>
    public IReadOnlyList<Datastream>? Datastreams { get; init; }

    // Audit fields

    /// <summary>
    /// Timestamp when this ObservedProperty was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when this ObservedProperty was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}

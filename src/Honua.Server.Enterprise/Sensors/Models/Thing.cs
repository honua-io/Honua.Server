// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Enterprise.Sensors.Models;

/// <summary>
/// Represents a Thing entity in the OGC SensorThings API.
/// A Thing is an object of the physical world (physical things) or the information world
/// (virtual things) that is capable of being identified and integrated into communication networks.
/// </summary>
public sealed record Thing
{
    /// <summary>
    /// Unique identifier for the Thing.
    /// </summary>
    public string Id { get; init; } = default!;

    /// <summary>
    /// A self link to this entity.
    /// </summary>
    public string? SelfLink { get; init; }

    /// <summary>
    /// A property provides a label for Thing entity, commonly a descriptive name.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// The description of the Thing entity.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// A JSON Object containing user-annotated properties as key-value pairs.
    /// </summary>
    public Dictionary<string, object>? Properties { get; init; }

    // Navigation properties

    /// <summary>
    /// The Locations entity locates the Thing.
    /// </summary>
    public IReadOnlyList<Location>? Locations { get; init; }

    /// <summary>
    /// The HistoricalLocations of the Thing.
    /// </summary>
    public IReadOnlyList<HistoricalLocation>? HistoricalLocations { get; init; }

    /// <summary>
    /// The Datastreams associated with this Thing.
    /// </summary>
    public IReadOnlyList<Datastream>? Datastreams { get; init; }

    // Audit fields

    /// <summary>
    /// Timestamp when this Thing was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when this Thing was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}

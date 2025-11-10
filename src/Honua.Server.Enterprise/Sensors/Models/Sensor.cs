// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Enterprise.Sensors.Models;

/// <summary>
/// Represents a Sensor entity in the OGC SensorThings API.
/// A Sensor is an instrument that observes a property or phenomenon with the goal of producing an estimate
/// of the value of the property.
/// </summary>
public sealed record Sensor
{
    /// <summary>
    /// Unique identifier for the Sensor.
    /// </summary>
    public string Id { get; init; } = default!;

    /// <summary>
    /// A self link to this entity.
    /// </summary>
    public string? SelfLink { get; init; }

    /// <summary>
    /// A property provides a label for Sensor entity.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// The description of the Sensor entity.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// The encoding type of the metadata property.
    /// Common values: "application/pdf", "http://www.opengis.net/doc/IS/SensorML/2.0"
    /// </summary>
    public string EncodingType { get; init; } = default!;

    /// <summary>
    /// The detailed description of the Sensor or system. The metadata type is defined by encodingType.
    /// </summary>
    public string Metadata { get; init; } = default!;

    /// <summary>
    /// A JSON Object containing user-annotated properties as key-value pairs.
    /// </summary>
    public Dictionary<string, object>? Properties { get; init; }

    // Navigation properties

    /// <summary>
    /// The Datastreams associated with this Sensor.
    /// </summary>
    public IReadOnlyList<Datastream>? Datastreams { get; init; }

    // Audit fields

    /// <summary>
    /// Timestamp when this Sensor was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when this Sensor was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}

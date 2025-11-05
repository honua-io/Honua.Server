using NetTopologySuite.Geometries;

namespace Honua.Server.Enterprise.Sensors.Models;

/// <summary>
/// Represents a Location entity in the OGC SensorThings API.
/// The Location entity locates the Thing or the Things it associated with.
/// </summary>
public sealed record Location
{
    /// <summary>
    /// Unique identifier for the Location.
    /// </summary>
    public string Id { get; init; } = default!;

    /// <summary>
    /// A self link to this entity.
    /// </summary>
    public string? SelfLink { get; init; }

    /// <summary>
    /// A property provides a label for Location entity.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// The description of the Location entity.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// The encoding type of the location property.
    /// Common values: "application/geo+json"
    /// </summary>
    public string EncodingType { get; init; } = "application/geo+json";

    /// <summary>
    /// The location as a NetTopologySuite Geometry object.
    /// </summary>
    public Geometry Location { get; init; } = default!;

    /// <summary>
    /// A JSON Object containing user-annotated properties as key-value pairs.
    /// </summary>
    public Dictionary<string, object>? Properties { get; init; }

    // Navigation properties

    /// <summary>
    /// Things associated with this Location.
    /// </summary>
    public IReadOnlyList<Thing>? Things { get; init; }

    /// <summary>
    /// HistoricalLocations associated with this Location.
    /// </summary>
    public IReadOnlyList<HistoricalLocation>? HistoricalLocations { get; init; }

    // Audit fields

    /// <summary>
    /// Timestamp when this Location was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when this Location was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}

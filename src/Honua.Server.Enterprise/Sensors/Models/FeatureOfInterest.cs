using NetTopologySuite.Geometries;

namespace Honua.Server.Enterprise.Sensors.Models;

/// <summary>
/// Represents a FeatureOfInterest entity in the OGC SensorThings API.
/// An Observation results in a value being assigned to a phenomenon. The phenomenon is a property of a feature,
/// the latter being the FeatureOfInterest of the Observation.
/// </summary>
public sealed record FeatureOfInterest
{
    /// <summary>
    /// Unique identifier for the FeatureOfInterest.
    /// </summary>
    public string Id { get; init; } = default!;

    /// <summary>
    /// A self link to this entity.
    /// </summary>
    public string? SelfLink { get; init; }

    /// <summary>
    /// A property provides a label for FeatureOfInterest entity.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// The description of the FeatureOfInterest entity.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// The encoding type of the feature property.
    /// Common values: "application/geo+json"
    /// </summary>
    public string EncodingType { get; init; } = "application/geo+json";

    /// <summary>
    /// The detailed description of the feature. The data type is defined by encodingType.
    /// </summary>
    public Geometry Feature { get; init; } = default!;

    /// <summary>
    /// A JSON Object containing user-annotated properties as key-value pairs.
    /// </summary>
    public Dictionary<string, object>? Properties { get; init; }

    // Navigation properties

    /// <summary>
    /// The Observations associated with this FeatureOfInterest.
    /// </summary>
    public IReadOnlyList<Observation>? Observations { get; init; }

    // Audit fields

    /// <summary>
    /// Timestamp when this FeatureOfInterest was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when this FeatureOfInterest was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}

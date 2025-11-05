using NetTopologySuite.Geometries;

namespace Honua.Server.Enterprise.Sensors.Models;

/// <summary>
/// Represents a Datastream entity in the OGC SensorThings API.
/// A Datastream groups a collection of Observations measuring the same ObservedProperty and produced by the same Sensor.
/// </summary>
public sealed record Datastream
{
    /// <summary>
    /// Unique identifier for the Datastream.
    /// </summary>
    public string Id { get; init; } = default!;

    /// <summary>
    /// A self link to this entity.
    /// </summary>
    public string? SelfLink { get; init; }

    /// <summary>
    /// A property provides a label for Datastream entity.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// The description of the Datastream entity.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// The type of Observation (with unique result type), which is used by the service to encode observations.
    /// Common values: "http://www.opengis.net/def/observationType/OGC-OM/2.0/OM_Measurement"
    /// </summary>
    public string ObservationType { get; init; } = default!;

    /// <summary>
    /// A JSON object containing three key-value pairs. The name property presents the full name of the unitOfMeasurement;
    /// the symbol property shows the textual form of the unit symbol; and the definition contains the URI defining the unitOfMeasurement.
    /// </summary>
    public UnitOfMeasurement UnitOfMeasurement { get; init; } = default!;

    /// <summary>
    /// The spatial bounding box of the spatial extent of all FeaturesOfInterest that belong to the Observations associated with this Datastream.
    /// </summary>
    public Geometry? ObservedArea { get; init; }

    /// <summary>
    /// The beginning of the phenomenon time period of all Observations in this Datastream.
    /// </summary>
    public DateTime? PhenomenonTimeStart { get; init; }

    /// <summary>
    /// The end of the phenomenon time period of all Observations in this Datastream.
    /// </summary>
    public DateTime? PhenomenonTimeEnd { get; init; }

    /// <summary>
    /// The beginning of the result time period of all Observations in this Datastream.
    /// </summary>
    public DateTime? ResultTimeStart { get; init; }

    /// <summary>
    /// The end of the result time period of all Observations in this Datastream.
    /// </summary>
    public DateTime? ResultTimeEnd { get; init; }

    /// <summary>
    /// A JSON Object containing user-annotated properties as key-value pairs.
    /// </summary>
    public Dictionary<string, object>? Properties { get; init; }

    // Foreign keys

    /// <summary>
    /// The ID of the Thing associated with this Datastream.
    /// </summary>
    public string ThingId { get; init; } = default!;

    /// <summary>
    /// The ID of the Sensor associated with this Datastream.
    /// </summary>
    public string SensorId { get; init; } = default!;

    /// <summary>
    /// The ID of the ObservedProperty associated with this Datastream.
    /// </summary>
    public string ObservedPropertyId { get; init; } = default!;

    // Navigation properties

    /// <summary>
    /// The Thing associated with this Datastream (when expanded).
    /// </summary>
    public Thing? Thing { get; init; }

    /// <summary>
    /// The Sensor associated with this Datastream (when expanded).
    /// </summary>
    public Sensor? Sensor { get; init; }

    /// <summary>
    /// The ObservedProperty associated with this Datastream (when expanded).
    /// </summary>
    public ObservedProperty? ObservedProperty { get; init; }

    /// <summary>
    /// The Observations associated with this Datastream (when expanded).
    /// </summary>
    public IReadOnlyList<Observation>? Observations { get; init; }

    // Audit fields

    /// <summary>
    /// Timestamp when this Datastream was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when this Datastream was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}

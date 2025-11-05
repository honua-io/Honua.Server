namespace Honua.Server.Enterprise.Sensors.Models;

/// <summary>
/// Represents an Observation entity in the OGC SensorThings API.
/// An Observation is the act of measuring or otherwise determining the value of a property.
/// </summary>
public sealed record Observation
{
    /// <summary>
    /// Unique identifier for the Observation.
    /// </summary>
    public string Id { get; init; } = default!;

    /// <summary>
    /// A self link to this entity.
    /// </summary>
    public string? SelfLink { get; init; }

    /// <summary>
    /// The time instant or period of when the Observation happens.
    /// </summary>
    public DateTime PhenomenonTime { get; init; }

    /// <summary>
    /// The time of the Observation's result was generated.
    /// </summary>
    public DateTime? ResultTime { get; init; }

    /// <summary>
    /// The estimated value of an ObservedProperty from the Observation.
    /// Can be a number, string, boolean, or complex JSON object.
    /// </summary>
    public object Result { get; init; } = default!;

    /// <summary>
    /// Describes the quality of the result. Should be of type DQ_Element (ISO 19157).
    /// </summary>
    public string? ResultQuality { get; init; }

    /// <summary>
    /// The time period during which the result may be used.
    /// </summary>
    public DateTime? ValidTimeStart { get; init; }

    /// <summary>
    /// The end of the time period during which the result may be used.
    /// </summary>
    public DateTime? ValidTimeEnd { get; init; }

    /// <summary>
    /// Key-value pairs showing the environmental conditions during measurement.
    /// </summary>
    public Dictionary<string, object>? Parameters { get; init; }

    // Foreign keys

    /// <summary>
    /// The ID of the Datastream associated with this Observation.
    /// </summary>
    public string DatastreamId { get; init; } = default!;

    /// <summary>
    /// The ID of the FeatureOfInterest associated with this Observation.
    /// </summary>
    public string? FeatureOfInterestId { get; init; }

    // Navigation properties

    /// <summary>
    /// The Datastream associated with this Observation (when expanded).
    /// </summary>
    public Datastream? Datastream { get; init; }

    /// <summary>
    /// The FeatureOfInterest associated with this Observation (when expanded).
    /// </summary>
    public FeatureOfInterest? FeatureOfInterest { get; init; }

    // Mobile-specific fields

    /// <summary>
    /// The timestamp from the client device when the observation was recorded.
    /// Used for offline sync scenarios.
    /// </summary>
    public DateTime? ClientTimestamp { get; init; }

    /// <summary>
    /// The timestamp when the observation was received by the server.
    /// </summary>
    public DateTime ServerTimestamp { get; init; }

    /// <summary>
    /// Identifier for the batch sync operation that created this observation.
    /// Used for tracking offline sync operations.
    /// </summary>
    public string? SyncBatchId { get; init; }

    // Audit fields

    /// <summary>
    /// Timestamp when this Observation was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }
}

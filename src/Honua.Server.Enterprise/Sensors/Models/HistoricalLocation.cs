namespace Honua.Server.Enterprise.Sensors.Models;

/// <summary>
/// Represents a HistoricalLocation entity in the OGC SensorThings API.
/// A Thing's HistoricalLocation entity set provides the times of the current and previous locations of the Thing.
/// </summary>
public sealed record HistoricalLocation
{
    /// <summary>
    /// Unique identifier for the HistoricalLocation.
    /// </summary>
    public string Id { get; init; } = default!;

    /// <summary>
    /// A self link to this entity.
    /// </summary>
    public string? SelfLink { get; init; }

    /// <summary>
    /// The time when the Thing was at the Location.
    /// </summary>
    public DateTime Time { get; init; }

    // Navigation properties

    /// <summary>
    /// The Thing associated with this HistoricalLocation.
    /// </summary>
    public string ThingId { get; init; } = default!;

    /// <summary>
    /// The Thing entity (when expanded).
    /// </summary>
    public Thing? Thing { get; init; }

    /// <summary>
    /// The Locations associated with this HistoricalLocation.
    /// </summary>
    public IReadOnlyList<Location>? Locations { get; init; }

    // Audit fields

    /// <summary>
    /// Timestamp when this HistoricalLocation was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }
}

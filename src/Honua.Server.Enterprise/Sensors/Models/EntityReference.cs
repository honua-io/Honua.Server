using System.Text.Json.Serialization;

namespace Honua.Server.Enterprise.Sensors.Models;

/// <summary>
/// Represents a reference to an entity by its ID.
/// Used in requests to link entities together (e.g., in deep insert or navigation).
/// </summary>
public sealed record EntityReference
{
    /// <summary>
    /// The ID of the referenced entity.
    /// In SensorThings API, this is typically represented as "@iot.id" in JSON.
    /// </summary>
    [JsonPropertyName("@iot.id")]
    public string Id { get; init; } = default!;
}

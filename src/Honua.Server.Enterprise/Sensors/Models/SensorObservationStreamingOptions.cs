namespace Honua.Server.Enterprise.Sensors.Models;

/// <summary>
/// Configuration options for sensor observation streaming via SignalR.
/// </summary>
public class SensorObservationStreamingOptions
{
    /// <summary>
    /// Enable/disable WebSocket streaming for observations.
    /// Default: false (opt-in feature)
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Enable rate limiting to prevent overwhelming clients.
    /// Default: true
    /// </summary>
    public bool RateLimitingEnabled { get; set; } = true;

    /// <summary>
    /// Maximum observations per second per group (datastream/thing/sensor).
    /// Default: 100
    /// </summary>
    public int RateLimitPerSecond { get; set; } = 100;

    /// <summary>
    /// Enable batching for high-volume sensors.
    /// Default: true
    /// </summary>
    public bool BatchingEnabled { get; set; } = true;

    /// <summary>
    /// Threshold for batching (if more than this many observations/sec, batch them).
    /// Default: 100
    /// </summary>
    public int BatchingThreshold { get; set; } = 100;

    /// <summary>
    /// Configuration section name for binding.
    /// </summary>
    public const string SectionName = "SensorThings:Streaming";
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Enterprise.Sensors.AnomalyDetection.Models;

/// <summary>
/// Represents a detected sensor anomaly
/// </summary>
public sealed record SensorAnomaly
{
    /// <summary>
    /// Unique identifier for this anomaly
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of anomaly detected
    /// </summary>
    public AnomalyType Type { get; init; }

    /// <summary>
    /// Severity level of the anomaly
    /// </summary>
    public AnomalySeverity Severity { get; init; }

    /// <summary>
    /// Datastream ID where the anomaly was detected
    /// </summary>
    public string DatastreamId { get; init; } = default!;

    /// <summary>
    /// Datastream name for human-readable context
    /// </summary>
    public string DatastreamName { get; init; } = default!;

    /// <summary>
    /// Thing (sensor) ID
    /// </summary>
    public string ThingId { get; init; } = default!;

    /// <summary>
    /// Thing (sensor) name
    /// </summary>
    public string ThingName { get; init; } = default!;

    /// <summary>
    /// Sensor ID
    /// </summary>
    public string SensorId { get; init; } = default!;

    /// <summary>
    /// Sensor name
    /// </summary>
    public string SensorName { get; init; } = default!;

    /// <summary>
    /// ObservedProperty being measured
    /// </summary>
    public string ObservedPropertyName { get; init; } = default!;

    /// <summary>
    /// When the anomaly was detected
    /// </summary>
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Last observation time (for stale sensor detection)
    /// </summary>
    public DateTime? LastObservationTime { get; init; }

    /// <summary>
    /// Time since last observation (for stale sensor detection)
    /// </summary>
    public TimeSpan? TimeSinceLastObservation { get; init; }

    /// <summary>
    /// Anomalous observation value (for unusual reading detection)
    /// </summary>
    public object? AnomalousValue { get; init; }

    /// <summary>
    /// Expected value or range (for unusual reading detection)
    /// </summary>
    public object? ExpectedValue { get; init; }

    /// <summary>
    /// Statistical deviation (for unusual reading detection)
    /// Number of standard deviations from the mean
    /// </summary>
    public double? StandardDeviations { get; init; }

    /// <summary>
    /// Mean value from historical data
    /// </summary>
    public double? Mean { get; init; }

    /// <summary>
    /// Standard deviation from historical data
    /// </summary>
    public double? StdDev { get; init; }

    /// <summary>
    /// Number of observations used for statistical calculation
    /// </summary>
    public int? ObservationCount { get; init; }

    /// <summary>
    /// Human-readable description of the anomaly
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// Additional metadata about the anomaly
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public string? TenantId { get; init; }
}

/// <summary>
/// Type of sensor anomaly
/// </summary>
public enum AnomalyType
{
    /// <summary>
    /// Sensor has not reported data within the expected time window
    /// </summary>
    StaleSensor,

    /// <summary>
    /// Observation value is a statistical outlier
    /// </summary>
    UnusualReading,

    /// <summary>
    /// Sensor readings have stopped completely
    /// </summary>
    SensorOffline,

    /// <summary>
    /// Observation value is outside valid range
    /// </summary>
    OutOfRange
}

/// <summary>
/// Severity level of an anomaly
/// </summary>
public enum AnomalySeverity
{
    /// <summary>
    /// Informational - no immediate action required
    /// </summary>
    Info,

    /// <summary>
    /// Warning - may require investigation
    /// </summary>
    Warning,

    /// <summary>
    /// Critical - immediate attention required
    /// </summary>
    Critical
}

/// <summary>
/// Statistical summary for a datastream
/// Used for unusual reading detection
/// </summary>
public sealed record DatastreamStatistics
{
    public string DatastreamId { get; init; } = default!;
    public string DatastreamName { get; init; } = default!;
    public string ObservedPropertyName { get; init; } = default!;
    public int ObservationCount { get; init; }
    public double Mean { get; init; }
    public double StandardDeviation { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public DateTime CalculatedAt { get; init; } = DateTime.UtcNow;
    public TimeSpan Window { get; init; }
}

/// <summary>
/// Alert payload sent to webhooks
/// </summary>
public sealed record AnomalyAlert
{
    /// <summary>
    /// Alert ID
    /// </summary>
    public string AlertId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Event type for webhook routing
    /// </summary>
    public string EventType { get; init; } = "sensor.anomaly.detected";

    /// <summary>
    /// Timestamp when the alert was generated
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// The detected anomaly
    /// </summary>
    public SensorAnomaly Anomaly { get; init; } = default!;

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Additional context for the alert
    /// </summary>
    public Dictionary<string, object>? Context { get; init; }
}

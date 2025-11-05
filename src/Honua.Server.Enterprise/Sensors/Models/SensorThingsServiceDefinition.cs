namespace Honua.Server.Enterprise.Sensors.Models;

/// <summary>
/// Configuration definition for the SensorThings API service.
/// This is part of Honua's metadata-driven architecture.
/// </summary>
public sealed record SensorThingsServiceDefinition
{
    /// <summary>
    /// Whether the SensorThings API is enabled for this service.
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// The SensorThings API version to expose.
    /// Default: "v1.1"
    /// </summary>
    public string Version { get; init; } = "v1.1";

    /// <summary>
    /// The base path for the SensorThings API endpoints.
    /// Default: "/sta/v1.1"
    /// </summary>
    public string BasePath { get; init; } = "/sta/v1.1";

    // Feature flags

    /// <summary>
    /// Whether MQTT support is enabled for real-time observation streaming.
    /// </summary>
    public bool MqttEnabled { get; init; } = false;

    /// <summary>
    /// Whether batch operations ($batch endpoint) are enabled.
    /// Recommended: true for mobile applications.
    /// </summary>
    public bool BatchOperationsEnabled { get; init; } = true;

    /// <summary>
    /// Whether deep insert is enabled (creating entity hierarchies in a single request).
    /// Recommended: true for simplified client operations.
    /// </summary>
    public bool DeepInsertEnabled { get; init; } = true;

    /// <summary>
    /// Whether the Data Array extension is enabled for efficient bulk observation uploads.
    /// Recommended: true for mobile applications with many observations.
    /// </summary>
    public bool DataArrayEnabled { get; init; } = true;

    // Mobile optimizations

    /// <summary>
    /// Whether the custom offline sync endpoint is enabled.
    /// </summary>
    public bool OfflineSyncEnabled { get; init; } = true;

    /// <summary>
    /// Maximum number of items allowed in a single batch operation.
    /// </summary>
    public int MaxBatchSize { get; init; } = 1000;

    /// <summary>
    /// Maximum number of observations allowed in a single request.
    /// </summary>
    public int MaxObservationsPerRequest { get; init; } = 5000;

    // Storage configuration

    /// <summary>
    /// The ID of the data source (database connection) to use for SensorThings data.
    /// Must reference a valid DataSourceDefinition in the metadata.
    /// </summary>
    public string DataSourceId { get; init; } = default!;

    /// <summary>
    /// Storage-specific configuration for the SensorThings entities.
    /// </summary>
    public SensorThingsStorageDefinition Storage { get; init; } = new();
}

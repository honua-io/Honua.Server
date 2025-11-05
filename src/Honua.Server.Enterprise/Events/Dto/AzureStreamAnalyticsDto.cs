using System.Text.Json.Serialization;

namespace Honua.Server.Enterprise.Events.Dto;

/// <summary>
/// Azure Stream Analytics event payload
/// </summary>
/// <remarks>
/// This represents the expected format from Azure Stream Analytics output.
/// Configure ASA to send location data in this format.
/// </remarks>
public class AzureStreamAnalyticsEvent
{
    /// <summary>
    /// Unique identifier for the entity (device, vehicle, asset)
    /// </summary>
    [JsonPropertyName("entity_id")]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Type of entity
    /// </summary>
    [JsonPropertyName("entity_type")]
    public string? EntityType { get; set; }

    /// <summary>
    /// Longitude (X coordinate)
    /// </summary>
    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    /// <summary>
    /// Latitude (Y coordinate)
    /// </summary>
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    /// <summary>
    /// Timestamp when the event was created (ISO 8601)
    /// </summary>
    [JsonPropertyName("event_time")]
    public DateTime? EventTime { get; set; }

    /// <summary>
    /// Additional properties from the source event
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, object>? Properties { get; set; }

    /// <summary>
    /// Optional IoT Hub device ID
    /// </summary>
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    /// <summary>
    /// Optional partition key for sharding
    /// </summary>
    [JsonPropertyName("partition_key")]
    public string? PartitionKey { get; set; }
}

/// <summary>
/// Batch of Azure Stream Analytics events
/// </summary>
public class AzureStreamAnalyticsBatch
{
    /// <summary>
    /// Array of events from Stream Analytics
    /// </summary>
    [JsonPropertyName("events")]
    public List<AzureStreamAnalyticsEvent> Events { get; set; } = new();

    /// <summary>
    /// Batch metadata
    /// </summary>
    [JsonPropertyName("metadata")]
    public AzureStreamAnalyticsBatchMetadata? Metadata { get; set; }
}

/// <summary>
/// Metadata for Azure Stream Analytics batch
/// </summary>
public class AzureStreamAnalyticsBatchMetadata
{
    /// <summary>
    /// Stream Analytics job name
    /// </summary>
    [JsonPropertyName("job_name")]
    public string? JobName { get; set; }

    /// <summary>
    /// Output name in Stream Analytics
    /// </summary>
    [JsonPropertyName("output_name")]
    public string? OutputName { get; set; }

    /// <summary>
    /// Batch timestamp
    /// </summary>
    [JsonPropertyName("batch_time")]
    public DateTime? BatchTime { get; set; }
}

/// <summary>
/// Response from Azure Stream Analytics webhook
/// </summary>
public class AzureStreamAnalyticsResponse
{
    /// <summary>
    /// Number of events processed successfully
    /// </summary>
    [JsonPropertyName("processed_count")]
    public int ProcessedCount { get; set; }

    /// <summary>
    /// Number of events that failed
    /// </summary>
    [JsonPropertyName("failed_count")]
    public int FailedCount { get; set; }

    /// <summary>
    /// Total events generated (enter/exit)
    /// </summary>
    [JsonPropertyName("events_generated_count")]
    public int EventsGeneratedCount { get; set; }

    /// <summary>
    /// Processing time in milliseconds
    /// </summary>
    [JsonPropertyName("processing_time_ms")]
    public double ProcessingTimeMs { get; set; }

    /// <summary>
    /// Errors encountered
    /// </summary>
    [JsonPropertyName("errors")]
    public List<string>? Errors { get; set; }
}

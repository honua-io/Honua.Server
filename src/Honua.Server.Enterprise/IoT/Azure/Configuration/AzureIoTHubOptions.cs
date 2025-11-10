// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Enterprise.IoT.Azure.Configuration;

/// <summary>
/// Configuration options for Azure IoT Hub integration with SensorThings API
/// </summary>
public sealed class AzureIoTHubOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "AzureIoTHub";

    /// <summary>
    /// Enable or disable the IoT Hub integration
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Event Hub-compatible connection string (IoT Hub's built-in endpoint)
    /// Format: Endpoint=sb://...;SharedAccessKeyName=...;SharedAccessKey=...;EntityPath=...
    /// If null, will use DefaultAzureCredential for Managed Identity
    /// </summary>
    public string? EventHubConnectionString { get; set; }

    /// <summary>
    /// Event Hub-compatible name (typically the IoT Hub name)
    /// Required when using Managed Identity
    /// </summary>
    public string? EventHubName { get; set; }

    /// <summary>
    /// Fully qualified Event Hub namespace (e.g., myhub.servicebus.windows.net)
    /// Required when using Managed Identity
    /// </summary>
    public string? EventHubNamespace { get; set; }

    /// <summary>
    /// Consumer group name (default: $Default)
    /// It's recommended to create a dedicated consumer group for Honua
    /// </summary>
    public string ConsumerGroup { get; set; } = "$Default";

    /// <summary>
    /// Azure Storage connection string for checkpointing
    /// Required for Event Hub processor to track position
    /// </summary>
    public string? CheckpointStorageConnectionString { get; set; }

    /// <summary>
    /// Azure Storage container name for checkpoints
    /// </summary>
    public string CheckpointContainerName { get; set; } = "iot-hub-checkpoints";

    /// <summary>
    /// Maximum number of events to process in a single batch
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum wait time for a batch to fill before processing
    /// </summary>
    public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum number of concurrent partition processors
    /// Set to 0 for unlimited (will process all partitions concurrently)
    /// </summary>
    public int MaxConcurrentPartitions { get; set; } = 0;

    /// <summary>
    /// Path to device/sensor mapping configuration file (JSON or YAML)
    /// If null, will use default auto-mapping logic
    /// </summary>
    public string? MappingConfigurationPath { get; set; }

    /// <summary>
    /// Retry policy configuration
    /// </summary>
    public RetryPolicyOptions RetryPolicy { get; set; } = new();

    /// <summary>
    /// Error handling configuration
    /// </summary>
    public ErrorHandlingOptions ErrorHandling { get; set; } = new();

    /// <summary>
    /// Telemetry parsing options
    /// </summary>
    public TelemetryParsingOptions TelemetryParsing { get; set; } = new();

    /// <summary>
    /// Validate configuration
    /// </summary>
    public void Validate()
    {
        if (Enabled)
        {
            // Must have either connection string OR namespace + name
            if (string.IsNullOrWhiteSpace(EventHubConnectionString))
            {
                if (string.IsNullOrWhiteSpace(EventHubNamespace) || string.IsNullOrWhiteSpace(EventHubName))
                {
                    throw new InvalidOperationException(
                        "Either EventHubConnectionString OR both EventHubNamespace and EventHubName must be provided");
                }
            }

            if (string.IsNullOrWhiteSpace(CheckpointStorageConnectionString))
            {
                throw new InvalidOperationException("CheckpointStorageConnectionString is required");
            }

            if (MaxBatchSize <= 0)
            {
                throw new InvalidOperationException("MaxBatchSize must be greater than 0");
            }
        }
    }
}

/// <summary>
/// Retry policy configuration
/// </summary>
public sealed class RetryPolicyOptions
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial retry delay
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum retry delay
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Retry delay multiplier (for exponential backoff)
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;
}

/// <summary>
/// Error handling configuration
/// </summary>
public sealed class ErrorHandlingOptions
{
    /// <summary>
    /// Enable dead letter queue for failed messages
    /// </summary>
    public bool EnableDeadLetterQueue { get; set; } = true;

    /// <summary>
    /// Log malformed messages at this level
    /// </summary>
    public string MalformedMessageLogLevel { get; set; } = "Warning";

    /// <summary>
    /// Continue processing on errors (vs. stop the service)
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// Maximum consecutive errors before marking service unhealthy
    /// </summary>
    public int MaxConsecutiveErrors { get; set; } = 10;
}

/// <summary>
/// Telemetry parsing configuration
/// </summary>
public sealed class TelemetryParsingOptions
{
    /// <summary>
    /// Expected telemetry format (Json, Binary, Custom)
    /// </summary>
    public string DefaultFormat { get; set; } = "Json";

    /// <summary>
    /// Property name in IoT Hub message that contains the telemetry timestamp
    /// If null, will use the IoT Hub enqueued time
    /// </summary>
    public string? TimestampProperty { get; set; }

    /// <summary>
    /// Timestamp format (for parsing string timestamps)
    /// </summary>
    public string TimestampFormat { get; set; } = "yyyy-MM-ddTHH:mm:ss.fffZ";

    /// <summary>
    /// Whether to preserve IoT Hub system properties as observation parameters
    /// </summary>
    public bool PreserveSystemProperties { get; set; } = true;

    /// <summary>
    /// Whether to preserve IoT Hub application properties as observation parameters
    /// </summary>
    public bool PreserveApplicationProperties { get; set; } = true;
}

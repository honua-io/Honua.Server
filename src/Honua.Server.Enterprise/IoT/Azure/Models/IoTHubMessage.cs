// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Enterprise.IoT.Azure.Models;

/// <summary>
/// Represents a parsed IoT Hub message with telemetry data
/// </summary>
public sealed class IoTHubMessage
{
    /// <summary>
    /// Device ID that sent the message
    /// </summary>
    public string DeviceId { get; init; } = default!;

    /// <summary>
    /// Module ID (if message is from a module)
    /// </summary>
    public string? ModuleId { get; init; }

    /// <summary>
    /// Message ID
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// Correlation ID
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Time the message was enqueued in IoT Hub
    /// </summary>
    public DateTime EnqueuedTime { get; init; }

    /// <summary>
    /// Message body as raw bytes
    /// </summary>
    public byte[] Body { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// Parsed telemetry data (key-value pairs)
    /// </summary>
    public Dictionary<string, object> Telemetry { get; init; } = new();

    /// <summary>
    /// IoT Hub system properties
    /// </summary>
    public Dictionary<string, object> SystemProperties { get; init; } = new();

    /// <summary>
    /// Application properties from the message
    /// </summary>
    public Dictionary<string, object> ApplicationProperties { get; init; } = new();

    /// <summary>
    /// Sequence number from Event Hub
    /// </summary>
    public long SequenceNumber { get; init; }

    /// <summary>
    /// Partition key
    /// </summary>
    public string? PartitionKey { get; init; }

    /// <summary>
    /// Offset in the Event Hub partition
    /// </summary>
    public string Offset { get; init; } = default!;
}

/// <summary>
/// Result of processing a batch of IoT Hub messages
/// </summary>
public sealed class MessageProcessingResult
{
    /// <summary>
    /// Number of messages successfully processed
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// Number of messages that failed processing
    /// </summary>
    public int FailureCount { get; init; }

    /// <summary>
    /// Number of messages skipped (e.g., filtered out)
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Total number of observations created
    /// </summary>
    public int ObservationsCreated { get; init; }

    /// <summary>
    /// Total number of Things created
    /// </summary>
    public int ThingsCreated { get; init; }

    /// <summary>
    /// Total number of Datastreams created
    /// </summary>
    public int DatastreamsCreated { get; init; }

    /// <summary>
    /// Processing duration
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Errors that occurred during processing
    /// </summary>
    public List<ProcessingError> Errors { get; init; } = new();

    /// <summary>
    /// Create a successful result
    /// </summary>
    public static MessageProcessingResult Success(
        int successCount,
        int observationsCreated,
        int thingsCreated,
        int datastreamsCreated,
        TimeSpan duration)
    {
        return new MessageProcessingResult
        {
            SuccessCount = successCount,
            ObservationsCreated = observationsCreated,
            ThingsCreated = thingsCreated,
            DatastreamsCreated = datastreamsCreated,
            Duration = duration
        };
    }

    /// <summary>
    /// Merge multiple results
    /// </summary>
    public static MessageProcessingResult Merge(IEnumerable<MessageProcessingResult> results)
    {
        var allResults = results.ToList();
        return new MessageProcessingResult
        {
            SuccessCount = allResults.Sum(r => r.SuccessCount),
            FailureCount = allResults.Sum(r => r.FailureCount),
            SkippedCount = allResults.Sum(r => r.SkippedCount),
            ObservationsCreated = allResults.Sum(r => r.ObservationsCreated),
            ThingsCreated = allResults.Sum(r => r.ThingsCreated),
            DatastreamsCreated = allResults.Sum(r => r.DatastreamsCreated),
            Duration = allResults.Max(r => r.Duration),
            Errors = allResults.SelectMany(r => r.Errors).ToList()
        };
    }
}

/// <summary>
/// Represents an error that occurred during message processing
/// </summary>
public sealed class ProcessingError
{
    /// <summary>
    /// Device ID that caused the error
    /// </summary>
    public string DeviceId { get; init; } = default!;

    /// <summary>
    /// Message ID (if available)
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// Error message
    /// </summary>
    public string Message { get; init; } = default!;

    /// <summary>
    /// Exception details
    /// </summary>
    public string? ExceptionDetails { get; init; }

    /// <summary>
    /// When the error occurred
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Retry count (if applicable)
    /// </summary>
    public int RetryCount { get; init; }
}

/// <summary>
/// Dead letter queue message
/// </summary>
public sealed class DeadLetterMessage
{
    /// <summary>
    /// Unique ID for this dead letter message
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Original IoT Hub message
    /// </summary>
    public IoTHubMessage OriginalMessage { get; init; } = default!;

    /// <summary>
    /// Error that caused the message to be dead lettered
    /// </summary>
    public ProcessingError Error { get; init; } = default!;

    /// <summary>
    /// When the message was dead lettered
    /// </summary>
    public DateTime DeadLetteredAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Number of times processing was attempted
    /// </summary>
    public int AttemptCount { get; init; }
}

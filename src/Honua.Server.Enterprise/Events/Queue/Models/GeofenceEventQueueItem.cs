// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Enterprise.Events.Queue.Models;

/// <summary>
/// Represents a queued geofence event awaiting delivery
/// </summary>
public class GeofenceEventQueueItem
{
    /// <summary>
    /// Queue item ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Reference to the source geofence event
    /// </summary>
    public Guid GeofenceEventId { get; set; }

    /// <summary>
    /// Delivery status
    /// </summary>
    public QueueItemStatus Status { get; set; } = QueueItemStatus.Pending;

    /// <summary>
    /// Priority (higher = more urgent)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Partition key for FIFO ordering (geofence_id for ordering per geofence)
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Deduplication fingerprint (hash of event content)
    /// </summary>
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>
    /// Number of delivery attempts
    /// </summary>
    public int AttemptCount { get; set; } = 0;

    /// <summary>
    /// Maximum retry attempts before moving to DLQ
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Next scheduled delivery attempt
    /// </summary>
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Exponential backoff delay in seconds
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Delivery targets (signalr, servicebus, webhook, etc.)
    /// </summary>
    public List<string> DeliveryTargets { get; set; } = new() { "signalr" };

    /// <summary>
    /// Delivery results per target
    /// </summary>
    public Dictionary<string, DeliveryResult>? DeliveryResults { get; set; }

    /// <summary>
    /// Last error message (if any)
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Multi-tenancy
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Timestamps
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Queue item status
/// </summary>
public enum QueueItemStatus
{
    /// <summary>
    /// Awaiting delivery
    /// </summary>
    Pending,

    /// <summary>
    /// Currently being processed
    /// </summary>
    Processing,

    /// <summary>
    /// Successfully delivered
    /// </summary>
    Completed,

    /// <summary>
    /// Failed after retries
    /// </summary>
    Failed,

    /// <summary>
    /// Moved to dead letter queue
    /// </summary>
    DeadLetter
}

/// <summary>
/// Delivery result for a specific target
/// </summary>
public class DeliveryResult
{
    /// <summary>
    /// Delivery status
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// When delivery was attempted
    /// </summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>
    /// Number of recipients delivered to
    /// </summary>
    public int? RecipientCount { get; set; }

    /// <summary>
    /// Delivery latency in milliseconds
    /// </summary>
    public int? LatencyMs { get; set; }

    /// <summary>
    /// Error message if delivery failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Delivery log entry for audit trail
/// </summary>
public class GeofenceEventDeliveryLog
{
    /// <summary>
    /// Log entry ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Reference to queue item
    /// </summary>
    public Guid QueueItemId { get; set; }

    /// <summary>
    /// Delivery attempt number
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Delivery target (signalr, servicebus, webhook, etc.)
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Delivery status
    /// </summary>
    public DeliveryStatus Status { get; set; }

    /// <summary>
    /// Number of recipients/subscribers delivered to
    /// </summary>
    public int? RecipientCount { get; set; }

    /// <summary>
    /// Delivery latency in milliseconds
    /// </summary>
    public int? LatencyMs { get; set; }

    /// <summary>
    /// Error message (if failed)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Timestamp
    /// </summary>
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Delivery status for audit log
/// </summary>
public enum DeliveryStatus
{
    /// <summary>
    /// Successfully delivered to all recipients
    /// </summary>
    Success,

    /// <summary>
    /// Delivered to some recipients, but not all
    /// </summary>
    Partial,

    /// <summary>
    /// Failed to deliver to any recipients
    /// </summary>
    Failed,

    /// <summary>
    /// Delivery timed out
    /// </summary>
    Timeout
}

/// <summary>
/// Queue metrics for monitoring
/// </summary>
public class QueueMetrics
{
    /// <summary>
    /// Number of pending items
    /// </summary>
    public long PendingCount { get; set; }

    /// <summary>
    /// Number of items being processed
    /// </summary>
    public long ProcessingCount { get; set; }

    /// <summary>
    /// Number of completed items (last hour)
    /// </summary>
    public long CompletedCount { get; set; }

    /// <summary>
    /// Number of items in dead letter queue
    /// </summary>
    public long DeadLetterCount { get; set; }

    /// <summary>
    /// Average queue depth in seconds
    /// </summary>
    public double AvgQueueDepthSeconds { get; set; }

    /// <summary>
    /// Average delivery latency in milliseconds
    /// </summary>
    public double AvgDeliveryLatencyMs { get; set; }

    /// <summary>
    /// Success rate percentage
    /// </summary>
    public double SuccessRatePercent { get; set; }

    /// <summary>
    /// Age of oldest pending item in seconds
    /// </summary>
    public int? OldestPendingAgeSeconds { get; set; }
}

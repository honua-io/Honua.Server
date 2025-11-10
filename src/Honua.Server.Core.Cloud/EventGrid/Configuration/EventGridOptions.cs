// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Cloud.EventGrid.Configuration;

/// <summary>
/// Configuration options for Azure Event Grid publisher.
/// </summary>
public class EventGridOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Honua:EventGrid";

    /// <summary>
    /// Enable/disable Event Grid publishing globally.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Azure Event Grid topic endpoint (e.g., https://mytopic.westus-1.eventgrid.azure.net/api/events).
    /// </summary>
    public string? TopicEndpoint { get; set; }

    /// <summary>
    /// Azure Event Grid topic access key (alternative: use Managed Identity).
    /// </summary>
    public string? TopicKey { get; set; }

    /// <summary>
    /// Use Azure Managed Identity for authentication (recommended for production).
    /// If true, TopicKey is ignored.
    /// </summary>
    public bool UseManagedIdentity { get; set; } = true;

    /// <summary>
    /// Event Grid domain endpoint (alternative to topic for multi-topic scenarios).
    /// </summary>
    public string? DomainEndpoint { get; set; }

    /// <summary>
    /// Event Grid domain access key.
    /// </summary>
    public string? DomainKey { get; set; }

    /// <summary>
    /// Maximum batch size for publishing events.
    /// Azure Event Grid supports up to 1MB per request (typically ~100-1000 events).
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>
    /// Flush interval in seconds for batched events.
    /// Events are published when batch size is reached OR flush interval expires.
    /// </summary>
    public int FlushIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum queue size for pending events (before batching).
    /// If queue fills up, oldest events are dropped (or publishing blocks based on BackpressureMode).
    /// </summary>
    public int MaxQueueSize { get; set; } = 10000;

    /// <summary>
    /// Event types to publish (if empty, all events are published).
    /// Use this to filter which events are sent to Event Grid.
    /// Example: ["honua.features.created", "honua.sensor.observation.created"]
    /// </summary>
    public List<string> EventTypeFilter { get; set; } = new();

    /// <summary>
    /// Collections to publish events for (if empty, all collections are published).
    /// Example: ["parcels", "buildings", "sensors"]
    /// </summary>
    public List<string> CollectionFilter { get; set; } = new();

    /// <summary>
    /// Tenants to publish events for (if empty, all tenants are published).
    /// Example: ["tenant1", "tenant2"]
    /// </summary>
    public List<string> TenantFilter { get; set; } = new();

    /// <summary>
    /// Backpressure mode when queue is full.
    /// - Drop: Drop oldest events (non-blocking)
    /// - Block: Block until queue has space (may slow down API responses)
    /// </summary>
    public BackpressureMode BackpressureMode { get; set; } = BackpressureMode.Drop;

    /// <summary>
    /// Retry configuration for failed publishes.
    /// </summary>
    public RetryOptions Retry { get; set; } = new();

    /// <summary>
    /// Circuit breaker configuration.
    /// </summary>
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();

    /// <summary>
    /// Validate configuration on startup.
    /// </summary>
    public void Validate()
    {
        if (!Enabled)
            return;

        if (string.IsNullOrWhiteSpace(TopicEndpoint) && string.IsNullOrWhiteSpace(DomainEndpoint))
        {
            throw new InvalidOperationException(
                "Event Grid is enabled but neither TopicEndpoint nor DomainEndpoint is configured.");
        }

        if (!UseManagedIdentity && string.IsNullOrWhiteSpace(TopicKey) && string.IsNullOrWhiteSpace(DomainKey))
        {
            throw new InvalidOperationException(
                "Event Grid is configured without Managed Identity, but no access key is provided.");
        }

        if (MaxBatchSize <= 0 || MaxBatchSize > 1000)
        {
            throw new InvalidOperationException("MaxBatchSize must be between 1 and 1000.");
        }

        if (FlushIntervalSeconds <= 0)
        {
            throw new InvalidOperationException("FlushIntervalSeconds must be greater than 0.");
        }

        if (MaxQueueSize <= 0)
        {
            throw new InvalidOperationException("MaxQueueSize must be greater than 0.");
        }
    }
}

/// <summary>
/// Retry options for Event Grid publishing.
/// </summary>
public class RetryOptions
{
    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial retry delay in seconds.
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 1;

    /// <summary>
    /// Maximum retry delay in seconds (for exponential backoff).
    /// </summary>
    public int MaxDelaySeconds { get; set; } = 30;
}

/// <summary>
/// Circuit breaker options for Event Grid publishing.
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// Enable circuit breaker.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of consecutive failures before opening the circuit.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Duration in seconds to keep the circuit open before attempting to close it.
    /// </summary>
    public int DurationOfBreakSeconds { get; set; } = 60;

    /// <summary>
    /// Sampling duration in seconds for failure rate calculation.
    /// </summary>
    public int SamplingDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Minimum throughput (requests per sampling duration) before circuit breaker activates.
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;
}

/// <summary>
/// Backpressure mode when event queue is full.
/// </summary>
public enum BackpressureMode
{
    /// <summary>
    /// Drop oldest events from the queue (non-blocking).
    /// </summary>
    Drop,

    /// <summary>
    /// Block callers until queue has space (may slow down API responses).
    /// </summary>
    Block
}

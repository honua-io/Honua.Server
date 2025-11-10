// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Cloud.EventGrid.Models;

namespace Honua.Server.Core.Cloud.EventGrid.Services;

/// <summary>
/// Service for publishing CloudEvents to Azure Event Grid.
/// </summary>
public interface IEventGridPublisher
{
    /// <summary>
    /// Publish a single event asynchronously (non-blocking, batched).
    /// </summary>
    /// <param name="cloudEvent">The CloudEvent to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when event is queued (not necessarily published)</returns>
    Task PublishAsync(HonuaCloudEvent cloudEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish multiple events asynchronously (non-blocking, batched).
    /// </summary>
    /// <param name="cloudEvents">The CloudEvents to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when events are queued (not necessarily published)</returns>
    Task PublishBatchAsync(IEnumerable<HonuaCloudEvent> cloudEvents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Flush all pending events immediately (synchronous publish to Event Grid).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when all pending events are published</returns>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current queue size (number of pending events).
    /// </summary>
    int GetQueueSize();

    /// <summary>
    /// Get metrics for monitoring (events published, failed, dropped).
    /// </summary>
    EventGridMetrics GetMetrics();
}

/// <summary>
/// Metrics for Event Grid publishing.
/// </summary>
public class EventGridMetrics
{
    /// <summary>
    /// Total events published successfully.
    /// </summary>
    public long EventsPublished { get; set; }

    /// <summary>
    /// Total events failed (after retries).
    /// </summary>
    public long EventsFailed { get; set; }

    /// <summary>
    /// Total events dropped (queue full).
    /// </summary>
    public long EventsDropped { get; set; }

    /// <summary>
    /// Total events filtered (didn't match filters).
    /// </summary>
    public long EventsFiltered { get; set; }

    /// <summary>
    /// Current queue size.
    /// </summary>
    public int CurrentQueueSize { get; set; }

    /// <summary>
    /// Circuit breaker state (Open, Closed, HalfOpen).
    /// </summary>
    public string CircuitBreakerState { get; set; } = "Closed";

    /// <summary>
    /// Last publish time.
    /// </summary>
    public DateTimeOffset? LastPublishTime { get; set; }

    /// <summary>
    /// Last error message.
    /// </summary>
    public string? LastError { get; set; }
}

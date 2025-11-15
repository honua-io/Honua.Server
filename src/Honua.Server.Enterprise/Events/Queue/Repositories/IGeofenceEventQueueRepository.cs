// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.Events.Models;
using Honua.Server.Enterprise.Events.Queue.Models;

namespace Honua.Server.Enterprise.Events.Queue.Repositories;

/// <summary>
/// Repository for geofence event queue operations
/// </summary>
public interface IGeofenceEventQueueRepository
{
    /// <summary>
    /// Enqueue a geofence event for delivery
    /// </summary>
    /// <param name="geofenceEvent">The geofence event to enqueue</param>
    /// <param name="deliveryTargets">Delivery targets (signalr, servicebus, etc.)</param>
    /// <param name="priority">Priority (higher = more urgent)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Queue item ID</returns>
    Task<Guid> EnqueueEventAsync(
        GeofenceEvent geofenceEvent,
        List<string>? deliveryTargets = null,
        int priority = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Poll for pending events ready for delivery
    /// </summary>
    /// <param name="batchSize">Maximum number of events to poll</param>
    /// <param name="tenantId">Optional tenant ID for multi-tenancy</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of queue items ready for delivery</returns>
    Task<List<GeofenceEventQueueItem>> PollPendingEventsAsync(
        int batchSize = 10,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark an event as successfully delivered
    /// </summary>
    /// <param name="queueItemId">Queue item ID</param>
    /// <param name="target">Delivery target</param>
    /// <param name="recipientCount">Number of recipients delivered to</param>
    /// <param name="latencyMs">Delivery latency in milliseconds</param>
    /// <param name="metadata">Additional metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkEventDeliveredAsync(
        Guid queueItemId,
        string target,
        int recipientCount,
        int latencyMs,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark an event delivery attempt as failed
    /// </summary>
    /// <param name="queueItemId">Queue item ID</param>
    /// <param name="target">Delivery target</param>
    /// <param name="errorMessage">Error message</param>
    /// <param name="metadata">Additional metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkEventFailedAsync(
        Guid queueItemId,
        string target,
        string errorMessage,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get queue metrics for monitoring
    /// </summary>
    /// <param name="tenantId">Optional tenant ID for multi-tenancy</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Queue metrics</returns>
    Task<QueueMetrics> GetQueueMetricsAsync(
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get delivery log for a queue item
    /// </summary>
    /// <param name="queueItemId">Queue item ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of delivery log entries</returns>
    Task<List<GeofenceEventDeliveryLog>> GetDeliveryLogAsync(
        Guid queueItemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get dead letter queue items
    /// </summary>
    /// <param name="limit">Maximum number of items to retrieve</param>
    /// <param name="offset">Offset for pagination</param>
    /// <param name="tenantId">Optional tenant ID for multi-tenancy</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of dead letter queue items</returns>
    Task<List<GeofenceEventQueueItem>> GetDeadLetterQueueAsync(
        int limit = 100,
        int offset = 0,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleanup completed queue items older than retention period
    /// </summary>
    /// <param name="retentionDays">Retention period in days</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of items deleted</returns>
    Task<int> CleanupCompletedItemsAsync(
        int retentionDays = 30,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replay geofence events for a given entity or geofence
    /// </summary>
    /// <param name="request">Replay request parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of geofence events</returns>
    Task<List<GeofenceEvent>> ReplayEventsAsync(
        EventReplayRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request parameters for event replay
/// </summary>
public class EventReplayRequest
{
    /// <summary>
    /// Filter by entity ID
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// Filter by geofence ID
    /// </summary>
    public Guid? GeofenceId { get; set; }

    /// <summary>
    /// Start time for replay
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow.AddHours(-24);

    /// <summary>
    /// End time for replay
    /// </summary>
    public DateTime EndTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Filter by event types
    /// </summary>
    public List<GeofenceEventType>? EventTypes { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public string? TenantId { get; set; }
}

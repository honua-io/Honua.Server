// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.Events.Queue.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Events.Queue.Health;

/// <summary>
/// Health check for geofence event queue
/// </summary>
public class GeoEventQueueHealthCheck : IHealthCheck
{
    private readonly IGeofenceEventQueueRepository _queueRepository;
    private readonly ILogger<GeoEventQueueHealthCheck> _logger;

    public GeoEventQueueHealthCheck(
        IGeofenceEventQueueRepository queueRepository,
        ILogger<GeoEventQueueHealthCheck> logger)
    {
        _queueRepository = queueRepository ?? throw new ArgumentNullException(nameof(queueRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metrics = await _queueRepository.GetQueueMetricsAsync(
                tenantId: null,
                cancellationToken);

            var data = new Dictionary<string, object>
            {
                { "pending_count", metrics.PendingCount },
                { "processing_count", metrics.ProcessingCount },
                { "completed_count", metrics.CompletedCount },
                { "dlq_count", metrics.DeadLetterCount },
                { "avg_queue_depth_seconds", metrics.AvgQueueDepthSeconds },
                { "avg_delivery_latency_ms", metrics.AvgDeliveryLatencyMs },
                { "success_rate_percent", metrics.SuccessRatePercent },
                { "oldest_pending_age_seconds", metrics.OldestPendingAgeSeconds ?? 0 }
            };

            // Determine health status based on metrics
            if (metrics.DeadLetterCount > 100)
            {
                return HealthCheckResult.Unhealthy(
                    $"High dead letter queue count: {metrics.DeadLetterCount}",
                    data: data);
            }

            if (metrics.PendingCount > 1000)
            {
                return HealthCheckResult.Degraded(
                    $"High pending count: {metrics.PendingCount}",
                    data: data);
            }

            if (metrics.OldestPendingAgeSeconds > 300) // 5 minutes
            {
                return HealthCheckResult.Degraded(
                    $"Oldest pending event is {metrics.OldestPendingAgeSeconds}s old",
                    data: data);
            }

            if (metrics.SuccessRatePercent < 95)
            {
                return HealthCheckResult.Degraded(
                    $"Low success rate: {metrics.SuccessRatePercent:F2}%",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"Queue is healthy. Pending: {metrics.PendingCount}, Success rate: {metrics.SuccessRatePercent:F2}%",
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking geofence event queue health");

            return HealthCheckResult.Unhealthy(
                "Failed to retrieve queue metrics",
                exception: ex);
        }
    }
}

/// <summary>
/// Extension methods for registering queue health checks
/// </summary>
public static class GeoEventQueueHealthCheckExtensions
{
    /// <summary>
    /// Add geofence event queue health check
    /// </summary>
    public static IHealthChecksBuilder AddGeoEventQueueHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "geoevent_queue",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.AddCheck<GeoEventQueueHealthCheck>(
            name,
            failureStatus ?? HealthStatus.Degraded,
            tags ?? new[] { "queue", "geofence", "events" });
    }
}

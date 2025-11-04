// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Honua.Server.Observability.HealthChecks;

/// <summary>
/// Health check for build queue processing status.
/// </summary>
public class QueueHealthCheck : IHealthCheck
{
    private readonly string _connectionString;
    private readonly int _maxQueueDepthWarning;
    private readonly TimeSpan _maxAgeWarning;

    public QueueHealthCheck(
        string connectionString,
        int maxQueueDepthWarning = 100,
        TimeSpan? maxAgeWarning = null)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _maxQueueDepthWarning = maxQueueDepthWarning;
        _maxAgeWarning = maxAgeWarning ?? TimeSpan.FromMinutes(30);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Check if queue table exists
            var queueTableExists = await connection.QuerySingleAsync<bool>(
                @"SELECT EXISTS (
                    SELECT FROM information_schema.tables
                    WHERE table_schema = 'public'
                    AND table_name = 'build_queue'
                )",
                cancellationToken);

            if (!queueTableExists)
            {
                return HealthCheckResult.Degraded(
                    "Build queue table does not exist",
                    data: new Dictionary<string, object>
                    {
                        { "table_exists", false }
                    });
            }

            // Get queue statistics
            var stats = await connection.QuerySingleAsync<QueueStats>(
                @"SELECT
                    COUNT(*) FILTER (WHERE status = 'pending') as pending_count,
                    COUNT(*) FILTER (WHERE status = 'processing') as processing_count,
                    COUNT(*) FILTER (WHERE status = 'completed') as completed_count,
                    COUNT(*) FILTER (WHERE status = 'failed') as failed_count,
                    MIN(created_at) FILTER (WHERE status = 'pending') as oldest_pending,
                    AVG(EXTRACT(EPOCH FROM (completed_at - created_at))) FILTER (WHERE status = 'completed' AND completed_at > NOW() - INTERVAL '1 hour') as avg_processing_time_seconds
                  FROM build_queue
                  WHERE created_at > NOW() - INTERVAL '24 hours'",
                cancellationToken);

            var data = new Dictionary<string, object>
            {
                { "pending_count", stats.PendingCount },
                { "processing_count", stats.ProcessingCount },
                { "completed_count", stats.CompletedCount },
                { "failed_count", stats.FailedCount }
            };

            if (stats.AvgProcessingTimeSeconds.HasValue)
            {
                data["avg_processing_time_seconds"] = stats.AvgProcessingTimeSeconds.Value;
            }

            // Check for stuck items
            var stuckItems = 0;
            if (stats.OldestPending.HasValue)
            {
                var age = DateTime.UtcNow - stats.OldestPending.Value;
                data["oldest_pending_age_seconds"] = age.TotalSeconds;

                if (age > _maxAgeWarning)
                {
                    stuckItems = await connection.QuerySingleAsync<int>(
                        @"SELECT COUNT(*) FROM build_queue
                          WHERE status = 'pending'
                          AND created_at < NOW() - @MaxAge",
                        new { MaxAge = _maxAgeWarning });

                    data["stuck_items"] = stuckItems;
                }
            }

            // Determine health status
            if (stats.PendingCount > _maxQueueDepthWarning)
            {
                return HealthCheckResult.Degraded(
                    $"Queue depth ({stats.PendingCount}) exceeds warning threshold ({_maxQueueDepthWarning})",
                    data: data);
            }

            if (stuckItems > 0)
            {
                return HealthCheckResult.Degraded(
                    $"{stuckItems} item(s) stuck in queue for more than {_maxAgeWarning.TotalMinutes} minutes",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"Queue processing normally. {stats.PendingCount} pending, {stats.ProcessingCount} processing",
                data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Queue health check failed",
                ex);
        }
    }

    private class QueueStats
    {
        public int PendingCount { get; set; }
        public int ProcessingCount { get; set; }
        public int CompletedCount { get; set; }
        public int FailedCount { get; set; }
        public DateTime? OldestPending { get; set; }
        public double? AvgProcessingTimeSeconds { get; set; }
    }
}

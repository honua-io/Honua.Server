// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Enterprise.Multitenancy;

/// <summary>
/// Tracks and meters tenant resource usage
/// </summary>
public interface ITenantUsageTracker
{
    Task<TenantUsage> GetCurrentUsageAsync(string tenantId, CancellationToken cancellationToken = default);
    Task RecordApiRequestAsync(string tenantId, string endpoint, int responseTimeMs, CancellationToken cancellationToken = default);
    Task RecordStorageUsageAsync(string tenantId, long bytesUsed, CancellationToken cancellationToken = default);
    Task RecordRasterProcessingAsync(string tenantId, int durationMinutes, CancellationToken cancellationToken = default);
    Task RecordVectorProcessingAsync(string tenantId, CancellationToken cancellationToken = default);
    Task RecordBuildAsync(string tenantId, CancellationToken cancellationToken = default);
    Task RecordExportAsync(string tenantId, long sizeBytes, CancellationToken cancellationToken = default);
}

public class TenantUsageTracker : ITenantUsageTracker
{
    private readonly string _connectionString;
    private readonly ILogger<TenantUsageTracker> _logger;

    /// <summary>
    /// Whitelist of allowed metric column names to prevent SQL injection
    /// </summary>
    private static readonly HashSet<string> AllowedMetricNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "api_requests",
        "storage_bytes",
        "raster_processing_minutes",
        "vector_processing_requests",
        "builds",
        "exports"
    };

    public TenantUsageTracker(string connectionString, ILogger<TenantUsageTracker> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<TenantUsage> GetCurrentUsageAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            SELECT
                COALESCE(SUM(api_requests), 0) as ApiRequests,
                COALESCE(SUM(storage_bytes), 0) as StorageBytes,
                COALESCE(SUM(raster_processing_minutes), 0) as RasterProcessingMinutes,
                COALESCE(SUM(vector_processing_requests), 0) as VectorProcessingRequests,
                COALESCE(SUM(builds), 0) as Builds
            FROM tenant_usage
            WHERE tenant_id = @TenantId
              AND period_start >= DATE_TRUNC('month', NOW())
              AND period_end <= DATE_TRUNC('month', NOW()) + INTERVAL '1 month'";

        var result = await connection.QueryFirstOrDefaultAsync<TenantUsage>(sql, new { TenantId = tenantId });
        return result ?? new TenantUsage { TenantId = tenantId };
    }

    public async Task RecordApiRequestAsync(string tenantId, string endpoint, int responseTimeMs, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            INSERT INTO tenant_usage_events (tenant_id, event_type, event_data, created_at)
            VALUES (@TenantId, 'api_request', @EventData::jsonb, NOW())",
            new
            {
                TenantId = tenantId,
                EventData = System.Text.Json.JsonSerializer.Serialize(new { endpoint, responseTimeMs })
            });

        // Update monthly aggregate
        await UpdateMonthlyAggregateAsync(connection, tenantId, "api_requests", 1);
    }

    public async Task RecordStorageUsageAsync(string tenantId, long bytesUsed, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await UpdateMonthlyAggregateAsync(connection, tenantId, "storage_bytes", bytesUsed, isAbsolute: true);
    }

    public async Task RecordRasterProcessingAsync(string tenantId, int durationMinutes, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            INSERT INTO tenant_usage_events (tenant_id, event_type, event_data, created_at)
            VALUES (@TenantId, 'raster_processing', @EventData::jsonb, NOW())",
            new
            {
                TenantId = tenantId,
                EventData = System.Text.Json.JsonSerializer.Serialize(new { durationMinutes })
            });

        await UpdateMonthlyAggregateAsync(connection, tenantId, "raster_processing_minutes", durationMinutes);
    }

    public async Task RecordVectorProcessingAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            INSERT INTO tenant_usage_events (tenant_id, event_type, created_at)
            VALUES (@TenantId, 'vector_processing', NOW())",
            new { TenantId = tenantId });

        await UpdateMonthlyAggregateAsync(connection, tenantId, "vector_processing_requests", 1);
    }

    public async Task RecordBuildAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            INSERT INTO tenant_usage_events (tenant_id, event_type, created_at)
            VALUES (@TenantId, 'build', NOW())",
            new { TenantId = tenantId });

        await UpdateMonthlyAggregateAsync(connection, tenantId, "builds", 1);
    }

    public async Task RecordExportAsync(string tenantId, long sizeBytes, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            INSERT INTO tenant_usage_events (tenant_id, event_type, event_data, created_at)
            VALUES (@TenantId, 'export', @EventData::jsonb, NOW())",
            new
            {
                TenantId = tenantId,
                EventData = System.Text.Json.JsonSerializer.Serialize(new { sizeBytes })
            });
    }

    private async Task UpdateMonthlyAggregateAsync(
        NpgsqlConnection connection,
        string tenantId,
        string metricName,
        long value,
        bool isAbsolute = false)
    {
        // Validate metric name against whitelist to prevent SQL injection
        if (!AllowedMetricNames.Contains(metricName))
        {
            _logger.LogWarning(
                "Invalid metric name attempted: {MetricName}. This may be a SQL injection attempt.",
                metricName);

            throw new ArgumentException(
                $"Invalid metric name '{metricName}'. Allowed metrics: {string.Join(", ", AllowedMetricNames)}",
                nameof(metricName));
        }

        var sql = isAbsolute
            ? @"
                INSERT INTO tenant_usage (tenant_id, period_start, period_end, " + metricName + @")
                VALUES (@TenantId, DATE_TRUNC('month', NOW()), DATE_TRUNC('month', NOW()) + INTERVAL '1 month', @Value)
                ON CONFLICT (tenant_id, period_start)
                DO UPDATE SET " + metricName + @" = @Value, updated_at = NOW()"
            : @"
                INSERT INTO tenant_usage (tenant_id, period_start, period_end, " + metricName + @")
                VALUES (@TenantId, DATE_TRUNC('month', NOW()), DATE_TRUNC('month', NOW()) + INTERVAL '1 month', @Value)
                ON CONFLICT (tenant_id, period_start)
                DO UPDATE SET " + metricName + @" = tenant_usage." + metricName + @" + @Value, updated_at = NOW()";

        await connection.ExecuteAsync(sql, new { TenantId = tenantId, Value = value });
    }
}

/// <summary>
/// Current usage for a tenant
/// </summary>
public class TenantUsage
{
    public string TenantId { get; set; } = string.Empty;
    public long ApiRequests { get; set; }
    public long StorageBytes { get; set; }
    public int RasterProcessingMinutes { get; set; }
    public long VectorProcessingRequests { get; set; }
    public int Builds { get; set; }
}

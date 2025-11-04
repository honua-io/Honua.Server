// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Npgsql;

namespace Honua.Server.Enterprise.Multitenancy;

/// <summary>
/// Service for querying and analyzing tenant usage data for admin dashboard
/// </summary>
public interface ITenantUsageAnalyticsService
{
    Task<List<TenantSummary>> GetActiveTenantSummariesAsync(CancellationToken cancellationToken = default);
    Task<TenantDetailedUsage?> GetTenantDetailedUsageAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<List<UsageHistoryPoint>> GetTenantUsageHistoryAsync(string tenantId, int months = 6, CancellationToken cancellationToken = default);
    Task<DashboardOverview> GetDashboardOverviewAsync(CancellationToken cancellationToken = default);
}

public class TenantUsageAnalyticsService : ITenantUsageAnalyticsService
{
    private readonly string _connectionString;

    public TenantUsageAnalyticsService(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<List<TenantSummary>> GetActiveTenantSummariesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            SELECT
                c.customer_id as TenantId,
                c.organization_name as OrganizationName,
                c.contact_email as ContactEmail,
                c.tier as Tier,
                c.subscription_status as SubscriptionStatus,
                l.trial_expires_at as TrialExpiresAt,
                c.created_at as CreatedAt,
                COALESCE(u.api_requests, 0) as ApiRequestsThisMonth,
                COALESCE(u.storage_bytes, 0) as StorageBytes,
                COALESCE(u.raster_processing_minutes, 0) as RasterProcessingMinutes
            FROM customers c
            LEFT JOIN licenses l ON c.customer_id = l.customer_id
            LEFT JOIN tenant_usage u ON c.customer_id = u.tenant_id
                AND u.period_start = DATE_TRUNC('month', NOW())
            WHERE c.deleted_at IS NULL
            ORDER BY c.created_at DESC";

        var result = await connection.QueryAsync<TenantSummary>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<TenantDetailedUsage?> GetTenantDetailedUsageAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            SELECT
                c.customer_id as TenantId,
                c.organization_name as OrganizationName,
                c.contact_email as ContactEmail,
                c.contact_name as ContactName,
                c.tier as Tier,
                c.subscription_status as SubscriptionStatus,
                l.trial_expires_at as TrialExpiresAt,
                c.created_at as CreatedAt,
                COALESCE(u.api_requests, 0) as ApiRequests,
                COALESCE(u.api_errors, 0) as ApiErrors,
                COALESCE(u.storage_bytes, 0) as StorageBytes,
                COALESCE(u.dataset_count, 0) as DatasetCount,
                COALESCE(u.raster_processing_minutes, 0) as RasterProcessingMinutes,
                COALESCE(u.vector_processing_requests, 0) as VectorProcessingRequests,
                COALESCE(u.builds, 0) as Builds,
                COALESCE(u.export_requests, 0) as ExportRequests,
                COALESCE(u.export_size_bytes, 0) as ExportSizeBytes
            FROM customers c
            LEFT JOIN licenses l ON c.customer_id = l.customer_id
            LEFT JOIN tenant_usage u ON c.customer_id = u.tenant_id
                AND u.period_start = DATE_TRUNC('month', NOW())
            WHERE c.customer_id = @TenantId AND c.deleted_at IS NULL";

        var result = await connection.QueryFirstOrDefaultAsync<TenantDetailedUsage>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return result;
    }

    public async Task<List<UsageHistoryPoint>> GetTenantUsageHistoryAsync(string tenantId, int months = 6, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            SELECT
                period_start as PeriodStart,
                api_requests as ApiRequests,
                storage_bytes as StorageBytes,
                raster_processing_minutes as RasterProcessingMinutes,
                vector_processing_requests as VectorProcessingRequests,
                builds as Builds
            FROM tenant_usage
            WHERE tenant_id = @TenantId
              AND period_start >= DATE_TRUNC('month', NOW() - INTERVAL '@Months months')
            ORDER BY period_start DESC";

        var result = await connection.QueryAsync<UsageHistoryPoint>(new CommandDefinition(sql, new { TenantId = tenantId, Months = months }, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<DashboardOverview> GetDashboardOverviewAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            SELECT
                COUNT(CASE WHEN subscription_status = 'active' THEN 1 END) as ActiveTenants,
                COUNT(CASE WHEN subscription_status = 'trial' THEN 1 END) as TrialTenants,
                COUNT(CASE WHEN subscription_status = 'trial' AND l.trial_expires_at < NOW() THEN 1 END) as ExpiredTrials,
                SUM(COALESCE(u.api_requests, 0)) as TotalApiRequests,
                SUM(COALESCE(u.storage_bytes, 0)) as TotalStorageBytes,
                SUM(COALESCE(u.raster_processing_minutes, 0)) as TotalRasterMinutes
            FROM customers c
            LEFT JOIN licenses l ON c.customer_id = l.customer_id
            LEFT JOIN tenant_usage u ON c.customer_id = u.tenant_id
                AND u.period_start = DATE_TRUNC('month', NOW())
            WHERE c.deleted_at IS NULL";

        var result = await connection.QueryFirstOrDefaultAsync<DashboardOverview>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result ?? new DashboardOverview();
    }
}

/// <summary>
/// Summary information about a tenant
/// </summary>
public class TenantSummary
{
    public string TenantId { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string Tier { get; set; } = string.Empty;
    public string SubscriptionStatus { get; set; } = string.Empty;
    public DateTimeOffset? TrialExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public long ApiRequestsThisMonth { get; set; }
    public long StorageBytes { get; set; }
    public int RasterProcessingMinutes { get; set; }

    public bool IsTrialExpired => TrialExpiresAt.HasValue && TrialExpiresAt.Value < DateTimeOffset.UtcNow;
    public string StatusDisplay => SubscriptionStatus switch
    {
        "trial" when IsTrialExpired => "Trial Expired",
        "trial" => $"Trial (expires {TrialExpiresAt:d})",
        "active" => "Active",
        "cancelled" => "Cancelled",
        _ => SubscriptionStatus
    };
}

/// <summary>
/// Detailed usage information for a specific tenant
/// </summary>
public class TenantDetailedUsage
{
    public string TenantId { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactName { get; set; }
    public string Tier { get; set; } = string.Empty;
    public string SubscriptionStatus { get; set; } = string.Empty;
    public DateTimeOffset? TrialExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Current month usage
    public long ApiRequests { get; set; }
    public int ApiErrors { get; set; }
    public long StorageBytes { get; set; }
    public int DatasetCount { get; set; }
    public int RasterProcessingMinutes { get; set; }
    public long VectorProcessingRequests { get; set; }
    public int Builds { get; set; }
    public int ExportRequests { get; set; }
    public long ExportSizeBytes { get; set; }

    public double ErrorRate => ApiRequests > 0 ? (double)ApiErrors / ApiRequests * 100 : 0;
}

/// <summary>
/// Historical usage data point
/// </summary>
public class UsageHistoryPoint
{
    public DateTimeOffset PeriodStart { get; set; }
    public long ApiRequests { get; set; }
    public long StorageBytes { get; set; }
    public int RasterProcessingMinutes { get; set; }
    public long VectorProcessingRequests { get; set; }
    public int Builds { get; set; }
}

/// <summary>
/// Dashboard overview statistics
/// </summary>
public class DashboardOverview
{
    public int ActiveTenants { get; set; }
    public int TrialTenants { get; set; }
    public int ExpiredTrials { get; set; }
    public long TotalApiRequests { get; set; }
    public long TotalStorageBytes { get; set; }
    public int TotalRasterMinutes { get; set; }

    public int TotalTenants => ActiveTenants + TrialTenants;
}

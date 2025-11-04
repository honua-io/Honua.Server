// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Multitenancy;

/// <summary>
/// Middleware that enforces tenant quotas and limits
/// Returns 429 (Too Many Requests) or 507 (Insufficient Storage) when limits exceeded
/// </summary>
public class QuotaEnforcementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<QuotaEnforcementMiddleware> _logger;

    public QuotaEnforcementMiddleware(RequestDelegate next, ILogger<QuotaEnforcementMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip for health checks and non-tenant routes
        if (IsExemptPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Get tenant context
        var tenantContext = context.GetTenantContext();
        if (tenantContext == null)
        {
            await _next(context);
            return;
        }

        // Get tenant quotas
        var quotas = TenantQuotas.FromTier(tenantContext.Tier);

        // Get usage tracker
        var usageTracker = context.RequestServices.GetRequiredService<ITenantUsageTracker>();
        var currentUsage = await usageTracker.GetCurrentUsageAsync(tenantContext.TenantId, context.RequestAborted);

        // Check API request quota
        if (quotas.MaxApiRequestsPerMonth.HasValue &&
            currentUsage.ApiRequests >= quotas.MaxApiRequestsPerMonth.Value)
        {
            _logger.LogWarning("Tenant {TenantId} exceeded API request quota: {Current}/{Max}",
                tenantContext.TenantId, currentUsage.ApiRequests, quotas.MaxApiRequestsPerMonth);

            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = GetSecondsUntilNextMonth().ToString();
            await context.Response.WriteAsJsonAsync(new
            {
                error = "quota_exceeded",
                message = "Monthly API request quota exceeded. Please upgrade your plan or wait until next month.",
                quota = quotas.MaxApiRequestsPerMonth,
                current = currentUsage.ApiRequests,
                resetAt = GetFirstDayOfNextMonth()
            });
            return;
        }

        // Check storage quota (for upload/import endpoints)
        if (IsStorageOperation(context.Request.Path) &&
            quotas.MaxStorageBytes.HasValue &&
            currentUsage.StorageBytes >= quotas.MaxStorageBytes.Value)
        {
            _logger.LogWarning("Tenant {TenantId} exceeded storage quota: {Current}/{Max} bytes",
                tenantContext.TenantId, currentUsage.StorageBytes, quotas.MaxStorageBytes);

            context.Response.StatusCode = 507; // Insufficient Storage
            await context.Response.WriteAsJsonAsync(new
            {
                error = "storage_quota_exceeded",
                message = "Storage quota exceeded. Please delete data or upgrade your plan.",
                quota = quotas.MaxStorageBytes,
                current = currentUsage.StorageBytes
            });
            return;
        }

        // Check raster processing quota
        if (IsRasterOperation(context.Request.Path) &&
            quotas.MaxRasterProcessingMinutesPerMonth.HasValue &&
            currentUsage.RasterProcessingMinutes >= quotas.MaxRasterProcessingMinutesPerMonth.Value)
        {
            _logger.LogWarning("Tenant {TenantId} exceeded raster processing quota: {Current}/{Max} minutes",
                tenantContext.TenantId, currentUsage.RasterProcessingMinutes, quotas.MaxRasterProcessingMinutesPerMonth);

            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = GetSecondsUntilNextMonth().ToString();
            await context.Response.WriteAsJsonAsync(new
            {
                error = "quota_exceeded",
                message = "Monthly raster processing quota exceeded. Please upgrade your plan.",
                quota = quotas.MaxRasterProcessingMinutesPerMonth,
                current = currentUsage.RasterProcessingMinutes,
                resetAt = GetFirstDayOfNextMonth()
            });
            return;
        }

        // Check vector processing quota
        if (IsVectorOperation(context.Request.Path) &&
            quotas.MaxVectorProcessingRequestsPerMonth.HasValue &&
            currentUsage.VectorProcessingRequests >= quotas.MaxVectorProcessingRequestsPerMonth.Value)
        {
            _logger.LogWarning("Tenant {TenantId} exceeded vector processing quota: {Current}/{Max}",
                tenantContext.TenantId, currentUsage.VectorProcessingRequests, quotas.MaxVectorProcessingRequestsPerMonth);

            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = GetSecondsUntilNextMonth().ToString();
            await context.Response.WriteAsJsonAsync(new
            {
                error = "quota_exceeded",
                message = "Monthly vector processing quota exceeded. Please upgrade your plan.",
                quota = quotas.MaxVectorProcessingRequestsPerMonth,
                current = currentUsage.VectorProcessingRequests,
                resetAt = GetFirstDayOfNextMonth()
            });
            return;
        }

        // Record API request
        var startTime = DateTime.UtcNow;

        // Continue pipeline
        await _next(context);

        // Record usage after response (fire and forget)
        var responseTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        _ = Task.Run(async () =>
        {
            try
            {
                await usageTracker.RecordApiRequestAsync(tenantContext.TenantId, context.Request.Path, responseTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording API usage for tenant {TenantId}", tenantContext.TenantId);
            }
        });
    }

    private bool IsExemptPath(PathString path)
    {
        var exemptPaths = new[] { "/health", "/metrics", "/swagger", "/.well-known" };
        foreach (var exempt in exemptPaths)
        {
            if (path.StartsWithSegments(exempt))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsStorageOperation(PathString path)
    {
        return path.StartsWithSegments("/api/upload") ||
               path.StartsWithSegments("/api/import") ||
               path.StartsWithSegments("/intake");
    }

    private bool IsRasterOperation(PathString path)
    {
        return path.StartsWithSegments("/raster");
    }

    private bool IsVectorOperation(PathString path)
    {
        return path.StartsWithSegments("/vector");
    }

    private DateTimeOffset GetFirstDayOfNextMonth()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
    }

    private int GetSecondsUntilNextMonth()
    {
        return (int)(GetFirstDayOfNextMonth() - DateTimeOffset.UtcNow).TotalSeconds;
    }
}

public static class QuotaEnforcementMiddlewareExtensions
{
    /// <summary>
    /// Adds quota enforcement middleware (Enterprise feature)
    /// Should be added after tenant resolution middleware
    /// </summary>
    public static IApplicationBuilder UseQuotaEnforcement(this IApplicationBuilder app)
    {
        return app.UseMiddleware<QuotaEnforcementMiddleware>();
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Honua.Server.Core.Security;
using Honua.Server.Enterprise.Multitenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.AuditLog;

/// <summary>
/// Middleware for automatically logging HTTP requests to the audit log
/// </summary>
public class AuditLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLogMiddleware> _logger;

    public AuditLogMiddleware(RequestDelegate next, ILogger<AuditLogMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context, IAuditLogService auditLogService, ITenantProvider? tenantProvider = null, TrustedProxyValidator? proxyValidator = null)
    {
        // Skip audit logging for certain paths
        if (ShouldSkipPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        Exception? exception = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            // SECURITY FIX: Log asynchronously with proper error tracking
            // We use Task.Run to avoid blocking the response, but we track the task
            // and log any failures to ensure audit events are not silently dropped
            var auditTask = Task.Run(async () =>
            {
                try
                {
                    await LogRequestAsync(context, auditLogService, tenantProvider, proxyValidator, stopwatch.ElapsedMilliseconds, exception);
                }
                catch (Exception ex)
                {
                    // Log the failure - in production, this should be sent to a monitoring system
                    _logger.LogError(ex, "CRITICAL: Failed to record audit log event for request {Method} {Path}",
                        context.Request.Method, context.Request.Path);
                }
            });

            // Track the task completion (but don't await it to avoid blocking the response)
            _ = auditTask.ContinueWith(task =>
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    _logger.LogError(task.Exception, "CRITICAL: Audit logging task faulted unexpectedly");
                }
            }, TaskScheduler.Default);
        }
    }

    private async Task LogRequestAsync(
        HttpContext context,
        IAuditLogService auditLogService,
        ITenantProvider? tenantProvider,
        TrustedProxyValidator? proxyValidator,
        long durationMs,
        Exception? exception)
    {
        // Get tenant
        Guid? tenantId = null;
        if (tenantProvider != null)
        {
            var tenant = await tenantProvider.GetCurrentTenantAsync(context, CancellationToken.None);
            if (tenant != null)
            {
                // Try to parse TenantId as Guid (for UUID-based tenants)
                if (Guid.TryParse(tenant.TenantId, out var parsedTenantId))
                {
                    tenantId = parsedTenantId;
                }
                // Try CustomerId as fallback
                else if (!string.IsNullOrEmpty(tenant.CustomerId) && Guid.TryParse(tenant.CustomerId, out var parsedCustomerId))
                {
                    tenantId = parsedCustomerId;
                }
            }
        }

        // Get user info
        Guid? userId = null;
        string? userIdentifier = null;

        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst("sub") ?? context.User.FindFirst("user_id");
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var parsedUserId))
            {
                userId = parsedUserId;
            }

            userIdentifier = context.User.FindFirst("email")?.Value
                          ?? context.User.FindFirst("name")?.Value
                          ?? context.User.Identity.Name;
        }

        // Determine category based on path
        var category = DetermineCategoryFromPath(context.Request.Path);

        // Determine action from HTTP method
        var action = context.Request.Method.ToLowerInvariant() switch
        {
            "get" => AuditAction.Read,
            "post" => AuditAction.Create,
            "put" or "patch" => AuditAction.Update,
            "delete" => AuditAction.Delete,
            _ => "http.request"
        };

        // Build audit event
        var auditEvent = AuditEventBuilder.Create()
            .WithCategory(category)
            .WithAction(action)
            .WithHttpContext(
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                durationMs)
            .WithSuccess(exception == null && context.Response.StatusCode < 400)
            .WithIpAddress(GetClientIpAddress(context, proxyValidator))
            .WithUserAgent(context.Request.Headers["User-Agent"].ToString())
            .WithTraceId(Activity.Current?.Id ?? context.TraceIdentifier);

        if (tenantId.HasValue)
        {
            auditEvent.WithTenant(tenantId.Value);
        }

        if (userId.HasValue && userIdentifier != null)
        {
            auditEvent.WithUser(userId.Value, userIdentifier);
        }

        // Add session ID if available
        if (context.Session != null && context.Session.IsAvailable)
        {
            auditEvent.WithSessionId(context.Session.Id);
        }

        // Extract resource info from path
        var (resourceType, resourceId) = ExtractResourceFromPath(context.Request.Path);
        if (resourceType != null)
        {
            auditEvent.WithResource(resourceType, resourceId ?? "");
        }

        // Add error info if failed
        if (exception != null)
        {
            auditEvent.WithError(exception.Message);
        }
        else if (context.Response.StatusCode >= 400)
        {
            auditEvent.WithError($"HTTP {context.Response.StatusCode}");
        }

        // Calculate risk score for suspicious activity
        var riskScore = CalculateRiskScore(context, exception);
        if (riskScore > 0)
        {
            auditEvent.WithRiskScore(riskScore);
        }

        // Add tags for filtering
        var tags = new System.Collections.Generic.List<string>();
        if (exception != null) tags.Add("error");
        if (context.Response.StatusCode >= 500) tags.Add("server-error");
        if (context.Response.StatusCode == 401) tags.Add("unauthorized");
        if (context.Response.StatusCode == 403) tags.Add("forbidden");
        if (riskScore >= 80) tags.Add("high-risk");

        if (tags.Count > 0)
        {
            auditEvent.WithTags(tags.ToArray());
        }

        await auditLogService.RecordAsync(auditEvent.Build());
    }

    private static bool ShouldSkipPath(PathString path)
    {
        // Skip health checks, metrics, static files
        var pathValue = path.Value?.ToLowerInvariant() ?? "";

        return pathValue.StartsWith("/health")
            || pathValue.StartsWith("/metrics")
            || pathValue.StartsWith("/swagger")
            || pathValue.StartsWith("/_framework")
            || pathValue.Contains("/css/")
            || pathValue.Contains("/js/")
            || pathValue.Contains("/img/");
    }

    private static string DetermineCategoryFromPath(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? "";

        if (pathValue.Contains("/auth/")) return AuditCategory.Authentication;
        if (pathValue.Contains("/admin/")) return AuditCategory.AdminAction;
        if (pathValue.Contains("/api/")) return AuditCategory.ApiRequest;
        if (pathValue.Contains("/saml/")) return AuditCategory.Authentication;

        return AuditCategory.ApiRequest;
    }

    private static (string? resourceType, string? resourceId) ExtractResourceFromPath(PathString path)
    {
        // Parse RESTful paths like /api/collections/{id}
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments == null || segments.Length < 2) return (null, null);

        // Look for resource type in path
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i] == "api" && i + 1 < segments.Length)
            {
                var resourceType = segments[i + 1];

                // Check if next segment is an ID
                if (i + 2 < segments.Length && Guid.TryParse(segments[i + 2], out _))
                {
                    return (resourceType, segments[i + 2]);
                }

                return (resourceType, null);
            }
        }

        return (null, null);
    }

    /// <summary>
    /// SECURITY FIX: Safely extracts client IP address with proper proxy validation.
    /// </summary>
    /// <remarks>
    /// VULNERABILITY FIX: Previously this method blindly trusted X-Forwarded-For and X-Real-IP headers
    /// from ANY caller, allowing IP spoofing in audit logs. This could be exploited to:
    /// - Hide attacker's real IP address in security logs
    /// - Bypass IP-based threat detection and rate limiting
    /// - Frame innocent parties by injecting their IPs into audit logs
    ///
    /// The fix validates that forwarded headers only come from configured trusted proxies.
    /// If no proxy validator is configured or the request doesn't come from a trusted proxy,
    /// we fall back to the direct connection IP which cannot be spoofed.
    ///
    /// Related CWE: CWE-290 (Authentication Bypass by Spoofing)
    /// </remarks>
    private static string GetClientIpAddress(HttpContext context, TrustedProxyValidator? proxyValidator)
    {
        // SECURITY: If TrustedProxyValidator is available, use it for validated IP extraction
        // This ensures X-Forwarded-For/X-Real-IP headers are only trusted from configured proxies
        if (proxyValidator != null)
        {
            return proxyValidator.GetClientIpAddress(context);
        }

        // SECURITY: No proxy validator configured - NEVER trust forwarded headers
        // They could be injected by any client to spoof their IP address
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp == null)
        {
            return "unknown";
        }

        // Log warning if someone is trying to inject forwarded headers without proper proxy configuration
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();

        if (!string.IsNullOrEmpty(forwardedFor) || !string.IsNullOrEmpty(realIp))
        {
            // Note: We can't use _logger here as this is a static method
            // The TrustedProxyValidator will log these security events when properly configured
            // For now, we silently ignore the headers and use the connection IP
        }

        return remoteIp.ToString();
    }

    private static int CalculateRiskScore(HttpContext context, Exception? exception)
    {
        int score = 0;

        // High number of failed auth attempts
        if (context.Request.Path.Value?.Contains("/auth/") == true && context.Response.StatusCode == 401)
        {
            score += 50;
        }

        // SQL injection attempt patterns in query string
        var query = context.Request.QueryString.Value?.ToLowerInvariant() ?? "";
        if (query.Contains("union select") || query.Contains("drop table") || query.Contains("exec("))
        {
            score += 90; // Very high risk
        }

        // XSS attempt patterns
        if (query.Contains("<script>") || query.Contains("javascript:"))
        {
            score += 85;
        }

        // Path traversal attempts
        if (context.Request.Path.Value?.Contains("../") == true)
        {
            score += 80;
        }

        // Exceptions indicate potential issues
        if (exception != null)
        {
            score += 30;
        }

        // Rate limiting: Multiple requests from same IP in short time (would need state tracking)
        // This is simplified - in production, use a rate limiting service

        return Math.Min(score, 100);
    }
}

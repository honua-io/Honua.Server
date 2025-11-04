// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Enterprise.Multitenancy;

/// <summary>
/// Middleware that extracts tenant context from subdomain and validates tenant
/// Enterprise feature for SaaS/multitenant deployments
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract tenant from subdomain or X-Tenant-Id header
        var tenantId = ExtractTenantId(context);

        if (!string.IsNullOrEmpty(tenantId))
        {
            // Resolve tenant context from database
            var tenantResolver = context.RequestServices.GetRequiredService<ITenantResolver>();
            var tenantContext = await tenantResolver.ResolveTenantAsync(tenantId, context.RequestAborted);

            if (tenantContext != null)
            {
                // Check if tenant is active
                if (!tenantContext.IsActive)
                {
                    _logger.LogWarning("Tenant {TenantId} is not active. Status: {Status}, Trial Expired: {TrialExpired}",
                        tenantId, tenantContext.SubscriptionStatus, tenantContext.IsTrialExpired);

                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "tenant_inactive",
                        message = tenantContext.IsTrialExpired
                            ? "Trial period has expired. Please upgrade to continue using Honua."
                            : "This account is not active. Please contact support.",
                        tenantId = tenantId
                    });
                    return;
                }

                // Store tenant context in HttpContext.Items
                context.Items["TenantContext"] = tenantContext;

                _logger.LogDebug("Resolved tenant {TenantId} ({OrganizationName}), Tier: {Tier}",
                    tenantId, tenantContext.OrganizationName, tenantContext.Tier);
            }
            else
            {
                _logger.LogWarning("Tenant {TenantId} not found in database", tenantId);

                context.Response.StatusCode = 404;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "tenant_not_found",
                    message = "The specified tenant does not exist.",
                    tenantId = tenantId
                });
                return;
            }
        }
        else
        {
            // No tenant ID - allow for non-tenant routes (health checks, metrics, etc.)
            _logger.LogDebug("No tenant ID found for request {Path}", context.Request.Path);
        }

        await _next(context);
    }

    private string? ExtractTenantId(HttpContext context)
    {
        // 1. Check X-Tenant-Id header (set by YARP)
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader))
        {
            var tenantId = tenantHeader.ToString();
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                return tenantId.ToLowerInvariant();
            }
        }

        // 2. Extract from subdomain (backup if header missing)
        var host = context.Request.Host.Host;
        if (!string.IsNullOrEmpty(host))
        {
            // Parse subdomain from host
            // Example: acme.honua.io → "acme"
            var parts = host.Split('.');
            if (parts.Length >= 3)
            {
                var subdomain = parts[0];

                // Exclude known non-tenant subdomains
                var excludedSubdomains = new[] { "www", "api", "intake", "orchestrator", "prometheus", "grafana", "admin" };
                if (!excludedSubdomains.Contains(subdomain.ToLowerInvariant()))
                {
                    return subdomain.ToLowerInvariant();
                }
            }
        }

        return null;
    }
}

/// <summary>
/// Extension methods for tenant middleware
/// </summary>
public static class TenantMiddlewareExtensions
{
    /// <summary>
    /// Adds tenant resolution middleware to the pipeline (Enterprise feature)
    /// </summary>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantMiddleware>();
    }

    /// <summary>
    /// Gets the current tenant context from HttpContext
    /// </summary>
    public static TenantContext? GetTenantContext(this HttpContext context)
    {
        return context.Items.TryGetValue("TenantContext", out var tenant)
            ? tenant as TenantContext
            : null;
    }

    /// <summary>
    /// Gets the current tenant ID (throws if not found)
    /// </summary>
    public static string GetRequiredTenantId(this HttpContext context)
    {
        var tenantContext = context.GetTenantContext();
        if (tenantContext == null)
        {
            throw new InvalidOperationException("Tenant context not found. Ensure TenantMiddleware is registered.");
        }
        return tenantContext.TenantId;
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading.Tasks;
using Honua.Server.Core.Features;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Middleware;

/// <summary>
/// Middleware that adds degradation status information to response headers.
/// </summary>
public sealed class DegradationStatusMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DegradationStatusMiddleware> _logger;

    public DegradationStatusMiddleware(
        RequestDelegate next,
        ILogger<DegradationStatusMiddleware> logger)
    {
        _next = Guard.NotNull(next);
        _logger = Guard.NotNull(logger);
    }

    public async Task InvokeAsync(HttpContext context, IFeatureManagementService featureManagement)
    {
        // Get all feature statuses
        var statuses = await featureManagement.GetAllFeatureStatusesAsync(context.RequestAborted);

        var degradedFeatures = statuses
            .Where(kvp => kvp.Value.IsDegraded || !kvp.Value.IsAvailable)
            .ToList();

        // Add overall service status header
        if (degradedFeatures.Any())
        {
            var hasUnavailable = degradedFeatures.Any(kvp => !kvp.Value.IsAvailable);
            context.Response.Headers["X-Service-Status"] = hasUnavailable ? "Degraded" : "Partial";

            // Add feature status details
            var featureStatusParts = degradedFeatures
                .Select(kvp =>
                {
                    var status = kvp.Value;
                    var state = status.IsAvailable ? "Degraded" : "Unavailable";
                    return $"{kvp.Key}={state}";
                });

            context.Response.Headers["X-Feature-Status"] = string.Join(",", featureStatusParts);

            _logger.LogDebug(
                "Service operating in degraded mode. Degraded features: {Count}",
                degradedFeatures.Count);
        }
        else
        {
            context.Response.Headers["X-Service-Status"] = "Healthy";
        }

        await _next(context);
    }
}

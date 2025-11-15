// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Honua.Server.Core.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Observability;

/// <summary>
/// Middleware that integrates SLI measurements with HTTP request processing.
/// </summary>
/// <remarks>
/// This middleware automatically tracks SLI metrics for all HTTP requests:
/// - Latency SLI: Request duration vs. configured thresholds
/// - Availability SLI: Success rate (non-5xx responses)
/// - Error Rate SLI: Server error rate (5xx only)
///
/// Works in conjunction with existing ApiMetrics infrastructure.
/// </remarks>
public sealed class SliIntegrationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SliIntegrationMiddleware> _logger;

    public SliIntegrationMiddleware(
        RequestDelegate next,
        ILogger<SliIntegrationMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context, ISliMetrics sliMetrics)
    {
        if (sliMetrics == null)
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var endpoint = context.Request.Path.Value;
        var method = context.Request.Method;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var duration = stopwatch.Elapsed;
            var statusCode = context.Response.StatusCode;

            try
            {
                // Record latency SLI
                sliMetrics.RecordLatency(duration, endpoint, method);

                // Record availability SLI
                sliMetrics.RecordAvailability(statusCode, endpoint, method);

                // Record error rate SLI (only for actual errors)
                if (statusCode >= 400)
                {
                    sliMetrics.RecordError(statusCode, endpoint, method);
                }
            }
            catch (Exception ex)
            {
                // Don't fail the request if SLI recording fails
                _logger.LogWarning(ex, "Failed to record SLI metrics for {Method} {Endpoint}", method, endpoint);
            }
        }
    }
}

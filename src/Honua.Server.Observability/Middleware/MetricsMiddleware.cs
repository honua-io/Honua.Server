// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Observability.Middleware;

/// <summary>
/// Middleware that collects HTTP request metrics.
/// </summary>
public class MetricsMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<MetricsMiddleware> logger;
    private readonly Counter<long> httpRequests;
    private readonly Histogram<double> httpRequestDuration;
    private readonly Counter<long> httpRequestErrors;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next request delegate.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="meterFactory">The meter factory.</param>
    public MetricsMiddleware(
        RequestDelegate next,
        ILogger<MetricsMiddleware> logger,
        IMeterFactory meterFactory)
    {
        this.next = next;
        this.logger = logger;

        var meter = meterFactory.Create("Honua.Http");

        this.httpRequests = meter.CreateCounter<long>(
            "http_requests_total",
            description: "Total number of HTTP requests");

        this.httpRequestDuration = meter.CreateHistogram<double>(
            "http_request_duration_seconds",
            unit: "s",
            description: "HTTP request duration in seconds");

        this.httpRequestErrors = meter.CreateCounter<long>(
            "http_request_errors_total",
            description: "Total number of HTTP request errors");
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var path = NormalizePath(context.Request.Path);

        try
        {
            await this.next(context);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Unhandled exception in HTTP request {Method} {Path}",
                context.Request.Method, path);

            this.httpRequestErrors.Add(1,
                new KeyValuePair<string, object?>("method", context.Request.Method),
                new KeyValuePair<string, object?>("path", path),
                new KeyValuePair<string, object?>("exception", ex.GetType().Name));

            throw;
        }
        finally
        {
            stopwatch.Stop();

            var statusCode = context.Response.StatusCode;
            var tags = new TagList
            {
                { "method", context.Request.Method },
                { "path", path },
                { "status", statusCode.ToString() },
                { "status_class", GetStatusClass(statusCode) },
            };

            this.httpRequests.Add(1, tags);
            this.httpRequestDuration.Record(stopwatch.Elapsed.TotalSeconds, tags);

            // Record errors for 4xx and 5xx responses
            if (statusCode >= 400)
            {
                this.httpRequestErrors.Add(1,
                    new KeyValuePair<string, object?>("method", context.Request.Method),
                    new KeyValuePair<string, object?>("path", path),
                    new KeyValuePair<string, object?>("status", statusCode.ToString()));
            }
        }
    }

    /// <summary>
    /// Normalizes paths to prevent high cardinality (e.g., /api/items/123 -> /api/items/{id}).
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized path.</returns>
    private static string NormalizePath(PathString path)
    {
        var pathValue = path.Value ?? "/";

        // Don't normalize metrics or health check endpoints
        if (pathValue.StartsWith("/metrics") || pathValue.StartsWith("/health"))
        {
            return pathValue;
        }

        // Replace GUIDs with {id}
        pathValue = System.Text.RegularExpressions.Regex.Replace(
            pathValue,
            @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
            "{id}",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Replace numeric IDs with {id}
        pathValue = System.Text.RegularExpressions.Regex.Replace(
            pathValue,
            @"/\d+(/|$)",
            "/{id}$1");

        return pathValue;
    }

    /// <summary>
    /// Gets the status code class (1xx, 2xx, 3xx, 4xx, 5xx).
    /// </summary>
    /// <param name="statusCode">The status code.</param>
    /// <returns>The status code class.</returns>
    private static string GetStatusClass(int statusCode)
    {
        return statusCode switch
        {
            >= 100 and < 200 => "1xx",
            >= 200 and < 300 => "2xx",
            >= 300 and < 400 => "3xx",
            >= 400 and < 500 => "4xx",
            >= 500 => "5xx",
            _ => "unknown",
        };
    }
}

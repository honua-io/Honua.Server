// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics;
using Microsoft.AspNetCore.Http.Extensions;

namespace Honua.Server.Host.Middleware;

/// <summary>
/// Middleware that logs slow database queries and HTTP requests.
/// Helps identify performance bottlenecks and optimization opportunities.
/// </summary>
public class SlowQueryLoggingMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<SlowQueryLoggingMiddleware> logger;
    private readonly SlowQueryOptions options;

    public SlowQueryLoggingMiddleware(
        RequestDelegate next,
        ILogger<SlowQueryLoggingMiddleware> logger,
        IConfiguration configuration)
    {
        this.next = next;
        this.logger = logger;
        this.options = new SlowQueryOptions(configuration);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!this.options.Enabled)
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestPath = context.Request.GetDisplayUrl();
        var method = context.Request.Method;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds;

            if (elapsed >= this.options.SlowRequestThresholdMs)
            {
                this.logger.LogWarning(
                    "Slow request detected: {Method} {Path} completed in {ElapsedMs}ms (threshold: {ThresholdMs}ms). Status: {StatusCode}",
                    method,
                    requestPath,
                    elapsed,
                    this.options.SlowRequestThresholdMs,
                    context.Response.StatusCode);

                // Log query string parameters if configured
                if (this.options.LogQueryParameters && context.Request.QueryString.HasValue)
                {
                    this.logger.LogWarning(
                        "Slow request query parameters: {QueryString}",
                        context.Request.QueryString.Value);
                }
            }
            else if (elapsed >= this.options.WarnRequestThresholdMs)
            {
                this.logger.LogInformation(
                    "Request completed in {ElapsedMs}ms: {Method} {Path} (Status: {StatusCode})",
                    elapsed,
                    method,
                    requestPath,
                    context.Response.StatusCode);
            }
        }
    }
}

/// <summary>
/// Configuration options for slow query logging.
/// </summary>
public class SlowQueryOptions
{
    public bool Enabled { get; set; }
    public int SlowRequestThresholdMs { get; set; }
    public int WarnRequestThresholdMs { get; set; }
    public bool LogQueryParameters { get; set; }

    public SlowQueryOptions(IConfiguration configuration)
    {
        Enabled = configuration.GetValue("SlowQueryLogging:Enabled", true);
        SlowRequestThresholdMs = configuration.GetValue("SlowQueryLogging:SlowRequestThresholdMs", 1000); // 1 second
        WarnRequestThresholdMs = configuration.GetValue("SlowQueryLogging:WarnRequestThresholdMs", 500); // 500ms
        LogQueryParameters = configuration.GetValue("SlowQueryLogging:LogQueryParameters", false);
    }
}

/// <summary>
/// Extension methods for adding slow query logging middleware.
/// </summary>
public static class SlowQueryLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseSlowQueryLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SlowQueryLoggingMiddleware>();
    }
}

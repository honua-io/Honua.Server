// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Observability.CorrelationId;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Honua.Server.Observability.Middleware;

/// <summary>
/// Middleware that ensures each request has a correlation ID for distributed tracing.
/// Supports both X-Correlation-ID and W3C Trace Context (traceparent) standards.
/// </summary>
/// <remarks>
/// <para>
/// This middleware implements a comprehensive correlation ID strategy:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>Extracts correlation ID from X-Correlation-ID header</description>
/// </item>
/// <item>
/// <description>Falls back to W3C Trace Context traceparent header (trace-id component)</description>
/// </item>
/// <item>
/// <description>Generates a new correlation ID if none provided</description>
/// </item>
/// <item>
/// <description>Stores correlation ID in HttpContext.Items for easy access throughout the pipeline</description>
/// </item>
/// <item>
/// <description>Adds correlation ID to response headers (X-Correlation-ID)</description>
/// </item>
/// <item>
/// <description>Enriches all log entries with correlation ID via Serilog LogContext</description>
/// </item>
/// <item>
/// <description>Supports W3C Trace Context for distributed tracing compatibility</description>
/// </item>
/// </list>
/// <para>
/// <strong>W3C Trace Context Support:</strong>
/// </para>
/// <para>
/// The middleware supports the W3C Trace Context standard (https://www.w3.org/TR/trace-context/).
/// When a traceparent header is present, the trace-id component is extracted and used as the correlation ID.
/// This ensures compatibility with distributed tracing systems like OpenTelemetry, Jaeger, and Zipkin.
/// </para>
/// <para>
/// <strong>Header Priority:</strong>
/// </para>
/// <list type="number">
/// <item>
/// <description>X-Correlation-ID (custom header for explicit correlation)</description>
/// </item>
/// <item>
/// <description>traceparent (W3C Trace Context standard)</description>
/// </item>
/// <item>
/// <description>Generated correlation ID (if none provided)</description>
/// </item>
/// </list>
/// </remarks>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<CorrelationIdMiddleware> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next request delegate.</param>
    /// <param name="logger">The logger.</param>
    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Extract or generate correlation ID
        var correlationId = GetOrCreateCorrelationId(context);

        // Store in HttpContext.Items for easy access throughout the pipeline
        CorrelationIdUtilities.SetCorrelationId(context, correlationId);

        // Add to response headers
        context.Response.OnStarting(() =>
        {
            // Always add X-Correlation-ID to response
            if (!context.Response.Headers.ContainsKey(CorrelationIdConstants.HeaderName))
            {
                context.Response.Headers.Append(CorrelationIdConstants.HeaderName, correlationId);
            }

            // If we generated a new W3C trace context, add traceparent to response
            if (!context.Request.Headers.ContainsKey(CorrelationIdConstants.W3CTraceParentHeader) &&
                !context.Response.Headers.ContainsKey(CorrelationIdConstants.W3CTraceParentHeader))
            {
                var traceParent = CorrelationIdUtilities.GenerateW3CTraceParent();
                context.Response.Headers.Append(CorrelationIdConstants.W3CTraceParentHeader, traceParent);
            }

            return Task.CompletedTask;
        });

        // Add to log context - all subsequent log entries will include this correlation ID
        using (LogContext.PushProperty(CorrelationIdConstants.LogPropertyName, correlationId))
        {
            this.logger.LogDebug(
                "Request started | Method: {Method} | Path: {Path} | CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                correlationId);

            await this.next(context);

            this.logger.LogDebug(
                "Request completed | Method: {Method} | Path: {Path} | StatusCode: {StatusCode} | CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                correlationId);
        }
    }

    /// <summary>
    /// Gets or creates a correlation ID for the current request.
    /// Checks headers first (X-Correlation-ID, then traceparent), generates new one if needed.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The correlation ID.</returns>
    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        // Try to extract from request headers
        var correlationId = CorrelationIdUtilities.ExtractCorrelationId(context.Request);

        if (!string.IsNullOrEmpty(correlationId))
        {
            // Normalize and validate the extracted correlation ID
            return CorrelationIdUtilities.NormalizeCorrelationId(correlationId);
        }

        // Generate a new correlation ID
        return CorrelationIdUtilities.GenerateCorrelationId();
    }
}

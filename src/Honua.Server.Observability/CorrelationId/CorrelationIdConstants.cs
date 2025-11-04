// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Observability.CorrelationId;

/// <summary>
/// Constants for correlation ID handling across the application.
/// </summary>
public static class CorrelationIdConstants
{
    /// <summary>
    /// Standard X-Correlation-ID header name for request/response correlation.
    /// </summary>
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>
    /// W3C Trace Context traceparent header name for distributed tracing.
    /// Format: {version}-{trace-id}-{parent-id}-{trace-flags}
    /// Example: 00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01
    /// </summary>
    public const string W3CTraceParentHeader = "traceparent";

    /// <summary>
    /// W3C Trace Context tracestate header name for vendor-specific trace data.
    /// </summary>
    public const string W3CTraceStateHeader = "tracestate";

    /// <summary>
    /// HttpContext.Items key for storing the correlation ID.
    /// </summary>
    public const string HttpContextItemsKey = "CorrelationId";

    /// <summary>
    /// Serilog log context property name for correlation ID.
    /// </summary>
    public const string LogPropertyName = "CorrelationId";

    /// <summary>
    /// Problem Details extension key for correlation ID.
    /// </summary>
    public const string ProblemDetailsExtensionKey = "correlationId";

    /// <summary>
    /// W3C Trace Context version.
    /// </summary>
    public const string W3CVersion = "00";

    /// <summary>
    /// Length of W3C Trace ID (32 hex characters = 128 bits).
    /// </summary>
    public const int W3CTraceIdLength = 32;

    /// <summary>
    /// Length of W3C Parent ID (16 hex characters = 64 bits).
    /// </summary>
    public const int W3CParentIdLength = 16;
}

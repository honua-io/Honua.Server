// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Observability.CorrelationId;

/// <summary>
/// Utilities for correlation ID generation and extraction.
/// Supports both custom X-Correlation-ID and W3C Trace Context standards.
/// </summary>
public static class CorrelationIdUtilities
{
    /// <summary>
    /// Generates a new correlation ID using a cryptographically secure random GUID.
    /// </summary>
    /// <returns>A 32-character hexadecimal string (GUID without hyphens).</returns>
    public static string GenerateCorrelationId()
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Generates a W3C Trace Context compliant trace ID (128-bit hex string).
    /// </summary>
    /// <returns>A 32-character hexadecimal trace ID.</returns>
    public static string GenerateW3CTraceId()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Generates a W3C Trace Context compliant parent ID (64-bit hex string).
    /// </summary>
    /// <returns>A 16-character hexadecimal parent ID.</returns>
    public static string GenerateW3CParentId()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Generates a full W3C Trace Context traceparent header value.
    /// Format: {version}-{trace-id}-{parent-id}-{trace-flags}
    /// </summary>
    /// <param name="sampled">Whether this trace should be sampled (default: true).</param>
    /// <returns>A W3C compliant traceparent header value.</returns>
    public static string GenerateW3CTraceParent(bool sampled = true)
    {
        var traceId = GenerateW3CTraceId();
        var parentId = GenerateW3CParentId();
        var flags = sampled ? "01" : "00";

        return $"{CorrelationIdConstants.W3CVersion}-{traceId}-{parentId}-{flags}";
    }

    /// <summary>
    /// Extracts correlation ID from HTTP request headers.
    /// Checks X-Correlation-ID first, then falls back to W3C traceparent trace-id.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <returns>The correlation ID if found, null otherwise.</returns>
    public static string? ExtractCorrelationId(HttpRequest request)
    {
        // First, check for X-Correlation-ID header
        if (request.Headers.TryGetValue(CorrelationIdConstants.HeaderName, out var correlationId) &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId.ToString();
        }

        // Fall back to W3C Trace Context traceparent
        if (request.Headers.TryGetValue(CorrelationIdConstants.W3CTraceParentHeader, out var traceParent) &&
            !string.IsNullOrWhiteSpace(traceParent))
        {
            var traceId = ExtractTraceIdFromTraceParent(traceParent!);
            if (!string.IsNullOrEmpty(traceId))
            {
                return traceId;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts the trace ID from a W3C traceparent header value.
    /// </summary>
    /// <param name="traceParent">The traceparent header value.</param>
    /// <returns>The trace ID if valid, null otherwise.</returns>
    public static string? ExtractTraceIdFromTraceParent(string traceParent)
    {
        if (string.IsNullOrWhiteSpace(traceParent))
        {
            return null;
        }

        // Format: {version}-{trace-id}-{parent-id}-{trace-flags}
        // Example: 00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01
        var parts = traceParent.Split('-');

        if (parts.Length != 4)
        {
            return null;
        }

        var version = parts[0];
        var traceId = parts[1];

        // Validate version
        if (version != CorrelationIdConstants.W3CVersion)
        {
            return null;
        }

        // Validate trace ID length and format
        if (traceId.Length != CorrelationIdConstants.W3CTraceIdLength ||
            !IsValidHexString(traceId))
        {
            return null;
        }

        return traceId;
    }

    /// <summary>
    /// Validates that a string contains only hexadecimal characters.
    /// </summary>
    /// <param name="value">The string to validate.</param>
    /// <returns>True if the string is valid hex, false otherwise.</returns>
    public static bool IsValidHexString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the correlation ID from HttpContext.Items.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <returns>The correlation ID if available, null otherwise.</returns>
    public static string? GetCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(CorrelationIdConstants.HttpContextItemsKey, out var correlationId))
        {
            return correlationId?.ToString();
        }

        return null;
    }

    /// <summary>
    /// Sets the correlation ID in HttpContext.Items.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="correlationId">The correlation ID to set.</param>
    public static void SetCorrelationId(HttpContext httpContext, string correlationId)
    {
        httpContext.Items[CorrelationIdConstants.HttpContextItemsKey] = correlationId;
    }

    /// <summary>
    /// Validates a correlation ID format (must be a valid GUID without hyphens or W3C trace ID).
    /// </summary>
    /// <param name="correlationId">The correlation ID to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValidCorrelationId(string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return false;
        }

        // Check if it's a valid GUID format (32 hex chars)
        if (correlationId.Length == 32 && IsValidHexString(correlationId))
        {
            return true;
        }

        // Check if it's a valid GUID with hyphens
        if (Guid.TryParse(correlationId, out _))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Normalizes a correlation ID to a standard format (32 hex chars, lowercase).
    /// </summary>
    /// <param name="correlationId">The correlation ID to normalize.</param>
    /// <returns>The normalized correlation ID, or a new one if invalid.</returns>
    public static string NormalizeCorrelationId(string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return GenerateCorrelationId();
        }

        // If it's a GUID with hyphens, remove them
        if (Guid.TryParse(correlationId, out var guid))
        {
            return guid.ToString("N").ToLowerInvariant();
        }

        // If it's already 32 hex chars, just normalize case
        if (correlationId.Length == 32 && IsValidHexString(correlationId))
        {
            return correlationId.ToLowerInvariant();
        }

        // If invalid, generate a new one
        return GenerateCorrelationId();
    }
}

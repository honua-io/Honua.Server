// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace Honua.Server.Enterprise.ETL.Resilience;

/// <summary>
/// Standard error categories for classification
/// </summary>
public enum ErrorCategory
{
    /// <summary>
    /// Transient errors that are likely to succeed on retry
    /// (network timeout, temporary unavailability)
    /// </summary>
    Transient,

    /// <summary>
    /// Data errors that won't be fixed by retry
    /// (invalid geometry, missing required field, parse errors)
    /// </summary>
    Data,

    /// <summary>
    /// Resource errors that may be fixed by retry with backoff
    /// (out of memory, disk full, rate limiting)
    /// </summary>
    Resource,

    /// <summary>
    /// Configuration errors that require manual intervention
    /// (missing API key, invalid credentials, misconfiguration)
    /// </summary>
    Configuration,

    /// <summary>
    /// External service errors (third-party API down, service unavailable)
    /// </summary>
    External,

    /// <summary>
    /// Logic errors (bugs in node implementation, null references)
    /// </summary>
    Logic,

    /// <summary>
    /// Unknown error category
    /// </summary>
    Unknown
}

/// <summary>
/// Utility for categorizing exceptions
/// </summary>
public static class ErrorCategorizer
{
    /// <summary>
    /// Categorize an exception into an error category
    /// </summary>
    public static ErrorCategory Categorize(Exception exception)
    {
        return exception switch
        {
            // Transient errors
            TimeoutException => ErrorCategory.Transient,
            HttpRequestException httpEx when IsTransientHttpError(httpEx) => ErrorCategory.Transient,

            // Resource errors
            OutOfMemoryException => ErrorCategory.Resource,
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.TooManyRequests => ErrorCategory.Resource,

            // Configuration errors
            UnauthorizedAccessException => ErrorCategory.Configuration,
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.Unauthorized => ErrorCategory.Configuration,
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.Forbidden => ErrorCategory.Configuration,
            ArgumentException => ErrorCategory.Configuration,
            InvalidOperationException => ErrorCategory.Configuration,

            // External errors
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.ServiceUnavailable => ErrorCategory.External,
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.BadGateway => ErrorCategory.External,
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.GatewayTimeout => ErrorCategory.External,

            // Data errors
            FormatException => ErrorCategory.Data,

            // Logic errors
            NullReferenceException => ErrorCategory.Logic,
            IndexOutOfRangeException => ErrorCategory.Logic,

            _ => ErrorCategory.Unknown
        };
    }

    /// <summary>
    /// Categorize by exception message patterns
    /// </summary>
    public static ErrorCategory CategorizeByMessage(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return ErrorCategory.Unknown;

        var lowerMessage = errorMessage.ToLowerInvariant();

        // Transient patterns
        if (ContainsAny(lowerMessage, TransientPatterns))
            return ErrorCategory.Transient;

        // Data patterns
        if (ContainsAny(lowerMessage, DataPatterns))
            return ErrorCategory.Data;

        // Resource patterns
        if (ContainsAny(lowerMessage, ResourcePatterns))
            return ErrorCategory.Resource;

        // Configuration patterns
        if (ContainsAny(lowerMessage, ConfigurationPatterns))
            return ErrorCategory.Configuration;

        // External patterns
        if (ContainsAny(lowerMessage, ExternalPatterns))
            return ErrorCategory.External;

        return ErrorCategory.Unknown;
    }

    /// <summary>
    /// Get suggested resolution for an error category
    /// </summary>
    public static string GetSuggestion(ErrorCategory category)
    {
        return category switch
        {
            ErrorCategory.Transient => "This is a temporary error. The system will automatically retry this operation.",
            ErrorCategory.Data => "This appears to be a data quality issue. Please check your input data for validity.",
            ErrorCategory.Resource => "System resources are constrained. Consider reducing data volume or increasing system capacity.",
            ErrorCategory.Configuration => "This appears to be a configuration issue. Please verify your settings and credentials.",
            ErrorCategory.External => "An external service is unavailable. Please try again later or check service status.",
            ErrorCategory.Logic => "This is an unexpected error. Please contact support with the error details.",
            _ => "Unable to determine the cause of this error. Please review the error details."
        };
    }

    private static bool IsTransientHttpError(HttpRequestException ex)
    {
        return ex.StatusCode is
            HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout or
            HttpStatusCode.BadGateway;
    }

    private static bool ContainsAny(string text, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (text.Contains(pattern))
                return true;
        }
        return false;
    }

    private static readonly string[] TransientPatterns = new[]
    {
        "timeout", "timed out", "connection reset", "connection refused",
        "temporarily unavailable", "try again", "network", "socket"
    };

    private static readonly string[] DataPatterns = new[]
    {
        "invalid geometry", "invalid format", "parse error", "missing field",
        "required field", "invalid data", "malformed", "corrupt"
    };

    private static readonly string[] ResourcePatterns = new[]
    {
        "out of memory", "disk full", "rate limit", "quota exceeded",
        "too many requests", "insufficient resources"
    };

    private static readonly string[] ConfigurationPatterns = new[]
    {
        "unauthorized", "forbidden", "api key", "authentication",
        "permission denied", "access denied", "invalid credentials"
    };

    private static readonly string[] ExternalPatterns = new[]
    {
        "service unavailable", "bad gateway", "gateway timeout",
        "external service", "third-party"
    };
}

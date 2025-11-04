// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Honua.Server.Observability.CorrelationId;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Middleware;

/// <summary>
/// Global exception handler middleware that converts exceptions to RFC 7807 Problem Details.
/// Handles both application exceptions and unexpected errors with appropriate logging and responses.
/// </summary>
public sealed class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        IWebHostEnvironment environment,
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = Guard.NotNull(next);
        _environment = Guard.NotNull(environment);
        _logger = Guard.NotNull(logger);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex).ConfigureAwait(false);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Determine if this is a transient failure
        var isTransient = exception is ITransientException transientEx && transientEx.IsTransient;

        // Log with appropriate severity
        LogException(exception, context, isTransient);

        // Create RFC 7807 Problem Details response
        var problemDetails = CreateProblemDetails(exception, context);

        // Set response headers
        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        // Write response
        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    private void LogException(Exception exception, HttpContext context, bool isTransient)
    {
        // Get correlation ID from context
        var correlationId = CorrelationIdUtilities.GetCorrelationId(context) ?? context.TraceIdentifier;

        var logMessage = "Unhandled exception occurred: {ExceptionType} - {Message} | Path: {Path} | Method: {Method} | CorrelationId: {CorrelationId}";
        var args = new object[]
        {
            exception.GetType().Name,
            exception.Message,
            context.Request.Path,
            context.Request.Method,
            correlationId
        };

        // Log with structured scope including correlation ID
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["ExceptionType"] = exception.GetType().Name,
            ["Path"] = context.Request.Path.ToString(),
            ["Method"] = context.Request.Method,
            ["CorrelationId"] = correlationId,
            ["IsTransient"] = isTransient
        }))
        {
            // Transient errors are warnings, permanent errors are errors
            if (isTransient)
            {
                _logger.LogWarning(exception, logMessage, args);
            }
            else if (exception is HonuaException)
            {
                // Application exceptions are logged as errors (with stack trace)
                _logger.LogError(exception, logMessage, args);
            }
            else
            {
                // Unexpected exceptions are critical
                _logger.LogCritical(exception, "CRITICAL: " + logMessage, args);
            }
        }
    }

    private ProblemDetails CreateProblemDetails(Exception exception, HttpContext context)
    {
        var isDevelopment = _environment.IsDevelopment();

        // Get correlation ID from context
        var correlationId = CorrelationIdUtilities.GetCorrelationId(context) ?? context.TraceIdentifier;

        // Determine status code and error details based on exception type
        var (statusCode, title, type) = MapExceptionToHttpStatus(exception);

        var problemDetails = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = statusCode,
            Instance = context.Request.Path,
            Detail = GetSafeDetail(exception, isDevelopment)
        };

        // Add correlation ID (primary identifier for tracing across systems)
        problemDetails.Extensions[CorrelationIdConstants.ProblemDetailsExtensionKey] = correlationId;

        // Add trace ID for backward compatibility
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        // Add timestamp
        problemDetails.Extensions["timestamp"] = DateTimeOffset.UtcNow;

        // Add transient flag if applicable
        if (exception is ITransientException transientEx)
        {
            problemDetails.Extensions["isTransient"] = transientEx.IsTransient;
        }

        // In development, add more debugging info
        if (isDevelopment)
        {
            problemDetails.Extensions["exceptionType"] = exception.GetType().FullName;
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;

            if (exception.InnerException != null)
            {
                problemDetails.Extensions["innerException"] = new
                {
                    type = exception.InnerException.GetType().FullName,
                    message = exception.InnerException.Message
                };
            }
        }

        // Add retry-after header for throttled requests
        if (exception is ServiceThrottledException throttled && throttled.RetryAfter.HasValue)
        {
            context.Response.Headers["Retry-After"] = ((int)throttled.RetryAfter.Value.TotalSeconds).ToString();
            problemDetails.Extensions["retryAfter"] = throttled.RetryAfter.Value.TotalSeconds;
        }

        // Add circuit breaker info
        if (exception is CircuitBreakerOpenException circuitBreaker)
        {
            problemDetails.Extensions["breakDuration"] = circuitBreaker.BreakDuration.TotalSeconds;
            problemDetails.Extensions["serviceName"] = circuitBreaker.ServiceName;
        }

        return problemDetails;
    }

    private static (int statusCode, string title, string type) MapExceptionToHttpStatus(Exception exception)
    {
        return exception switch
        {
            // 400 Bad Request - Client errors
            ArgumentException or ArgumentNullException =>
                (StatusCodes.Status400BadRequest, "Invalid Request", "https://tools.ietf.org/html/rfc7231#section-6.5.1"),

            FeatureValidationException or MetadataValidationException =>
                (StatusCodes.Status400BadRequest, "Validation Failed", "https://tools.ietf.org/html/rfc7231#section-6.5.1"),

            QueryFilterParseException or QueryOperationNotSupportedException =>
                (StatusCodes.Status400BadRequest, "Invalid Query", "https://tools.ietf.org/html/rfc7231#section-6.5.1"),

            UnsupportedRasterFormatException =>
                (StatusCodes.Status400BadRequest, "Unsupported Format", "https://tools.ietf.org/html/rfc7231#section-6.5.1"),

            // 401 Unauthorized
            UnauthorizedAccessException =>
                (StatusCodes.Status401Unauthorized, "Unauthorized", "https://tools.ietf.org/html/rfc7235#section-3.1"),

            // 404 Not Found
            FeatureNotFoundException or LayerNotFoundException or ServiceNotFoundException =>
                (StatusCodes.Status404NotFound, "Resource Not Found", "https://tools.ietf.org/html/rfc7231#section-6.5.4"),

            RasterSourceNotFoundException or CacheKeyNotFoundException =>
                (StatusCodes.Status404NotFound, "Resource Not Found", "https://tools.ietf.org/html/rfc7231#section-6.5.4"),

            // 429 Too Many Requests
            ServiceThrottledException =>
                (StatusCodes.Status429TooManyRequests, "Too Many Requests", "https://tools.ietf.org/html/rfc6585#section-4"),

            // 501 Not Implemented
            NotImplementedException =>
                (StatusCodes.Status501NotImplemented, "Not Implemented", "https://tools.ietf.org/html/rfc7231#section-6.6.2"),

            // 503 Service Unavailable - Transient failures
            ServiceUnavailableException or CircuitBreakerOpenException or CacheUnavailableException =>
                (StatusCodes.Status503ServiceUnavailable, "Service Unavailable", "https://tools.ietf.org/html/rfc7231#section-6.6.4"),

            ServiceTimeoutException =>
                (StatusCodes.Status504GatewayTimeout, "Gateway Timeout", "https://tools.ietf.org/html/rfc7231#section-6.6.5"),

            // 500 Internal Server Error - Everything else
            _ =>
                (StatusCodes.Status500InternalServerError, "Internal Server Error", "https://tools.ietf.org/html/rfc7231#section-6.6.1")
        };
    }

    private static string GetSafeDetail(Exception exception, bool isDevelopment)
    {
        // In production, return safe messages to avoid information disclosure
        if (!isDevelopment)
        {
            return exception switch
            {
                ArgumentException or ArgumentNullException => "The request contains invalid parameters.",
                FeatureNotFoundException => "The requested feature was not found.",
                LayerNotFoundException or ServiceNotFoundException => "The requested resource was not found.",
                QueryFilterParseException => "The query filter is invalid.",
                ServiceUnavailableException => "The service is temporarily unavailable. Please try again later.",
                CircuitBreakerOpenException => "The service is temporarily unavailable due to multiple failures. Please try again later.",
                ServiceTimeoutException => "The request timed out. Please try again.",
                ServiceThrottledException => "Too many requests. Please slow down and try again later.",
                CacheUnavailableException => "Cache service is temporarily unavailable.",
                NotImplementedException => "This feature is not yet available.",
                _ => "An unexpected error occurred while processing your request."
            };
        }

        // In development, include the actual exception message
        return exception.Message;
    }
}

/// <summary>
/// Extension methods for registering the global exception handler.
/// </summary>
public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}

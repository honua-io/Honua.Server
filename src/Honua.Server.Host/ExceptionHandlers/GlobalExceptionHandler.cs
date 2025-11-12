// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Utilities;
using Honua.Server.Observability.CorrelationId;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.ExceptionHandlers;

/// <summary>
/// Global exception handler that converts all unhandled exceptions to RFC 7807 Problem Details.
/// This handler catches exceptions from both MVC controllers and minimal API endpoints.
/// Implements .NET 8+ IExceptionHandler for modern exception handling pipeline.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> _logger,
        IHostEnvironment environment)
    {
        this._logger = Guard.NotNull(_logger);
        _environment = Guard.NotNull(environment);
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Determine if this is a transient failure
        var isTransient = exception is ITransientException transientEx && transientEx.IsTransient;

        // Log with appropriate severity based on exception type
        LogException(exception, httpContext, isTransient);

        // Map exception to HTTP status and create ProblemDetails
        var problemDetails = CreateProblemDetails(exception, httpContext);

        // Set response status and content type
        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        // Write response
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true; // Exception handled
    }

    private void LogException(Exception exception, HttpContext context, bool isTransient)
    {
        var endpoint = context.GetEndpoint();
        var endpointName = endpoint?.DisplayName ?? context.Request.Path;

        // Get correlation ID from context
        var correlationId = CorrelationIdUtilities.GetCorrelationId(context) ?? context.TraceIdentifier;

        // Create activity for distributed tracing
        var activity = Activity.Current;
        activity?.SetTag("exception.type", exception.GetType().FullName);
        activity?.SetTag("exception.message", exception.Message);
        activity?.SetTag("http.status_code", GetStatusCode(exception));
        activity?.SetTag("error", true);
        activity?.SetTag("correlation.id", correlationId);

        // Log with structured data including correlation ID
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["ExceptionType"] = exception.GetType().Name,
            ["Endpoint"] = endpointName,
            ["Path"] = context.Request.Path.ToString(),
            ["Method"] = context.Request.Method,
            ["TraceId"] = context.TraceIdentifier,
            ["CorrelationId"] = correlationId,
            ["IsTransient"] = isTransient
        }))
        {
            // Transient errors are warnings (expected, will retry)
            if (isTransient)
            {
                _logger.LogWarning(exception,
                    "Transient exception occurred: {ExceptionType} - {Message} | CorrelationId: {CorrelationId}",
                    exception.GetType().Name,
                    exception.Message,
                    correlationId);
            }
            // Application exceptions are errors (handled, but unexpected)
            else if (exception is HonuaException)
            {
                _logger.LogError(exception,
                    "Application exception occurred: {ExceptionType} - {Message} | CorrelationId: {CorrelationId}",
                    exception.GetType().Name,
                    exception.Message,
                    correlationId);
            }
            // Validation and argument exceptions are warnings (client error)
            else if (exception is ArgumentException or InvalidOperationException)
            {
                _logger.LogWarning(exception,
                    "Validation exception occurred: {ExceptionType} - {Message} | CorrelationId: {CorrelationId}",
                    exception.GetType().Name,
                    exception.Message,
                    correlationId);
            }
            // Unexpected system exceptions are critical
            else
            {
                _logger.LogCritical(exception,
                    "CRITICAL: Unhandled system exception: {ExceptionType} - {Message} | CorrelationId: {CorrelationId}",
                    exception.GetType().Name,
                    exception.Message,
                    correlationId);
            }
        }
    }

    private ProblemDetails CreateProblemDetails(Exception exception, HttpContext context)
    {
        var isDevelopment = _environment.IsDevelopment();

        // Get correlation ID from context
        var correlationId = CorrelationIdUtilities.GetCorrelationId(context) ?? context.TraceIdentifier;

        // Map exception to HTTP status
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

        // In development, add debugging information
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

        // Add Retry-After header for throttled requests
        if (exception is ServiceThrottledException throttled && throttled.RetryAfter.HasValue)
        {
            context.Response.Headers["Retry-After"] = ((int)throttled.RetryAfter.Value.TotalSeconds).ToString();
            problemDetails.Extensions["retryAfter"] = throttled.RetryAfter.Value.TotalSeconds;
        }

        // Add circuit breaker information
        if (exception is CircuitBreakerOpenException circuitBreaker)
        {
            problemDetails.Extensions["breakDuration"] = circuitBreaker.BreakDuration.TotalSeconds;
            problemDetails.Extensions["serviceName"] = circuitBreaker.ServiceName;
        }

        // Add service timeout information
        if (exception is ServiceTimeoutException timeout)
        {
            problemDetails.Extensions["timeout"] = timeout.Timeout.TotalSeconds;
            problemDetails.Extensions["serviceName"] = timeout.ServiceName;
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

            InvalidOperationException =>
                (StatusCodes.Status400BadRequest, "Invalid Operation", "https://tools.ietf.org/html/rfc7231#section-6.5.1"),

            // 401 Unauthorized
            UnauthorizedAccessException =>
                (StatusCodes.Status401Unauthorized, "Unauthorized", "https://tools.ietf.org/html/rfc7235#section-3.1"),

            // 404 Not Found
            FeatureNotFoundException or LayerNotFoundException or ServiceNotFoundException =>
                (StatusCodes.Status404NotFound, "Resource Not Found", "https://tools.ietf.org/html/rfc7231#section-6.5.4"),

            RasterSourceNotFoundException or CacheKeyNotFoundException =>
                (StatusCodes.Status404NotFound, "Resource Not Found", "https://tools.ietf.org/html/rfc7231#section-6.5.4"),

            MetadataNotFoundException =>
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

            // 504 Gateway Timeout
            ServiceTimeoutException =>
                (StatusCodes.Status504GatewayTimeout, "Gateway Timeout", "https://tools.ietf.org/html/rfc7231#section-6.6.5"),

            // 503 Service Unavailable - Alert persistence failures
            _ when exception.GetType().Name == "AlertPersistenceException" =>
                (StatusCodes.Status503ServiceUnavailable, "Service Unavailable", "https://tools.ietf.org/html/rfc7231#section-6.6.4"),

            // 500 Internal Server Error - Everything else
            _ =>
                (StatusCodes.Status500InternalServerError, "Internal Server Error", "https://tools.ietf.org/html/rfc7231#section-6.6.1")
        };
    }

    private static int GetStatusCode(Exception exception)
    {
        var (statusCode, _, _) = MapExceptionToHttpStatus(exception);
        return statusCode;
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
                MetadataNotFoundException => "The requested metadata was not found.",
                QueryFilterParseException => "The query filter is invalid.",
                QueryOperationNotSupportedException => "The requested query operation is not supported.",
                ServiceUnavailableException => "The service is temporarily unavailable. Please try again later.",
                CircuitBreakerOpenException => "The service is temporarily unavailable due to multiple failures. Please try again later.",
                ServiceTimeoutException => "The request timed out. Please try again.",
                ServiceThrottledException => "Too many requests. Please slow down and try again later.",
                CacheUnavailableException => "Cache service is temporarily unavailable.",
                CacheKeyNotFoundException => "The requested cached item was not found.",
                RasterSourceNotFoundException => "The requested raster source was not found.",
                UnsupportedRasterFormatException => "The raster format is not supported.",
                NotImplementedException => "This feature is not yet available.",
                InvalidOperationException => "The operation is invalid in the current state.",
                UnauthorizedAccessException => "You do not have permission to access this resource.",
                _ when exception.GetType().Name == "AlertPersistenceException" => "Alert persistence service is temporarily unavailable.",
                _ => "An unexpected error occurred while processing your request."
            };
        }

        // In development, include the actual exception message for debugging
        return exception.Message;
    }
}

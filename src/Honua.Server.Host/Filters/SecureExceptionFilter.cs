// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Validation;
using Honua.Server.Observability.CorrelationId;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Filters;

/// <summary>
/// Global exception filter that provides secure, consistent exception handling across all API endpoints.
/// </summary>
/// <remarks>
/// <para>
/// This filter implements the unified error handling architecture by:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>Catching all unhandled exceptions before they reach the client</description>
/// </item>
/// <item>
/// <description>Logging full exception details with structured logging for diagnostics</description>
/// </item>
/// <item>
/// <description>Logging to security audit for sensitive endpoints</description>
/// </item>
/// <item>
/// <description>Returning sanitized ProblemDetails responses that prevent information leakage</description>
/// </item>
/// <item>
/// <description>Environment-aware responses (verbose in Development, sanitized in Production)</description>
/// </item>
/// <item>
/// <description>Automatic request correlation via requestId for troubleshooting</description>
/// </item>
/// </list>
/// <para>
/// <strong>Exception Type Mapping:</strong>
/// </para>
/// <list type="table">
/// <listheader>
/// <term>Exception Type</term>
/// <description>HTTP Status</description>
/// </listheader>
/// <item>
/// <term><see cref="ValidationException"/></term>
/// <description>400 Bad Request with validation error details</description>
/// </item>
/// <item>
/// <term><see cref="UnauthorizedAccessException"/></term>
/// <description>401 Unauthorized</description>
/// </item>
/// <item>
/// <term><see cref="ArgumentException"/></term>
/// <description>400 Bad Request with safe error message</description>
/// </item>
/// <item>
/// <term><see cref="InvalidOperationException"/></term>
/// <description>400 Bad Request with safe error message</description>
/// </item>
/// <item>
/// <term>All other exceptions</term>
/// <description>500 Internal Server Error with generic message</description>
/// </item>
/// </list>
/// <para>
/// <strong>Security Considerations:</strong>
/// </para>
/// <list type="bullet">
/// <item>
/// <description>Exception messages are never exposed directly to prevent information disclosure</description>
/// </item>
/// <item>
/// <description>Stack traces are logged server-side but never sent to clients</description>
/// </item>
/// <item>
/// <description>Production environments receive minimal error details</description>
/// </item>
/// <item>
/// <description>All exceptions in admin/auth endpoints trigger security audit logs</description>
/// </item>
/// </list>
/// </remarks>
/// <example>
/// Register in Program.cs or Startup.cs:
/// <code>
/// services.AddControllers(options =>
/// {
///     options.Filters.Add&lt;SecureExceptionFilter&gt;();
/// });
/// </code>
/// </example>
public sealed class SecureExceptionFilter : IExceptionFilter
{
    private readonly ILogger<SecureExceptionFilter> logger;
    private readonly IHostEnvironment environment;
    private readonly ISecurityAuditLogger auditLogger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureExceptionFilter"/> class.
    /// </summary>
    /// <param name="logger">Logger for structured exception logging.</param>
    /// <param name="environment">Host environment to determine Development vs Production behavior.</param>
    /// <param name="auditLogger">Security audit logger for sensitive operations.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter is null.
    /// </exception>
    public SecureExceptionFilter(
        ILogger<SecureExceptionFilter> logger,
        IHostEnvironment environment,
        ISecurityAuditLogger auditLogger)
    {
        this.logger = Guard.NotNull(logger);
        this.environment = Guard.NotNull(environment);
        this.auditLogger = Guard.NotNull(auditLogger);
    }

    /// <summary>
    /// Called when an unhandled exception occurs during request processing.
    /// </summary>
    /// <param name="context">The exception context containing exception details and HTTP context.</param>
    /// <remarks>
    /// This method performs the following operations:
    /// <list type="number">
    /// <item>
    /// <description>Logs the full exception with structured logging including controller, action, and request ID</description>
    /// </item>
    /// <item>
    /// <description>Logs to security audit if the endpoint is sensitive (admin, auth, or mutation operations)</description>
    /// </item>
    /// <item>
    /// <description>Maps the exception to an appropriate HTTP status code and ProblemDetails response</description>
    /// </item>
    /// <item>
    /// <description>Sanitizes error messages based on exception type and environment</description>
    /// </item>
    /// <item>
    /// <description>Marks the exception as handled to prevent further propagation</description>
    /// </item>
    /// </list>
    /// </remarks>
    public void OnException(ExceptionContext context)
    {
        var exception = context.Exception;
        var requestId = context.HttpContext.TraceIdentifier;
        var controller = context.RouteData.Values["controller"]?.ToString() ?? "Unknown";
        var action = context.RouteData.Values["action"]?.ToString() ?? "Unknown";

        // Get correlation ID from context
        var correlationId = CorrelationIdUtilities.GetCorrelationId(context.HttpContext) ?? requestId;

        // Structured logging with full details for server-side diagnostics including correlation ID
        using (this.logger.BeginScope(new Dictionary<string, object>
        {
            ["Controller"] = controller,
            ["Action"] = action,
            ["RequestId"] = requestId,
            ["CorrelationId"] = correlationId,
            ["ExceptionType"] = exception.GetType().Name
        }))
        {
            this.logger.LogError(exception,
                "Unhandled exception in {Controller}.{Action} [RequestId: {RequestId}] [CorrelationId: {CorrelationId}]",
                controller,
                action,
                requestId,
                correlationId);
        }

        // Security audit for sensitive operations
        if (IsSensitiveEndpoint(context))
        {
            var username = context.HttpContext.User?.Identity?.Name ?? "Anonymous";
            var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString();

            this.auditLogger.LogSuspiciousActivity(
                activityType: "UnhandledException",
                username: username,
                ipAddress: ipAddress,
                details: $"Exception in {controller}.{action}: {exception.GetType().Name} [CorrelationId: {correlationId}]");
        }

        // Create appropriate ProblemDetails response based on exception type
        var problemDetails = CreateProblemDetailsForException(exception, requestId, correlationId);

        context.Result = new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };

        context.ExceptionHandled = true;
    }

    /// <summary>
    /// Creates a ProblemDetails response based on the exception type.
    /// </summary>
    /// <param name="exception">The exception to convert.</param>
    /// <param name="requestId">The request trace ID.</param>
    /// <param name="correlationId">The correlation ID for distributed tracing.</param>
    /// <returns>A ProblemDetails object with appropriate status code and sanitized message.</returns>
    private ProblemDetails CreateProblemDetailsForException(Exception exception, string requestId, string correlationId)
    {
        return exception switch
        {
            ValidationException validationEx => CreateValidationProblemDetails(validationEx, requestId, correlationId),
            UnauthorizedAccessException => CreateUnauthorizedProblemDetails(requestId, correlationId),
            ArgumentException argEx => CreateBadRequestProblemDetails(argEx.Message, requestId, correlationId),
            InvalidOperationException opEx => CreateBadRequestProblemDetails(opEx.Message, requestId, correlationId),
            _ => CreateGenericProblemDetails(exception, requestId, correlationId)
        };
    }

    /// <summary>
    /// Creates a ValidationProblemDetails response for validation failures.
    /// </summary>
    /// <param name="validationException">The validation exception containing error details.</param>
    /// <param name="requestId">The request trace ID.</param>
    /// <param name="correlationId">The correlation ID for distributed tracing.</param>
    /// <returns>A ValidationProblemDetails with 400 status and validation error details.</returns>
    private ValidationProblemDetails CreateValidationProblemDetails(
        ValidationException validationException,
        string requestId,
        string correlationId)
    {
        var problemDetails = new ValidationProblemDetails(validationException.Errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred",
            Detail = this.environment.IsDevelopment()
                ? "Please correct the validation errors and try again."
                : null,
            Instance = requestId,
        };

        problemDetails.Extensions[CorrelationIdConstants.ProblemDetailsExtensionKey] = correlationId;
        problemDetails.Extensions["requestId"] = requestId;
        problemDetails.Extensions["timestamp"] = DateTimeOffset.UtcNow;

        return problemDetails;
    }

    /// <summary>
    /// Creates a ProblemDetails response for unauthorized access attempts.
    /// </summary>
    /// <param name="requestId">The request trace ID.</param>
    /// <param name="correlationId">The correlation ID for distributed tracing.</param>
    /// <returns>A ProblemDetails with 401 status.</returns>
    private ProblemDetails CreateUnauthorizedProblemDetails(string requestId, string correlationId)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "Authentication is required to access this resource.",
            Instance = requestId,
            Extensions =
            {
                [CorrelationIdConstants.ProblemDetailsExtensionKey] = correlationId,
                ["requestId"] = requestId,
                ["timestamp"] = DateTimeOffset.UtcNow
            }
        };
    }

    /// <summary>
    /// Creates a ProblemDetails response for bad request errors (ArgumentException, InvalidOperationException).
    /// </summary>
    /// <param name="message">The exception message (sanitized before exposure).</param>
    /// <param name="requestId">The request trace ID.</param>
    /// <param name="correlationId">The correlation ID for distributed tracing.</param>
    /// <returns>A ProblemDetails with 400 status.</returns>
    /// <remarks>
    /// The message is used as-is for ArgumentException and InvalidOperationException because these
    /// are expected to contain safe, user-facing messages. If the message contains sensitive data,
    /// use a different exception type.
    /// </remarks>
    private ProblemDetails CreateBadRequestProblemDetails(string message, string requestId, string correlationId)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = SanitizeMessage(message),
            Instance = requestId,
            Extensions =
            {
                [CorrelationIdConstants.ProblemDetailsExtensionKey] = correlationId,
                ["requestId"] = requestId,
                ["timestamp"] = DateTimeOffset.UtcNow
            }
        };
    }

    /// <summary>
    /// Creates a generic ProblemDetails response for unexpected errors.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="requestId">The request trace ID.</param>
    /// <param name="correlationId">The correlation ID for distributed tracing.</param>
    /// <returns>A ProblemDetails with 500 status.</returns>
    /// <remarks>
    /// In Development environment, provides a hint to check server logs.
    /// In Production environment, provides only a generic error message to prevent information disclosure.
    /// </remarks>
    private ProblemDetails CreateGenericProblemDetails(Exception exception, string requestId, string correlationId)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred while processing your request",
            Detail = this.environment.IsDevelopment()
                ? "An unexpected error occurred. Check server logs for details."
                : null, // Production: no hint about server internals
            Instance = requestId,
            Extensions =
            {
                [CorrelationIdConstants.ProblemDetailsExtensionKey] = correlationId,
                ["requestId"] = requestId,
                ["timestamp"] = DateTimeOffset.UtcNow,
                // In development, include exception type to help with debugging
                ["exceptionType"] = this.environment.IsDevelopment()
                    ? exception.GetType().Name
                    : null
            }
        };
    }

    /// <summary>
    /// Sanitizes an error message by removing potentially sensitive information.
    /// </summary>
    /// <param name="message">The message to sanitize.</param>
    /// <returns>A sanitized message safe for client exposure.</returns>
    /// <remarks>
    /// This method removes common patterns that might leak sensitive information:
    /// <list type="bullet">
    /// <item><description>File paths (C:\, /home/, /var/)</description></item>
    /// <item><description>Connection strings</description></item>
    /// <item><description>SQL statements</description></item>
    /// <item><description>Stack trace fragments</description></item>
    /// </list>
    /// </remarks>
    private static string SanitizeMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "An error occurred.";
        }

        // Remove common patterns that might leak sensitive information
        var sanitized = message;

        // Remove file paths
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"[A-Za-z]:\\[^\s]+|/(?:home|var|usr|opt)/[^\s]+",
            "[PATH_REDACTED]");

        // Remove potential connection strings
        if (sanitized.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
            sanitized.Contains("Database=", StringComparison.OrdinalIgnoreCase) ||
            sanitized.Contains("Password=", StringComparison.OrdinalIgnoreCase))
        {
            return "A configuration error occurred.";
        }

        // Remove potential SQL
        if (sanitized.Contains("SELECT ", StringComparison.OrdinalIgnoreCase) ||
            sanitized.Contains("INSERT ", StringComparison.OrdinalIgnoreCase) ||
            sanitized.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase) ||
            sanitized.Contains("DELETE ", StringComparison.OrdinalIgnoreCase))
        {
            return "A database error occurred.";
        }

        // Remove stack trace fragments (lines starting with "at ")
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"\s+at\s+[^\n]+",
            "");

        return sanitized.Trim();
    }

    /// <summary>
    /// Determines if the current endpoint is sensitive and requires security audit logging.
    /// </summary>
    /// <param name="context">The exception context containing route information.</param>
    /// <returns>True if the endpoint is sensitive; otherwise, false.</returns>
    /// <remarks>
    /// Sensitive endpoints include:
    /// <list type="bullet">
    /// <item><description>All admin endpoints (/admin/*)</description></item>
    /// <item><description>Authentication endpoints (controllers with "Auth" in the name)</description></item>
    /// <item><description>User management endpoints (controllers with "User" in the name)</description></item>
    /// <item><description>Mutation operations (actions with Create, Update, Delete, or Post in the name)</description></item>
    /// </list>
    /// </remarks>
    private static bool IsSensitiveEndpoint(ExceptionContext context)
    {
        var controller = context.RouteData.Values["controller"]?.ToString() ?? string.Empty;
        var action = context.RouteData.Values["action"]?.ToString() ?? string.Empty;
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;

        // Admin endpoints
        if (path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Authentication/authorization controllers
        if (controller.Contains("Auth", StringComparison.OrdinalIgnoreCase) ||
            controller.Contains("User", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Mutation operations (Create, Update, Delete, Post actions)
        if (action.Contains("Create", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Post", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Put", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Patch", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}

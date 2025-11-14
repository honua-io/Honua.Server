// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Performance;
using Honua.Server.Host.Utilities;
using Honua.Server.Observability.CorrelationId;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Middleware;

/// <summary>
/// Handles exceptions and sanitizes error responses to prevent information disclosure.
/// </summary>
public sealed class SecureExceptionHandlerMiddleware
{
    private readonly RequestDelegate next;
    private readonly IWebHostEnvironment environment;
    private readonly ILogger<SecureExceptionHandlerMiddleware> logger;

    public SecureExceptionHandlerMiddleware(
        RequestDelegate next,
        IWebHostEnvironment environment,
        ILogger<SecureExceptionHandlerMiddleware> logger)
    {
        this.next = Guard.NotNull(next);
        this.environment = Guard.NotNull(environment);
        this.logger = Guard.NotNull(logger);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await this.next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex).ConfigureAwait(false);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Get correlation ID from context
        var correlationId = CorrelationIdUtilities.GetCorrelationId(context) ?? context.TraceIdentifier;

        // Log full exception details with correlation ID
        using (this.logger.BeginScope(new Dictionary<string, object>
        {
            ["ExceptionType"] = exception.GetType().Name,
            ["CorrelationId"] = correlationId
        }))
        {
            this.logger.LogError(exception, "Unhandled exception occurred: {Message} | CorrelationId: {CorrelationId}",
                exception.Message, correlationId);
        }

        // Determine status code
        var statusCode = exception switch
        {
            ArgumentException or ArgumentNullException => HttpStatusCode.BadRequest,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            InvalidOperationException => HttpStatusCode.BadRequest,
            NotImplementedException => HttpStatusCode.NotImplemented,
            _ => HttpStatusCode.InternalServerError
        };

        // Create sanitized error response
        var response = CreateErrorResponse(exception, statusCode, correlationId);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonSerializerOptionsRegistry.Web));
    }

    private object CreateErrorResponse(Exception exception, HttpStatusCode statusCode, string correlationId)
    {
        var isDevelopment = this.environment.IsDevelopment();

        // In production: sanitized error messages only
        // In development: include more details for debugging
        return new
        {
            error = new
            {
                code = statusCode.ToString(),
                message = GetSafeErrorMessage(exception, isDevelopment),
                correlationId = correlationId,
                timestamp = DateTimeOffset.UtcNow,
                // Only include details in development
                details = isDevelopment ? exception.Message : null,
                type = isDevelopment ? exception.GetType().Name : null,
                // Never include stack traces in any environment through API
                // (they're logged server-side for investigation)
            }
        };
    }

    private static string GetSafeErrorMessage(Exception exception, bool isDevelopment)
    {
        // In production, return generic messages to avoid information disclosure
        if (!isDevelopment)
        {
            return exception switch
            {
                ArgumentException or ArgumentNullException => "Invalid request parameters.",
                UnauthorizedAccessException => "Access denied.",
                InvalidOperationException => "The requested operation could not be completed.",
                NotImplementedException => "This feature is not yet available.",
                _ => "An error occurred while processing your request."
            };
        }

        // In development, include the actual message
        return exception.Message;
    }
}

/// <summary>
/// Extension methods for registering the secure exception handler.
/// </summary>
public static class SecureExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseSecureExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecureExceptionHandlerMiddleware>();
    }
}

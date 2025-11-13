// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Validation;

/// <summary>
/// Middleware that catches validation exceptions and converts them to RFC 7807 Problem Details responses.
/// </summary>
public sealed class ValidationMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<ValidationMiddleware> logger;

    public ValidationMiddleware(RequestDelegate next, ILogger<ValidationMiddleware> logger)
    {
        this.next = Guard.NotNull(next);
        this.logger = Guard.NotNull(logger);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await HandleValidationExceptionAsync(context, ex);
        }
        catch (ArgumentException ex) when (IsValidationRelated(ex))
        {
            await HandleArgumentExceptionAsync(context, ex);
        }
        catch (InvalidOperationException ex) when (IsValidationRelated(ex))
        {
            await HandleInvalidOperationExceptionAsync(context, ex);
        }
    }

    private async Task HandleValidationExceptionAsync(HttpContext context, ValidationException exception)
    {
        this.logger.LogWarning(
            exception,
            "Validation failed for request {Method} {Path}: {Message}",
            context.Request.Method,
            context.Request.Path,
            exception.Message);

        var problemDetails = new ValidationProblemDetails(exception.Errors)
        {
            Type = "https://tools.ietf.org/html/rfc7807",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    private async Task HandleArgumentExceptionAsync(HttpContext context, ArgumentException exception)
    {
        this.logger.LogWarning(
            exception,
            "Invalid argument in request {Method} {Path}: {Message}",
            context.Request.Method,
            context.Request.Path,
            exception.Message);

        var problemDetails = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7807",
            Title = "Invalid input parameter.",
            Status = StatusCodes.Status400BadRequest,
            Detail = exception.Message,
            Instance = context.Request.Path
        };

        if (exception.ParamName.HasValue())
        {
            problemDetails.Extensions["parameter"] = exception.ParamName;
        }

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    private async Task HandleInvalidOperationExceptionAsync(HttpContext context, InvalidOperationException exception)
    {
        this.logger.LogWarning(
            exception,
            "Invalid operation in request {Method} {Path}: {Message}",
            context.Request.Method,
            context.Request.Path,
            exception.Message);

        var problemDetails = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7807",
            Title = "Invalid operation.",
            Status = StatusCodes.Status400BadRequest,
            Detail = exception.Message,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    private static bool IsValidationRelated(Exception exception)
    {
        var message = exception.Message.ToLowerInvariant();
        return message.Contains("invalid") ||
               message.Contains("required") ||
               message.Contains("validation") ||
               message.Contains("must be") ||
               message.Contains("cannot be") ||
               message.Contains("should be");
    }
}

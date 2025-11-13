// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Filters;

/// <summary>
/// Global action filter that performs comprehensive request validation before controller execution.
/// Implements RFC 7807 Problem Details for consistent validation error responses.
/// </summary>
/// <remarks>
/// This filter runs before controller actions to:
/// <list type="bullet">
/// <item><description>Validate ModelState automatically for all requests</description></item>
/// <item><description>Return ValidationProblemDetails for invalid model state (RFC 7807 format)</description></item>
/// <item><description>Convert field names to camelCase for client-side consistency</description></item>
/// <item><description>Enforce request size limits (100 MB max) to prevent DoS attacks</description></item>
/// <item><description>Include requestId for correlation with logs and distributed tracing</description></item>
/// </list>
/// </remarks>
public sealed class SecureInputValidationFilter : IAsyncActionFilter
{
    private readonly ILogger<SecureInputValidationFilter> logger;

    /// <summary>
    /// Maximum allowed request payload size in bytes (100 MB).
    /// Requests exceeding this limit will receive a 413 Payload Too Large response.
    /// </summary>
    private const long MaxRequestSize = 100_000_000;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureInputValidationFilter"/> class.
    /// </summary>
    /// <param name="logger">Logger for recording validation failures and security events.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
    public SecureInputValidationFilter(ILogger<SecureInputValidationFilter> logger)
    {
        this.logger = Guard.NotNull(logger);
    }

    /// <summary>
    /// Executes the filter to validate the incoming request before the controller action.
    /// </summary>
    /// <param name="context">The action executing context containing request details.</param>
    /// <param name="next">The delegate to invoke the next action filter or the action itself.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        Guard.NotNull(context);

        Guard.NotNull(next);

        var requestId = context.HttpContext.TraceIdentifier;

        // Step 1: Enforce request size limits to prevent DoS attacks
        if (context.HttpContext.Request.ContentLength.HasValue &&
            context.HttpContext.Request.ContentLength.Value > MaxRequestSize)
        {
            this.logger.LogWarning(
                "Request size limit exceeded. Size: {RequestSize} bytes, Limit: {MaxSize} bytes [RequestId: {RequestId}]",
                context.HttpContext.Request.ContentLength.Value,
                MaxRequestSize,
                requestId);

            context.Result = new StatusCodeResult(StatusCodes.Status413PayloadTooLarge);
            return;
        }

        // Step 2: Validate ModelState automatically
        if (!context.ModelState.IsValid)
        {
            var validationErrors = BuildValidationErrors(context.ModelState);

            this.logger.LogWarning(
                "Model validation failed in {Controller}.{Action}. Errors: {ErrorCount} [RequestId: {RequestId}]",
                context.RouteData.Values["controller"],
                context.RouteData.Values["action"],
                validationErrors.Count,
                requestId);

            // Return RFC 7807 ValidationProblemDetails with camelCase field names
            var problemDetails = new ValidationProblemDetails(validationErrors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred",
                Instance = requestId,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            };

            // Add requestId to extensions for correlation
            problemDetails.Extensions["requestId"] = requestId;
            problemDetails.Extensions["timestamp"] = DateTimeOffset.UtcNow;

            context.Result = new BadRequestObjectResult(problemDetails);
            return;
        }

        // All validations passed, proceed to controller action
        await next().ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a dictionary of validation errors from ModelState with camelCase field names.
    /// </summary>
    /// <param name="modelState">The model state dictionary containing validation errors.</param>
    /// <returns>
    /// A dictionary where keys are camelCase field names and values are arrays of error messages.
    /// </returns>
    private static IDictionary<string, string[]> BuildValidationErrors(ModelStateDictionary modelState)
    {
        return modelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => ToCamelCase(kvp.Key),
                kvp => kvp.Value!.Errors
                    .Select(error => GetErrorMessage(error))
                    .Where(msg => !string.IsNullOrWhiteSpace(msg))
                    .ToArray()
            );
    }

    /// <summary>
    /// Extracts a safe error message from a ModelError.
    /// </summary>
    /// <param name="error">The model error.</param>
    /// <returns>The error message, or a generic message if the error message is empty.</returns>
    private static string GetErrorMessage(ModelError error)
    {
        // Prefer the error message, but fall back to exception message if available
        if (!string.IsNullOrWhiteSpace(error.ErrorMessage))
        {
            return error.ErrorMessage;
        }

        // If there's an exception but no message, provide a generic message
        // (don't expose internal exception details)
        if (error.Exception != null)
        {
            return "The value is invalid";
        }

        return "Validation error";
    }

    /// <summary>
    /// Converts a field name to camelCase for consistent client-side naming conventions.
    /// </summary>
    /// <param name="fieldName">The field name to convert (may use dot notation for nested properties).</param>
    /// <returns>The field name in camelCase format.</returns>
    /// <remarks>
    /// Examples:
    /// <list type="bullet">
    /// <item><description>"FirstName" → "firstName"</description></item>
    /// <item><description>"User.EmailAddress" → "user.emailAddress"</description></item>
    /// <item><description>"Items[0].Price" → "items[0].price"</description></item>
    /// </list>
    /// </remarks>
    private static string ToCamelCase(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return fieldName;
        }

        // Handle dot-separated nested properties (e.g., "User.FirstName" → "user.firstName")
        var parts = fieldName.Split('.');
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = ConvertSinglePartToCamelCase(parts[i]);
        }

        return string.Join(".", parts);
    }

    /// <summary>
    /// Converts a single part of a field name to camelCase.
    /// </summary>
    /// <param name="part">The part to convert.</param>
    /// <returns>The part in camelCase format.</returns>
    private static string ConvertSinglePartToCamelCase(string part)
    {
        if (string.IsNullOrWhiteSpace(part))
        {
            return part;
        }

        // Handle array indexers (e.g., "Items[0]" → "items[0]")
        var indexerStart = part.IndexOf('[');
        if (indexerStart > 0)
        {
            var propertyName = part[..indexerStart];
            var indexer = part[indexerStart..];
            return char.ToLowerInvariant(propertyName[0]) + propertyName[1..] + indexer;
        }

        // Simple camelCase conversion
        if (part.Length == 1)
        {
            return char.ToLowerInvariant(part[0]).ToString();
        }

        return char.ToLowerInvariant(part[0]) + part[1..];
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Honua.Server.Core.Geoservices.GeometryService;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Utilities;
using Honua.Server.Observability.CorrelationId;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.GeoservicesREST.Filters;

/// <summary>
/// Exception filter specifically designed for geometry service operations.
/// </summary>
/// <remarks>
/// <para>
/// This filter handles common exceptions that occur during geometry operations and maps them
/// to appropriate HTTP status codes with RFC 7807 Problem Details responses.
/// </para>
/// <para>
/// <strong>Exception Handling Strategy:</strong>
/// </para>
/// <list type="table">
/// <listheader>
/// <term>Exception Type</term>
/// <description>HTTP Status</description>
/// </listheader>
/// <item>
/// <term><see cref="ArgumentException"/> (complexity validation)</term>
/// <description>400 Bad Request - Geometry complexity limits exceeded</description>
/// </item>
/// <item>
/// <term><see cref="OperationCanceledException"/></term>
/// <description>408 Request Timeout - Operation exceeded time limit</description>
/// </item>
/// <item>
/// <term><see cref="GeometrySerializationException"/></term>
/// <description>400 Bad Request - Invalid geometry format or serialization error</description>
/// </item>
/// <item>
/// <term><see cref="GeometryServiceException"/></term>
/// <description>500 Internal Server Error - Geometry operation failed</description>
/// </item>
/// </list>
/// <para>
/// <strong>Integration:</strong>
/// </para>
/// <para>
/// This filter can be applied at the controller level using the <c>[ServiceFilter]</c> attribute:
/// </para>
/// <code>
/// [ServiceFilter(typeof(GeometryOperationExceptionFilter))]
/// public class GeoservicesRESTGeometryServerController : ControllerBase
/// {
///     // Controller methods...
/// }
/// </code>
/// <para>
/// The filter uses structured logging to capture operation details and provides consistent
/// error responses that prevent information leakage while helping clients understand failures.
/// </para>
/// </remarks>
public sealed class GeometryOperationExceptionFilter : IExceptionFilter
{
    private const int DefaultTimeoutSeconds = 30;
    private readonly ILogger<GeometryOperationExceptionFilter> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeometryOperationExceptionFilter"/> class.
    /// </summary>
    /// <param name="logger">Logger for structured exception logging.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
    public GeometryOperationExceptionFilter(ILogger<GeometryOperationExceptionFilter> logger)
    {
        this.logger = Guard.NotNull(logger);
    }

    /// <summary>
    /// Called when an unhandled exception occurs during geometry operation processing.
    /// </summary>
    /// <param name="context">The exception context containing exception details and HTTP context.</param>
    public void OnException(ExceptionContext context)
    {
        var exception = context.Exception;

        // Only handle geometry-specific exceptions
        if (!IsGeometryException(exception))
        {
            return;
        }

        var requestId = context.HttpContext.TraceIdentifier;
        var correlationId = CorrelationIdUtilities.GetCorrelationId(context.HttpContext) ?? requestId;
        var action = context.RouteData.Values["action"]?.ToString() ?? "Unknown";

        // Create appropriate response based on exception type
        var problemDetails = exception switch
        {
            ArgumentException argEx when IsComplexityValidationException(argEx)
                => CreateComplexityValidationProblemDetails(argEx, action, requestId, correlationId),

            OperationCanceledException
                => CreateTimeoutProblemDetails(action, requestId, correlationId),

            GeometrySerializationException serEx
                => CreateSerializationProblemDetails(serEx, action, requestId, correlationId),

            GeometryServiceException svcEx
                => CreateServiceProblemDetails(svcEx, action, requestId, correlationId),

            _ => null
        };

        // If we created a problem details response, use it
        if (problemDetails != null)
        {
            context.Result = new ObjectResult(problemDetails)
            {
                StatusCode = problemDetails.Status
            };

            context.ExceptionHandled = true;
        }
    }

    /// <summary>
    /// Determines if the exception is a geometry-specific exception that should be handled by this filter.
    /// </summary>
    private static bool IsGeometryException(Exception exception)
    {
        return exception is ArgumentException or
               OperationCanceledException or
               GeometrySerializationException or
               GeometryServiceException;
    }

    /// <summary>
    /// Determines if an ArgumentException is related to geometry complexity validation.
    /// </summary>
    private static bool IsComplexityValidationException(ArgumentException exception)
    {
        var message = exception.Message;
        return message.Contains("vertices", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("coordinates", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("nesting", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a ProblemDetails response for geometry complexity validation failures.
    /// </summary>
    private ProblemDetails CreateComplexityValidationProblemDetails(
        ArgumentException exception,
        string operationName,
        string requestId,
        string correlationId)
    {
        this.logger.LogWarning(
            exception,
            "Geometry complexity validation failed for {Operation} operation [CorrelationId: {CorrelationId}]",
            operationName,
            correlationId);

        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Geometry Complexity Limit Exceeded",
            Detail = $"Geometry complexity limit exceeded: {exception.Message}",
            Instance = requestId,
            Type = "https://honua.io/errors/geometry-complexity",
            Extensions =
            {
                [CorrelationIdConstants.ProblemDetailsExtensionKey] = correlationId,
                ["requestId"] = requestId,
                ["timestamp"] = DateTimeOffset.UtcNow,
                ["operation"] = operationName
            }
        };
    }

    /// <summary>
    /// Creates a ProblemDetails response for operation timeout.
    /// </summary>
    private ProblemDetails CreateTimeoutProblemDetails(
        string operationName,
        string requestId,
        string correlationId)
    {
        this.logger.LogOperationTimeout(operationName + " operation", DefaultTimeoutSeconds);

        return new ProblemDetails
        {
            Status = StatusCodes.Status408RequestTimeout,
            Title = "Operation Timeout",
            Detail = $"Operation timed out after {DefaultTimeoutSeconds} seconds.",
            Instance = requestId,
            Type = "https://honua.io/errors/timeout",
            Extensions =
            {
                [CorrelationIdConstants.ProblemDetailsExtensionKey] = correlationId,
                ["requestId"] = requestId,
                ["timestamp"] = DateTimeOffset.UtcNow,
                ["operation"] = operationName,
                ["timeoutSeconds"] = DefaultTimeoutSeconds
            }
        };
    }

    /// <summary>
    /// Creates a ProblemDetails response for geometry serialization errors.
    /// </summary>
    private ProblemDetails CreateSerializationProblemDetails(
        GeometrySerializationException exception,
        string operationName,
        string requestId,
        string correlationId)
    {
        this.logger.LogOperationFailure(
            exception,
            "Geometry serialization",
            operationName + " operation");

        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Geometry Serialization Error",
            Detail = exception.Message,
            Instance = requestId,
            Type = "https://honua.io/errors/geometry-serialization",
            Extensions =
            {
                [CorrelationIdConstants.ProblemDetailsExtensionKey] = correlationId,
                ["requestId"] = requestId,
                ["timestamp"] = DateTimeOffset.UtcNow,
                ["operation"] = operationName
            }
        };
    }

    /// <summary>
    /// Creates a ProblemDetails response for geometry service operation errors.
    /// </summary>
    private ProblemDetails CreateServiceProblemDetails(
        GeometryServiceException exception,
        string operationName,
        string requestId,
        string correlationId)
    {
        this.logger.LogOperationFailure(
            exception,
            "Geometry service operation",
            operationName + " operation");

        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Geometry Operation Failed",
            Detail = "Geometry operation failed. Check server logs for details.",
            Instance = requestId,
            Type = "https://honua.io/errors/geometry-operation",
            Extensions =
            {
                [CorrelationIdConstants.ProblemDetailsExtensionKey] = correlationId,
                ["requestId"] = requestId,
                ["timestamp"] = DateTimeOffset.UtcNow,
                ["operation"] = operationName
            }
        };
    }
}

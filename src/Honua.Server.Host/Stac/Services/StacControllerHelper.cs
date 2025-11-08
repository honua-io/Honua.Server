// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Host.Utilities;
using System.Linq;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Honua.Server.Host.Stac.Services;

/// <summary>
/// Helper service for common STAC controller operations.
/// Provides reusable logic for request parsing, validation, and response building.
/// </summary>
public sealed class StacControllerHelper
{
    private readonly IHonuaConfigurationService _configurationService;

    public StacControllerHelper(IHonuaConfigurationService configurationService)
    {
        _configurationService = Guard.NotNull(configurationService);
    }

    /// <summary>
    /// Checks if STAC is enabled in the configuration.
    /// </summary>
    public bool IsStacEnabled() => StacRequestHelpers.IsStacEnabled(_configurationService);

    /// <summary>
    /// Builds the base URI from an HTTP request.
    /// </summary>
    public Uri BuildBaseUri(HttpRequest request) => StacRequestHelpers.BuildBaseUri(request);

    /// <summary>
    /// Extracts the If-Match ETag from request headers.
    /// </summary>
    public string? GetIfMatchETag(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Headers.TryGetValue(HeaderNames.IfMatch, out var ifMatchValues))
        {
            var ifMatch = ifMatchValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(ifMatch))
            {
                return ifMatch.Trim('"');
            }
        }
        return null;
    }

    /// <summary>
    /// Sets the ETag response header if an ETag value is provided.
    /// </summary>
    public void SetETagHeader(HttpResponse response, string? etag)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!string.IsNullOrWhiteSpace(etag))
        {
            response.Headers[HeaderNames.ETag] = $"\"{etag}\"";
        }
    }

    /// <summary>
    /// Creates a standard NotFound ProblemDetails response.
    /// </summary>
    public ProblemDetails CreateNotFoundProblem(string title, string detail, string? instance = null)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = title,
            Detail = detail,
            Instance = instance
        };
    }

    /// <summary>
    /// Creates a standard BadRequest ProblemDetails response.
    /// </summary>
    public ProblemDetails CreateBadRequestProblem(string title, string detail)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title,
            Detail = detail
        };
    }

    /// <summary>
    /// Creates a standard Conflict ProblemDetails response.
    /// </summary>
    public ProblemDetails CreateConflictProblem(string title, string detail, string? instance = null)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = title,
            Detail = detail,
            Instance = instance
        };
    }

    /// <summary>
    /// Creates a standard Precondition Failed ProblemDetails response.
    /// Used for ETag mismatch scenarios (HTTP 412).
    /// </summary>
    public ProblemDetails CreatePreconditionFailedProblem(string title, string detail, string? instance = null)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status412PreconditionFailed,
            Title = title,
            Detail = detail,
            Instance = instance
        };
    }

    /// <summary>
    /// Creates a standard Server Error ProblemDetails response (HTTP 500).
    /// Used for internal server errors or unexpected exceptions.
    /// </summary>
    public ProblemDetails CreateServerErrorProblem(string detail, string? instance = null)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Detail = detail,
            Instance = instance
        };
    }

    /// <summary>
    /// Creates a standard BadRequest ProblemDetails for invalid parameter validation.
    /// Standardizes error messages for parameter validation failures.
    /// </summary>
    public ProblemDetails CreateInvalidParameterProblem(string parameterName, string detail)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = $"Invalid parameter: {parameterName}",
            Detail = detail
        };
    }

    /// <summary>
    /// Extracts username from the User principal.
    /// </summary>
    public string GetUsername(System.Security.Claims.ClaimsPrincipal? user)
    {
        return user?.Identity?.Name ?? "unknown";
    }

    /// <summary>
    /// Extracts IP address from HTTP connection info.
    /// </summary>
    public string? GetIpAddress(ConnectionInfo connection)
    {
        return connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Builds audit context from HTTP context by gathering username and IP address.
    /// Returns a tuple of (username, ipAddress) for audit metadata collection.
    /// </summary>
    public (string username, string? ipAddress) BuildAuditContext(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var username = GetUsername(httpContext.User);
        var ipAddress = GetIpAddress(httpContext.Connection);
        return (username, ipAddress);
    }

    /// <summary>
    /// Builds the base URI from the current request and optionally sets the ETag header.
    /// This is a convenience method that combines BuildBaseUri and SetETagHeader operations.
    /// </summary>
    /// <param name="request">The HTTP request to build the base URI from.</param>
    /// <param name="response">The HTTP response to set the ETag header on.</param>
    /// <param name="etag">Optional ETag value to set in the response header.</param>
    /// <returns>The base URI for building STAC resource links.</returns>
    public Uri GetBaseUriAndSetETag(HttpRequest request, HttpResponse response, string? etag = null)
    {
        var baseUri = BuildBaseUri(request);
        if (etag is not null)
        {
            SetETagHeader(response, etag);
        }
        return baseUri;
    }

    /// <summary>
    /// Handles DBConcurrencyException and returns a 412 Precondition Failed response.
    /// Used when an ETag mismatch indicates the resource was modified by another user.
    /// </summary>
    /// <param name="ex">The DBConcurrencyException that was thrown.</param>
    /// <param name="resourceType">The type of resource (e.g., "collection", "item").</param>
    /// <param name="instance">The request path or resource instance identifier.</param>
    /// <returns>A 412 status code with ProblemDetails explaining the ETag mismatch.</returns>
    public ObjectResult HandleDBConcurrencyException(System.Data.DBConcurrencyException ex, string resourceType, string? instance = null)
    {
        var detail = $"The {resourceType} was modified by another user. Please retrieve the latest version and retry your update.";
        return new ObjectResult(CreatePreconditionFailedProblem("ETag mismatch", detail, instance))
        {
            StatusCode = StatusCodes.Status412PreconditionFailed
        };
    }

    /// <summary>
    /// Maps OperationErrorType to appropriate HTTP response with ProblemDetails.
    /// Handles common error scenarios from service layer operations.
    /// </summary>
    /// <param name="errorType">The operation error type from the service layer.</param>
    /// <param name="errorMessage">The error message to include in the response.</param>
    /// <param name="instance">The request path or resource instance identifier.</param>
    /// <returns>An ActionResult with appropriate status code and ProblemDetails.</returns>
    public ActionResult MapOperationErrorToResponse(OperationErrorType errorType, string errorMessage, string? instance = null)
    {
        return errorType switch
        {
            OperationErrorType.Validation => new BadRequestObjectResult(CreateBadRequestProblem("Validation failed", errorMessage)),
            OperationErrorType.NotFound => new NotFoundObjectResult(CreateNotFoundProblem("Resource not found", errorMessage, instance)),
            OperationErrorType.Conflict => new ConflictObjectResult(CreateConflictProblem("Resource conflict", errorMessage, instance)),
            _ => new ObjectResult(new ProblemDetails { Detail = errorMessage }) { StatusCode = StatusCodes.Status500InternalServerError }
        };
    }

    /// <summary>
    /// Ensures STAC is enabled, returning a NotFound result if it is not.
    /// </summary>
    /// <returns>NotFoundResult if STAC is disabled, otherwise null to allow the request to proceed.</returns>
    public ActionResult? EnsureStacEnabledOrNotFound()
    {
        if (!IsStacEnabled())
        {
            return new NotFoundResult();
        }
        return null;
    }

    /// <summary>
    /// Validates that collections and ids parameters do not exceed maximum allowed counts.
    /// </summary>
    /// <param name="collections">The collections list to validate.</param>
    /// <param name="ids">The ids list to validate.</param>
    /// <param name="maxCollections">Maximum allowed collections count.</param>
    /// <param name="maxIds">Maximum allowed ids count.</param>
    /// <returns>A tuple with (isValid, errorResult). If invalid, errorResult contains the BadRequest response.</returns>
    public (bool IsValid, ActionResult? Error) ValidateCollectionsAndIds(
        System.Collections.Generic.IReadOnlyList<string>? collections,
        System.Collections.Generic.IReadOnlyList<string>? ids,
        int maxCollections,
        int maxIds)
    {
        if (collections is not null && collections.Count > maxCollections)
        {
            var problem = CreateBadRequestProblem(
                "Collections count exceeds maximum",
                $"Collections count ({collections.Count}) exceeds maximum of {maxCollections}.");
            return (false, new BadRequestObjectResult(problem));
        }

        if (ids is not null && ids.Count > maxIds)
        {
            var problem = CreateBadRequestProblem(
                "Ids count exceeds maximum",
                $"Ids count ({ids.Count}) exceeds maximum of {maxIds}.");
            return (false, new BadRequestObjectResult(problem));
        }

        return (true, null);
    }
}

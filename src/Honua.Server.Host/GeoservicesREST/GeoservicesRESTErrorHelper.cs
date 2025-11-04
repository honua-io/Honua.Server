// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Honua.Server.Host.Utilities;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST;

/// <summary>
/// Provides standardized result helpers for GeoservicesREST and general API responses.
/// This class now delegates to ApiErrorResponse.Json for consistency across the application.
/// </summary>
[Obsolete("Use ApiErrorResponse.Json instead for new code. This class is maintained for backward compatibility.")]
public static class GeoservicesRESTErrorHelper
{
    /// <summary>
    /// Creates a BadRequestObjectResult with a standardized error message format.
    /// </summary>
    /// <param name="message">The error message to include in the response.</param>
    /// <returns>A BadRequestObjectResult with the error message.</returns>
    public static BadRequestObjectResult BadRequest(string message)
    {
        return ApiErrorResponse.Json.BadRequest(message);
    }

    /// <summary>
    /// Creates a NotFound result with a standardized error message format.
    /// </summary>
    /// <param name="message">The error message to include in the response.</param>
    /// <returns>A NotFound result with the error message.</returns>
    public static IResult NotFoundWithMessage(string message)
    {
        return ApiErrorResponse.Json.NotFound(message);
    }

    /// <summary>
    /// Creates a NotFound result with a formatted message for a specific resource.
    /// </summary>
    /// <param name="resource">The type of resource (e.g., "Service", "Dataset", "Style").</param>
    /// <param name="identifier">The identifier of the resource that was not found.</param>
    /// <returns>A NotFound result with a formatted error message.</returns>
    public static IResult NotFound(string resource, string identifier)
    {
        return ApiErrorResponse.Json.NotFound(resource, identifier);
    }

    /// <summary>
    /// Creates an Ok result with the provided data.
    /// </summary>
    /// <typeparam name="T">The type of data to return.</typeparam>
    /// <param name="data">The data to include in the response.</param>
    /// <returns>An Ok result with the data.</returns>
    public static IResult OkData<T>(T data)
    {
        return Results.Ok(data);
    }
}

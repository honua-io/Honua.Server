// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for building common OGC API responses including error responses,
/// problem details, and validation errors.
/// </summary>
internal sealed class OgcResponseBuilder
{
    /// <summary>
    /// Creates a validation problem result for invalid request parameters.
    /// </summary>
    /// <param name="detail">Detailed error message</param>
    /// <param name="parameter">Name of the invalid parameter</param>
    /// <returns>Problem details result with 400 Bad Request status</returns>
    public IResult CreateValidationProblem(string detail, string parameter)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid request parameter",
            Detail = detail,
            Extensions = { ["parameter"] = parameter }
        };

        return Results.Problem(
            problemDetails.Detail,
            statusCode: problemDetails.Status,
            title: problemDetails.Title,
            extensions: problemDetails.Extensions);
    }

    /// <summary>
    /// Creates a not found problem result for missing resources.
    /// </summary>
    /// <param name="detail">Detailed error message</param>
    /// <returns>Problem details result with 404 Not Found status</returns>
    public IResult CreateNotFoundProblem(string detail)
    {
        return Results.Problem(
            detail,
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found");
    }

    /// <summary>
    /// Maps collection resolution errors to appropriate HTTP responses.
    /// </summary>
    /// <param name="error">Error from collection resolution</param>
    /// <param name="collectionId">Collection identifier that failed to resolve</param>
    /// <returns>Problem details result with appropriate status code</returns>
    public IResult MapCollectionResolutionError(Core.Results.Error error, string collectionId)
    {
        return error.Code switch
        {
            "not_found" => CreateNotFoundProblem(error.Message ?? $"Collection '{collectionId}' was not found."),
            "invalid" => Results.Problem(
                error.Message ?? "Collection resolution failed.",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Collection resolution failed"),
            _ => Results.Problem(
                error.Message ?? "Collection resolution failed.",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Collection resolution failed")
        };
    }

    /// <summary>
    /// Builds a collection summary object for HTML and JSON responses.
    /// </summary>
    /// <param name="service">Service definition</param>
    /// <param name="layer">Layer definition</param>
    /// <param name="collectionId">Collection identifier</param>
    /// <returns>Collection summary containing key metadata</returns>
    public OgcSharedHandlers.CollectionSummary BuildCollectionSummary(
        ServiceDefinition service,
        LayerDefinition layer,
        string collectionId)
    {
        var crs = layer.Crs.Count > 0
            ? layer.Crs
            : OgcSharedHandlers.BuildDefaultCrs(service);

        return new OgcSharedHandlers.CollectionSummary(
            collectionId,
            layer.Title,
            layer.Description,
            layer.ItemType,
            crs,
            OgcSharedHandlers.DetermineStorageCrs(layer));
    }

    /// <summary>
    /// Formats a Content-Crs header value with proper angle bracket wrapping.
    /// </summary>
    /// <param name="value">CRS identifier</param>
    /// <returns>Formatted CRS value for header</returns>
    public string FormatContentCrs(string? value)
        => value.IsNullOrWhiteSpace() ? string.Empty : $"<{value}>";

    /// <summary>
    /// Adds a response header to an existing result.
    /// </summary>
    /// <param name="result">Result to wrap</param>
    /// <param name="headerName">Header name</param>
    /// <param name="headerValue">Header value</param>
    /// <returns>Wrapped result with header</returns>
    public IResult WithResponseHeader(IResult result, string headerName, string headerValue)
        => new HeaderResult(result, headerName, headerValue);

    /// <summary>
    /// Adds a Content-Crs header to a result.
    /// </summary>
    /// <param name="result">Result to wrap</param>
    /// <param name="contentCrs">CRS identifier</param>
    /// <returns>Wrapped result with Content-Crs header</returns>
    public IResult WithContentCrsHeader(IResult result, string? contentCrs)
        => WithResponseHeader(result, "Content-Crs", FormatContentCrs(contentCrs));

    /// <summary>
    /// Builds style IDs list with default style first.
    /// </summary>
    /// <param name="layer">Layer definition</param>
    /// <returns>Ordered list of style identifiers</returns>
    public IReadOnlyList<string> BuildOrderedStyleIds(LayerDefinition layer)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<string>();

        if (layer.DefaultStyleId.HasValue() && seen.Add(layer.DefaultStyleId))
        {
            results.Add(layer.DefaultStyleId);
        }

        foreach (var styleId in layer.StyleIds)
        {
            if (styleId.HasValue() && seen.Add(styleId))
            {
                results.Add(styleId);
            }
        }

        return results.Count == 0 ? Array.Empty<string>() : results;
    }

    /// <summary>
    /// Internal result wrapper that adds a header to an existing result.
    /// </summary>
    private sealed class HeaderResult : IResult
    {
        private readonly IResult inner;
        private readonly string headerName;
        private readonly string headerValue;

        public HeaderResult(IResult inner, string headerName, string headerValue)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.headerName = headerName ?? throw new ArgumentNullException(nameof(headerName));
            this.headerValue = headerValue ?? throw new ArgumentNullException(nameof(headerValue));
        }

        public Task ExecuteAsync(HttpContext httpContext)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            if (this.headerValue.HasValue())
            {
                httpContext.Response.Headers[this.headerName] = this.headerValue;
            }

            return this.inner.ExecuteAsync(httpContext);
        }
    }
}

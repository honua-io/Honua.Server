// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Honua.Server.Host.Http;

/// <summary>
/// Extension methods for adding ETag headers to HTTP results.
/// </summary>
public static class ETagResultExtensions
{
    /// <summary>
    /// Adds an ETag header to the response if the feature record has a version.
    /// </summary>
    /// <param name="result">The HTTP result.</param>
    /// <param name="record">The feature record containing version information.</param>
    /// <returns>The HTTP result with ETag header added.</returns>
    public static IResult WithETag(this IResult result, FeatureRecord? record)
    {
        if (record?.Version == null)
        {
            return result;
        }

        return result.WithETag(record.Version);
    }

    /// <summary>
    /// Adds an ETag header to the response with the specified version value.
    /// </summary>
    /// <param name="result">The HTTP result.</param>
    /// <param name="version">The version object to use for the ETag.</param>
    /// <returns>The HTTP result with ETag header added.</returns>
    public static IResult WithETag(this IResult result, object? version)
    {
        if (version == null)
        {
            return result;
        }

        var etag = ETagHelper.GenerateETag(version);
        if (etag == null)
        {
            return result;
        }

        // Add ETag header to response by wrapping the result
        return new ETagResult(result, etag);
    }

    /// <summary>
    /// Validates the If-Match header against the current resource version.
    /// Returns 412 Precondition Failed if the header doesn't match.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="currentVersion">The current version of the resource.</param>
    /// <param name="requireIfMatch">If true, returns 428 Precondition Required when If-Match is missing.</param>
    /// <returns>A problem result if validation fails, otherwise null.</returns>
    public static IResult? ValidateIfMatch(HttpRequest request, object? currentVersion, bool requireIfMatch = false)
    {
        if (!request.Headers.TryGetValue(HeaderNames.IfMatch, out var ifMatchValues))
        {
            if (requireIfMatch)
            {
                return Results.StatusCode(428); // 428 Precondition Required
            }
            return null;
        }

        var ifMatch = ifMatchValues.ToString();

        // Handle If-Match: *  (match any version)
        if (ifMatch.Trim() == "*")
        {
            return null;
        }

        // Check if ETag matches current version
        if (ETagHelper.ETagMatches(ifMatch, currentVersion))
        {
            return null;
        }

        // ETag mismatch - return 412 Precondition Failed
        return Results.StatusCode(412);
    }

    /// <summary>
    /// Validates the If-None-Match header against the current resource version.
    /// Returns 304 Not Modified if the header matches (for GET requests).
    /// Returns 412 Precondition Failed if the header matches (for modification requests).
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="currentVersion">The current version of the resource.</param>
    /// <param name="forModification">True for PUT/PATCH/DELETE, false for GET/HEAD.</param>
    /// <returns>A problem result if validation fails, otherwise null.</returns>
    public static IResult? ValidateIfNoneMatch(HttpRequest request, object? currentVersion, bool forModification = false)
    {
        if (!request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var ifNoneMatchValues))
        {
            return null;
        }

        var ifNoneMatch = ifNoneMatchValues.ToString();

        // Handle If-None-Match: *  (fail if resource exists)
        if (ifNoneMatch.Trim() == "*")
        {
            if (currentVersion != null)
            {
                return forModification
                    ? Results.StatusCode(412) // 412 Precondition Failed for modification
                    : Results.StatusCode(304); // 304 Not Modified for GET
            }
            return null;
        }

        // Check if ETag matches current version
        if (ETagHelper.ETagMatches(ifNoneMatch, currentVersion))
        {
            return forModification
                ? Results.StatusCode(412) // 412 Precondition Failed for modification
                : Results.StatusCode(304); // 304 Not Modified for GET
        }

        return null;
    }

    /// <summary>
    /// Extracts version information from If-Match header for use in update operations.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="versionType">Optional version type hint for conversion.</param>
    /// <returns>The version object extracted from the If-Match header, or null if not present.</returns>
    public static object? ExtractVersionFromIfMatch(HttpRequest request, string? versionType = null)
    {
        if (!request.Headers.TryGetValue(HeaderNames.IfMatch, out var ifMatchValues))
        {
            return null;
        }

        var ifMatch = ifMatchValues.ToString();

        // Handle If-Match: * (no specific version)
        if (ifMatch.Trim() == "*")
        {
            return null;
        }

        var parsedETag = ETagHelper.ParseETag(ifMatch);
        return ETagHelper.ConvertETagToVersion(parsedETag, versionType);
    }

    /// <summary>
    /// IResult implementation that adds an ETag header to another result.
    /// </summary>
    private sealed class ETagResult : IResult
    {
        private readonly IResult innerResult;
        private readonly string etag;

        public ETagResult(IResult innerResult, string etag)
        {
            this.innerResult = innerResult;
            this.etag = etag;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers[HeaderNames.ETag] = this.etag;
            await this.innerResult.ExecuteAsync(httpContext);
        }
    }
}

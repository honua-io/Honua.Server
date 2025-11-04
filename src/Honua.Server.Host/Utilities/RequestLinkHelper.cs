// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Honua.Server.Core.Security;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Utilities;

/// <summary>
/// Provides HATEOAS link generation utilities for building hypermedia links across protocol implementations.
/// Supports proxy headers (X-Forwarded-Proto, X-Forwarded-Host, Forwarded), HTTPS termination, and base path handling.
/// </summary>
public static class RequestLinkHelper
{
    /// <summary>
    /// Builds an absolute URL from a relative path, respecting proxy headers and base paths.
    /// </summary>
    /// <param name="request">The HTTP request context</param>
    /// <param name="relativePath">The relative path (e.g., "/api/resource")</param>
    /// <returns>The absolute URL (e.g., "https://example.com/base/api/resource")</returns>
    public static string BuildAbsoluteUrl(this HttpRequest request, string relativePath)
    {
        Guard.NotNull(request);
        Guard.NotNull(relativePath);

        var scheme = GetEffectiveScheme(request);
        var host = GetEffectiveHost(request);
        var basePath = request.PathBase.HasValue ? request.PathBase.Value : string.Empty;
        var normalized = relativePath.StartsWith("/", StringComparison.Ordinal) ? relativePath : "/" + relativePath;
        return $"{scheme}://{host}{basePath}{normalized}";
    }

    /// <summary>
    /// Builds an absolute URL with query string parameters.
    /// </summary>
    /// <param name="request">The HTTP request context</param>
    /// <param name="relativePath">The relative path</param>
    /// <param name="queryParameters">Query string parameters to append</param>
    /// <returns>The absolute URL with query string</returns>
    public static string BuildAbsoluteUrl(this HttpRequest request, string relativePath, IDictionary<string, string?> queryParameters)
    {
        Guard.NotNull(request);
        Guard.NotNull(relativePath);

        var baseUrl = BuildAbsoluteUrl(request, relativePath);

        if (queryParameters == null || queryParameters.Count == 0)
        {
            return baseUrl;
        }

        var cleanedParams = queryParameters
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        return cleanedParams.Count == 0
            ? baseUrl
            : QueryHelpers.AddQueryString(baseUrl, cleanedParams!);
    }

    /// <summary>
    /// Generates a "self" link for the current resource.
    /// </summary>
    /// <param name="request">The HTTP request context</param>
    /// <param name="queryString">Optional query string to preserve (null = use current request query)</param>
    /// <returns>The self link URL</returns>
    public static string GenerateSelfLink(this HttpRequest request, string? queryString = null)
    {
        Guard.NotNull(request);

        var path = request.Path.HasValue ? request.Path.Value : "/";
        var baseUrl = BuildAbsoluteUrl(request, path);

        if (queryString != null)
        {
            return string.IsNullOrWhiteSpace(queryString) ? baseUrl : $"{baseUrl}?{queryString.TrimStart('?')}";
        }

        return request.QueryString.HasValue ? $"{baseUrl}{request.QueryString.Value}" : baseUrl;
    }

    /// <summary>
    /// Generates a "next" pagination link.
    /// </summary>
    /// <param name="request">The HTTP request context</param>
    /// <param name="nextOffset">The offset for the next page</param>
    /// <param name="limit">The page size limit</param>
    /// <returns>The next link URL</returns>
    public static string GenerateNextLink(this HttpRequest request, int nextOffset, int limit)
    {
        Guard.NotNull(request);

        var path = request.Path.HasValue ? request.Path.Value : "/";
        var queryParams = ParseQueryParameters(request);

        queryParams["offset"] = nextOffset.ToString(CultureInfo.InvariantCulture);
        queryParams["limit"] = limit.ToString(CultureInfo.InvariantCulture);

        return BuildAbsoluteUrl(request, path, queryParams);
    }

    /// <summary>
    /// Generates a "prev" pagination link.
    /// </summary>
    /// <param name="request">The HTTP request context</param>
    /// <param name="prevOffset">The offset for the previous page</param>
    /// <param name="limit">The page size limit</param>
    /// <returns>The previous link URL</returns>
    public static string GeneratePrevLink(this HttpRequest request, int prevOffset, int limit)
    {
        Guard.NotNull(request);

        var path = request.Path.HasValue ? request.Path.Value : "/";
        var queryParams = ParseQueryParameters(request);

        queryParams["offset"] = prevOffset.ToString(CultureInfo.InvariantCulture);
        queryParams["limit"] = limit.ToString(CultureInfo.InvariantCulture);

        return BuildAbsoluteUrl(request, path, queryParams);
    }

    /// <summary>
    /// Generates an "alternate" format link (e.g., different content type representation).
    /// </summary>
    /// <param name="request">The HTTP request context</param>
    /// <param name="format">The format parameter value (e.g., "json", "html", "geojson")</param>
    /// <param name="relativePath">Optional relative path (null = current path)</param>
    /// <returns>The alternate link URL</returns>
    public static string GenerateAlternateLink(this HttpRequest request, string format, string? relativePath = null)
    {
        Guard.NotNull(request);
        Guard.NotNull(format);

        var path = relativePath ?? (request.Path.HasValue ? request.Path.Value : "/");
        var queryParams = ParseQueryParameters(request);
        queryParams["f"] = format;

        return BuildAbsoluteUrl(request, path, queryParams);
    }

    /// <summary>
    /// Generates a collection link (e.g., OGC API Features collections).
    /// </summary>
    /// <param name="request">The HTTP request context</param>
    /// <param name="collectionId">The collection identifier</param>
    /// <param name="basePath">The base API path (e.g., "/ogc/collections", "/stac/collections")</param>
    /// <returns>The collection link URL</returns>
    public static string GenerateCollectionLink(this HttpRequest request, string collectionId, string basePath = "/ogc/collections")
    {
        Guard.NotNull(request);
        Guard.NotNull(collectionId);
        Guard.NotNull(basePath);

        var encodedId = Uri.EscapeDataString(collectionId);
        var path = $"{basePath.TrimEnd('/')}/{encodedId}";
        return BuildAbsoluteUrl(request, path);
    }

    /// <summary>
    /// Generates an item link (e.g., feature in a collection).
    /// </summary>
    /// <param name="request">The HTTP request context</param>
    /// <param name="collectionId">The collection identifier</param>
    /// <param name="itemId">The item identifier</param>
    /// <param name="basePath">The base API path (e.g., "/ogc/collections", "/stac/collections")</param>
    /// <returns>The item link URL</returns>
    public static string GenerateItemLink(this HttpRequest request, string collectionId, string itemId, string basePath = "/ogc/collections")
    {
        Guard.NotNull(request);
        Guard.NotNull(collectionId);
        Guard.NotNull(itemId);
        Guard.NotNull(basePath);

        var encodedCollectionId = Uri.EscapeDataString(collectionId);
        var encodedItemId = Uri.EscapeDataString(itemId);
        var path = $"{basePath.TrimEnd('/')}/{encodedCollectionId}/items/{encodedItemId}";
        return BuildAbsoluteUrl(request, path);
    }

    /// <summary>
    /// Generates a root/landing page link.
    /// </summary>
    /// <param name="request">The HTTP request context</param>
    /// <param name="rootPath">The root API path (e.g., "/ogc", "/stac")</param>
    /// <returns>The root link URL</returns>
    public static string GenerateRootLink(this HttpRequest request, string rootPath = "/")
    {
        Guard.NotNull(request);
        Guard.NotNull(rootPath);

        return BuildAbsoluteUrl(request, rootPath);
    }

    /// <summary>
    /// Generates a custom link with optional query parameter overrides.
    /// </summary>
    /// <param name="request">The HTTP request context</param>
    /// <param name="relativePath">The relative path</param>
    /// <param name="queryOverrides">Query parameters to set/override (null value removes parameter)</param>
    /// <param name="preserveCurrentQuery">Whether to preserve current request query parameters</param>
    /// <returns>The custom link URL</returns>
    public static string GenerateLink(
        this HttpRequest request,
        string relativePath,
        IDictionary<string, string?>? queryOverrides = null,
        bool preserveCurrentQuery = true)
    {
        Guard.NotNull(request);
        Guard.NotNull(relativePath);

        var queryParams = preserveCurrentQuery
            ? ParseQueryParameters(request)
            : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (queryOverrides != null)
        {
            foreach (var kvp in queryOverrides)
            {
                if (kvp.Value == null)
                {
                    queryParams.Remove(kvp.Key);
                }
                else
                {
                    queryParams[kvp.Key] = kvp.Value;
                }
            }
        }

        return BuildAbsoluteUrl(request, relativePath, queryParams);
    }

    /// <summary>
    /// Adds or replaces a query parameter in a QueryString.
    /// </summary>
    public static QueryString AddOrReplaceQuery(this QueryString query, string key, string value)
    {
        Guard.NotNull(key);

        var parsed = QueryHelpers.ParseQuery(query.Value ?? string.Empty);
        parsed[key] = value;

        // BUG FIX #10: Preserve StringValues when rebuilding query to prevent multi-valued parameters
        // from collapsing to a single value (e.g., multiple "collections" values)
        var flattened = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in parsed)
        {
            flattened[pair.Key] = pair.Value;
        }

        var newQuery = QueryHelpers.AddQueryString(string.Empty, flattened.SelectMany(kvp =>
            kvp.Value.Select(v => new KeyValuePair<string, string?>(kvp.Key, v))));
        return new QueryString(newQuery);
    }

    #region Public Helpers (exposed for CSRF validation and other security checks)

    /// <summary>
    /// Gets the effective scheme, respecting X-Forwarded-Proto and Forwarded headers.
    /// Handles HTTPS termination at load balancers/proxies.
    /// SECURITY: Only trusts forwarded headers from validated trusted proxies.
    /// </summary>
    public static string GetEffectiveScheme(HttpRequest request)
    {
        // SECURITY FIX: Validate that forwarded headers come from trusted proxies
        // to prevent header spoofing attacks (CWE-290, CWE-918)
        if (!ShouldTrustForwardedHeaders(request))
        {
            return request.Scheme;
        }

        // Check X-Forwarded-Proto (common with nginx, AWS ELB, etc.)
        if (request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto))
        {
            var proto = forwardedProto.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(proto))
            {
                return proto.ToLowerInvariant();
            }
        }

        // Check RFC 7239 Forwarded header
        if (request.Headers.TryGetValue("Forwarded", out var forwarded))
        {
            var forwardedValue = forwarded.ToString();
            var protoIndex = forwardedValue.IndexOf("proto=", StringComparison.OrdinalIgnoreCase);
            if (protoIndex >= 0)
            {
                var protoStart = protoIndex + 6;
                var protoEnd = forwardedValue.IndexOfAny(new[] { ';', ',' }, protoStart);
                var proto = protoEnd >= 0
                    ? forwardedValue.Substring(protoStart, protoEnd - protoStart)
                    : forwardedValue.Substring(protoStart);

                if (!string.IsNullOrWhiteSpace(proto))
                {
                    return proto.Trim().ToLowerInvariant();
                }
            }
        }

        return request.Scheme;
    }

    /// <summary>
    /// Gets the effective host, respecting X-Forwarded-Host and Forwarded headers.
    /// SECURITY: Only trusts forwarded headers from validated trusted proxies.
    /// </summary>
    public static string GetEffectiveHost(HttpRequest request)
    {
        // SECURITY FIX: Validate that forwarded headers come from trusted proxies
        // to prevent host header injection attacks (CWE-290)
        if (!ShouldTrustForwardedHeaders(request))
        {
            return request.Host.Value;
        }

        // Check X-Forwarded-Host (common with nginx, AWS ELB, etc.)
        if (request.Headers.TryGetValue("X-Forwarded-Host", out var forwardedHost))
        {
            var host = forwardedHost.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(host))
            {
                return host;
            }
        }

        // Check RFC 7239 Forwarded header
        if (request.Headers.TryGetValue("Forwarded", out var forwarded))
        {
            var forwardedValue = forwarded.ToString();
            var hostIndex = forwardedValue.IndexOf("host=", StringComparison.OrdinalIgnoreCase);
            if (hostIndex >= 0)
            {
                var hostStart = hostIndex + 5;
                var hostEnd = forwardedValue.IndexOfAny(new[] { ';', ',' }, hostStart);
                var host = hostEnd >= 0
                    ? forwardedValue.Substring(hostStart, hostEnd - hostStart)
                    : forwardedValue.Substring(hostStart);

                // Remove quotes if present
                host = host.Trim().Trim('"');

                if (!string.IsNullOrWhiteSpace(host))
                {
                    return host;
                }
            }
        }

        return request.Host.HasValue ? request.Host.Value : "localhost";
    }

    /// <summary>
    /// Determines whether forwarded headers should be trusted based on the remote IP address.
    /// SECURITY: Prevents header spoofing by validating the request originates from a trusted proxy.
    /// </summary>
    /// <param name="request">The HTTP request</param>
    /// <returns>True if forwarded headers should be trusted; otherwise, false.</returns>
    private static bool ShouldTrustForwardedHeaders(HttpRequest request)
    {
        // Try to get TrustedProxyValidator from DI container
        var validator = request.HttpContext.RequestServices.GetService<TrustedProxyValidator>();

        // If no validator is configured, default to NOT trusting forwarded headers for security
        // Users must explicitly configure TrustedProxies in appsettings.json to enable this feature
        if (validator == null || !validator.IsEnabled)
        {
            return false;
        }

        // Validate that the connection comes from a trusted proxy
        var remoteIp = request.HttpContext.Connection.RemoteIpAddress;
        return validator.IsTrustedProxy(remoteIp);
    }

    /// <summary>
    /// Parses current request query parameters into a mutable dictionary.
    /// </summary>
    private static Dictionary<string, string?> ParseQueryParameters(HttpRequest request)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in request.Query)
        {
            var value = kvp.Value.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                result[kvp.Key] = value;
            }
        }

        return result;
    }

    #endregion
}

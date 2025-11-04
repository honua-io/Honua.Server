// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Service for managing cache headers on OGC API responses
/// </summary>
public sealed class OgcCacheHeaderService
{
    private readonly CacheHeaderOptions _options;

    public OgcCacheHeaderService(IOptions<CacheHeaderOptions> options)
    {
        Guard.NotNull(options);
        _options = options.Value;
    }

    /// <summary>
    /// Applies appropriate cache headers based on resource type
    /// </summary>
    public void ApplyCacheHeaders(HttpContext context, OgcResourceType resourceType, string? etag = null, DateTimeOffset? lastModified = null)
    {
        Guard.NotNull(context);

        if (!_options.EnableCaching)
        {
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
            return;
        }

        var cacheControl = BuildCacheControlHeader(resourceType);
        context.Response.Headers.CacheControl = cacheControl;

        // Add ETag if provided and enabled
        if (_options.EnableETagGeneration && etag.HasValue())
        {
            context.Response.Headers.ETag = etag;
        }

        // Add Last-Modified if provided and enabled
        if (_options.EnableLastModifiedHeaders && lastModified.HasValue)
        {
            context.Response.Headers.LastModified = lastModified.Value.ToString("R");
        }

        // Add Vary headers for content negotiation
        if (_options.VaryHeaders?.Length > 0)
        {
            context.Response.Headers.Vary = string.Join(", ", _options.VaryHeaders);
        }
    }

    /// <summary>
    /// Checks if the request should return 304 Not Modified based on conditional headers
    /// </summary>
    public bool ShouldReturn304NotModified(HttpContext context, string? etag, DateTimeOffset? lastModified)
    {
        Guard.NotNull(context);

        if (!_options.EnableConditionalRequests)
        {
            return false;
        }

        var request = context.Request;

        // Check If-None-Match (ETag validation)
        if (_options.EnableETagGeneration && etag.HasValue())
        {
            if (request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var ifNoneMatchValues))
            {
                var ifNoneMatch = ifNoneMatchValues.ToString();
                if (ifNoneMatch.HasValue())
                {
                    // Handle wildcard
                    if (ifNoneMatch == "*")
                    {
                        return true;
                    }

                    // Parse multiple ETags
                    var requestedEtags = ifNoneMatch
                        .Split(',')
                        .Select(e => e.Trim())
                        .Where(e => !e.IsNullOrEmpty());

                    foreach (var requestedEtag in requestedEtags)
                    {
                        if (string.Equals(requestedEtag, etag, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        // Check If-Modified-Since (Last-Modified validation)
        if (_options.EnableLastModifiedHeaders && lastModified.HasValue)
        {
            if (request.Headers.TryGetValue(HeaderNames.IfModifiedSince, out var ifModifiedSinceValues))
            {
                var ifModifiedSinceStr = ifModifiedSinceValues.ToString();
                if (ifModifiedSinceStr.HasValue())
                {
                    if (DateTimeOffset.TryParse(ifModifiedSinceStr, out var ifModifiedSince))
                    {
                        // Truncate to seconds for comparison (HTTP dates don't include milliseconds)
                        var lastModifiedTruncated = new DateTimeOffset(
                            lastModified.Value.Year,
                            lastModified.Value.Month,
                            lastModified.Value.Day,
                            lastModified.Value.Hour,
                            lastModified.Value.Minute,
                            lastModified.Value.Second,
                            lastModified.Value.Offset);

                        var ifModifiedSinceTruncated = new DateTimeOffset(
                            ifModifiedSince.Year,
                            ifModifiedSince.Month,
                            ifModifiedSince.Day,
                            ifModifiedSince.Hour,
                            ifModifiedSince.Minute,
                            ifModifiedSince.Second,
                            ifModifiedSince.Offset);

                        if (lastModifiedTruncated <= ifModifiedSinceTruncated)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Generates an ETag for the given content
    /// </summary>
    public string GenerateETag(string content)
    {
        Guard.NotNull(content);
        var bytes = Encoding.UTF8.GetBytes(content);
        return GenerateETag(bytes);
    }

    /// <summary>
    /// Generates an ETag for the given content bytes
    /// </summary>
    public string GenerateETag(byte[] content)
    {
        Guard.NotNull(content);
        var hash = SHA256.HashData(content);
        var hashString = Convert.ToHexString(hash);
        return $"\"{hashString}\"";
    }

    /// <summary>
    /// Generates an ETag for an object by serializing it to JSON
    /// </summary>
    public string GenerateETagForObject(object obj)
    {
        Guard.NotNull(obj);
        try
        {
            var json = JsonSerializer.Serialize(obj, JsonSerializerOptionsRegistry.Web);
            return GenerateETag(json);
        }
        catch (NotSupportedException)
        {
            var json = JsonSerializer.Serialize(obj, RuntimeSerializerOptions);
            return GenerateETag(json);
        }
    }

    private static readonly JsonSerializerOptions RuntimeSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 64,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Builds the Cache-Control header value based on resource type
    /// </summary>
    private string BuildCacheControlHeader(OgcResourceType resourceType)
    {
        var parts = new System.Collections.Generic.List<string>();

        // Public or private
        if (_options.UsePublicCacheDirective)
        {
            parts.Add("public");
        }
        else
        {
            parts.Add("private");
        }

        // Max-age and immutable flags
        switch (resourceType)
        {
            case OgcResourceType.Tile:
                parts.Add($"max-age={_options.TileCacheDurationSeconds}");
                if (_options.MarkTilesAsImmutable)
                {
                    parts.Add("immutable");
                }
                break;

            case OgcResourceType.Metadata:
                parts.Add($"max-age={_options.MetadataCacheDurationSeconds}");
                break;

            case OgcResourceType.Feature:
                parts.Add($"max-age={_options.FeatureCacheDurationSeconds}");
                break;

            case OgcResourceType.Style:
                parts.Add($"max-age={_options.StyleCacheDurationSeconds}");
                break;

            case OgcResourceType.TileMatrixSet:
            case OgcResourceType.ApiDefinition:
            case OgcResourceType.Queryables:
                parts.Add($"max-age={_options.TileMatrixSetCacheDurationSeconds}");
                break;

            default:
                parts.Add($"max-age={_options.MetadataCacheDurationSeconds}");
                break;
        }

        return string.Join(", ", parts);
    }
}

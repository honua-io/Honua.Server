// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.AspNetCore.Http;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Extension methods for applying cache headers to OGC API responses
/// </summary>
public static class OgcCacheExtensions
{
    /// <summary>
    /// Wraps the result with cache headers
    /// </summary>
    public static IResult WithCacheHeaders(
        this IResult result,
        OgcCacheHeaderService cacheService,
        OgcResourceType resourceType,
        string? etag = null,
        DateTimeOffset? lastModified = null)
    {
        Guard.NotNull(result);
        Guard.NotNull(cacheService);

        return new CachedResult(result, cacheService, resourceType, etag, lastModified);
    }

    /// <summary>
    /// Wraps the result with cache headers for tile resources (immutable)
    /// </summary>
    public static IResult WithTileCacheHeaders(
        this IResult result,
        OgcCacheHeaderService cacheService,
        string? etag = null)
    {
        return result.WithCacheHeaders(cacheService, OgcResourceType.Tile, etag, null);
    }

    /// <summary>
    /// Wraps the result with cache headers for metadata resources
    /// </summary>
    public static IResult WithMetadataCacheHeaders(
        this IResult result,
        OgcCacheHeaderService cacheService,
        string? etag = null,
        DateTimeOffset? lastModified = null)
    {
        return result.WithCacheHeaders(cacheService, OgcResourceType.Metadata, etag, lastModified);
    }

    /// <summary>
    /// Wraps the result with cache headers for feature resources
    /// </summary>
    public static IResult WithFeatureCacheHeaders(
        this IResult result,
        OgcCacheHeaderService cacheService,
        string? etag = null,
        DateTimeOffset? lastModified = null)
    {
        return result.WithCacheHeaders(cacheService, OgcResourceType.Feature, etag, lastModified);
    }

    /// <summary>
    /// Wraps the result with cache headers for style resources
    /// </summary>
    public static IResult WithStyleCacheHeaders(
        this IResult result,
        OgcCacheHeaderService cacheService,
        string? etag = null,
        DateTimeOffset? lastModified = null)
    {
        return result.WithCacheHeaders(cacheService, OgcResourceType.Style, etag, lastModified);
    }

    /// <summary>
    /// Wraps the result with cache headers for tile matrix set resources
    /// </summary>
    public static IResult WithTileMatrixSetCacheHeaders(
        this IResult result,
        OgcCacheHeaderService cacheService,
        string? etag = null)
    {
        return result.WithCacheHeaders(cacheService, OgcResourceType.TileMatrixSet, etag, null);
    }
}

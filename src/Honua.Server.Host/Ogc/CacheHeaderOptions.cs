// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Configuration options for OGC API response caching headers
/// </summary>
public sealed class CacheHeaderOptions
{
    /// <summary>
    /// Cache duration for tile resources (immutable content)
    /// Default: 1 year (31536000 seconds)
    /// </summary>
    [Range(0, 31536000, ErrorMessage = "TileCacheDurationSeconds must be between 0 and 31536000 (1 year)")]
    public int TileCacheDurationSeconds { get; set; } = 31536000;

    /// <summary>
    /// Cache duration for metadata resources (collections, landing page, etc.)
    /// Default: 1 hour (3600 seconds)
    /// </summary>
    [Range(0, 86400, ErrorMessage = "MetadataCacheDurationSeconds must be between 0 and 86400 (1 day)")]
    public int MetadataCacheDurationSeconds { get; set; } = 3600;

    /// <summary>
    /// Cache duration for feature resources
    /// Default: 5 minutes (300 seconds)
    /// </summary>
    [Range(0, 3600, ErrorMessage = "FeatureCacheDurationSeconds must be between 0 and 3600 (1 hour)")]
    public int FeatureCacheDurationSeconds { get; set; } = 300;

    /// <summary>
    /// Cache duration for style resources
    /// Default: 1 hour (3600 seconds)
    /// </summary>
    [Range(0, 86400, ErrorMessage = "StyleCacheDurationSeconds must be between 0 and 86400 (1 day)")]
    public int StyleCacheDurationSeconds { get; set; } = 3600;

    /// <summary>
    /// Cache duration for tile matrix set definitions
    /// Default: 1 week (604800 seconds)
    /// </summary>
    [Range(0, 2592000, ErrorMessage = "TileMatrixSetCacheDurationSeconds must be between 0 and 2592000 (30 days)")]
    public int TileMatrixSetCacheDurationSeconds { get; set; } = 604800;

    /// <summary>
    /// Whether to mark tile resources as immutable
    /// Default: true
    /// </summary>
    public bool MarkTilesAsImmutable { get; set; } = true;

    /// <summary>
    /// Whether to enable ETag generation for cacheable resources
    /// Default: true
    /// </summary>
    public bool EnableETagGeneration { get; set; } = true;

    /// <summary>
    /// Whether to enable Last-Modified headers for cacheable resources
    /// Default: true
    /// </summary>
    public bool EnableLastModifiedHeaders { get; set; } = true;

    /// <summary>
    /// Whether to enable conditional request support (If-None-Match, If-Modified-Since)
    /// Default: true
    /// </summary>
    public bool EnableConditionalRequests { get; set; } = true;

    /// <summary>
    /// Whether caching is enabled globally
    /// Default: true
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Whether to use public cache directive (allows CDN caching)
    /// Default: true
    /// </summary>
    public bool UsePublicCacheDirective { get; set; } = true;

    /// <summary>
    /// Additional Vary headers to include for content negotiation
    /// Default: Accept, Accept-Encoding, Accept-Language
    /// </summary>
    public string[] VaryHeaders { get; set; } = new[] { "Accept", "Accept-Encoding", "Accept-Language" };
}

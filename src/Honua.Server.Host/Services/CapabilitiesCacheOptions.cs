// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Host.Services;

/// <summary>
/// Configuration options for OGC capabilities document caching.
/// Controls TTL, size limits, and feature flags for GetCapabilities response caching.
/// </summary>
/// <remarks>
/// <para>
/// These settings apply to all OGC service capabilities caching including:
/// WFS GetCapabilities, WMS GetCapabilities, WCS GetCapabilities, WMTS GetCapabilities,
/// and CSW GetCapabilities responses.
/// </para>
/// <para>
/// <strong>Performance Tuning Guidelines:</strong>
/// <list type="bullet">
/// <item>
/// <strong>CacheDurationMinutes:</strong> Higher values reduce server load but increase
/// staleness when metadata changes. Default 10 minutes balances freshness and performance.
/// </item>
/// <item>
/// <strong>MaxCachedDocuments:</strong> Each document is ~10-100KB. 100 documents ≈ 1-10MB memory.
/// Increase for servers with many services and language variants.
/// </item>
/// <item>
/// <strong>EnableCaching:</strong> Disable for development/testing to always see fresh metadata.
/// Always enable in production for optimal performance.
/// </item>
/// </list>
/// </para>
/// </remarks>
public sealed class CapabilitiesCacheOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "CapabilitiesCache";

    /// <summary>
    /// Gets or sets whether capabilities caching is enabled.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// When disabled, GetCapabilities responses are generated fresh on every request.
    /// Useful for development but should be enabled in production.
    /// </remarks>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache duration for capabilities documents in minutes.
    /// Default is 10 minutes as per PERFORMANCE_OPTIMIZATION_OPPORTUNITIES.md recommendation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This TTL applies to all OGC service capabilities documents. After expiration,
    /// the next GetCapabilities request will regenerate the document.
    /// </para>
    /// <para>
    /// <strong>Tuning Guidance:</strong>
    /// <list type="bullet">
    /// <item>5 minutes: High metadata change frequency, prioritize freshness</item>
    /// <item>10 minutes: Balanced (default, recommended for most deployments)</item>
    /// <item>15-30 minutes: Low metadata change frequency, prioritize performance</item>
    /// <item>60+ minutes: Static metadata, maximum performance (use with caution)</item>
    /// </list>
    /// </para>
    /// </remarks>
    [Range(1, 1440)] // 1 minute to 24 hours
    public int CacheDurationMinutes { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of capabilities documents to cache.
    /// Default is 100 as per PERFORMANCE_OPTIMIZATION_OPPORTUNITIES.md recommendation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each cached document includes all language/version variants for a service.
    /// A typical deployment with 10 services × 2 versions × 3 languages = 60 cache entries.
    /// </para>
    /// <para>
    /// <strong>Memory Estimation:</strong>
    /// <list type="bullet">
    /// <item>Small capabilities (~10KB): 100 entries ≈ 1MB</item>
    /// <item>Medium capabilities (~50KB): 100 entries ≈ 5MB</item>
    /// <item>Large capabilities (~100KB): 100 entries ≈ 10MB</item>
    /// </list>
    /// </para>
    /// <para>
    /// Set to 0 for unlimited caching (not recommended for production).
    /// </para>
    /// </remarks>
    [Range(0, 1000)]
    public int MaxCachedDocuments { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to automatically invalidate cache on metadata changes.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// When enabled, the cache listens to MetadataRegistry change tokens and automatically
    /// invalidates affected capabilities documents when metadata is updated.
    /// Disable only for testing or if using external cache invalidation mechanisms.
    /// </remarks>
    public bool AutoInvalidateOnMetadataChange { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include Accept-Language header in cache key.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When enabled, capabilities documents are cached separately per language.
    /// This supports multilingual deployments but increases cache entry count.
    /// </para>
    /// <para>
    /// Disable if your deployment:
    /// <list type="bullet">
    /// <item>Only serves a single language</item>
    /// <item>Doesn't provide language-specific metadata</item>
    /// <item>Wants to reduce cache memory usage</item>
    /// </list>
    /// </para>
    /// </remarks>
    public bool CachePerLanguage { get; set; } = true;
}

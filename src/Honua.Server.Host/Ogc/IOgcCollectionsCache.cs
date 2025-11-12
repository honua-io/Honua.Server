// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Cache for OGC API collections list responses to improve performance
/// by avoiding repeated metadata queries and serialization.
/// </summary>
/// <remarks>
/// <para>
/// This cache addresses the performance opportunity identified in PERFORMANCE_OPTIMIZATION_OPPORTUNITIES.md
/// where collections list responses are regenerated on every request. By caching both JSON and HTML
/// representations, we eliminate redundant metadata queries and improve response times.
/// </para>
/// <para>
/// <strong>Cache Key Strategy:</strong>
/// Keys are formatted as "ogc:collections:{service_id}:{format}:{accept_language}" to provide:
/// <list type="bullet">
/// <item>Service-specific isolation for multi-service deployments</item>
/// <item>Format-specific caching (JSON vs HTML)</item>
/// <item>Language-specific caching for internationalization support</item>
/// <item>Simple prefix-based invalidation patterns</item>
/// </list>
/// </para>
/// <para>
/// <strong>Invalidation Scenarios:</strong>
/// <list type="bullet">
/// <item>Service configuration changes (layers added/removed/modified)</item>
/// <item>Layer metadata updates (title, description, extent, etc.)</item>
/// <item>Service enablement changes</item>
/// <item>Administrative metadata reloads</item>
/// <item>TTL expiration (configurable, default 10 minutes)</item>
/// </list>
/// </para>
/// </remarks>
public interface IOgcCollectionsCache
{
    /// <summary>
    /// Attempts to retrieve a cached collections list response.
    /// </summary>
    /// <param name="serviceId">The service identifier, or null for all services.</param>
    /// <param name="format">The response format (json, html).</param>
    /// <param name="acceptLanguage">The Accept-Language header value for i18n support.</param>
    /// <param name="response">The cached response if found.</param>
    /// <returns>True if the response was found in cache; otherwise, false.</returns>
    bool TryGetCollections(string? serviceId, string format, string? acceptLanguage, out OgcCollectionsCacheEntry? response);

    /// <summary>
    /// Stores a collections list response in the cache.
    /// </summary>
    /// <param name="serviceId">The service identifier, or null for all services.</param>
    /// <param name="format">The response format (json, html).</param>
    /// <param name="acceptLanguage">The Accept-Language header value for i18n support.</param>
    /// <param name="content">The response content to cache.</param>
    /// <param name="contentType">The content type of the response.</param>
    /// <param name="etag">The ETag value for the response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetCollectionsAsync(
        string? serviceId,
        string format,
        string? acceptLanguage,
        string content,
        string contentType,
        string etag,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cached collections for a specific service.
    /// </summary>
    /// <param name="serviceId">The service identifier to invalidate.</param>
    void InvalidateService(string serviceId);

    /// <summary>
    /// Invalidates all cached collections entries.
    /// </summary>
    void InvalidateAll();

    /// <summary>
    /// Gets cache statistics for monitoring and diagnostics.
    /// </summary>
    /// <returns>Cache statistics including hit rate and entry count.</returns>
    OgcCollectionsCacheStatistics GetStatistics();
}

/// <summary>
/// Represents a cached collections list response.
/// </summary>
public sealed record OgcCollectionsCacheEntry
{
    /// <summary>
    /// The cached response content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// The content type of the cached response.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// The ETag value for cache validation.
    /// </summary>
    public required string ETag { get; init; }

    /// <summary>
    /// The timestamp when this entry was cached.
    /// </summary>
    public required DateTimeOffset CachedAt { get; init; }
}

/// <summary>
/// Statistics for the OGC collections cache.
/// </summary>
public sealed record OgcCollectionsCacheStatistics
{
    /// <summary>
    /// Total number of cache hits.
    /// </summary>
    public required long Hits { get; init; }

    /// <summary>
    /// Total number of cache misses.
    /// </summary>
    public required long Misses { get; init; }

    /// <summary>
    /// Total number of cache invalidations.
    /// </summary>
    public required long Invalidations { get; init; }

    /// <summary>
    /// Total number of cache evictions.
    /// </summary>
    public required long Evictions { get; init; }

    /// <summary>
    /// Current number of cached entries.
    /// </summary>
    public required int EntryCount { get; init; }

    /// <summary>
    /// Maximum number of entries allowed.
    /// </summary>
    public required int MaxEntries { get; init; }

    /// <summary>
    /// Cache hit rate (0.0 to 1.0).
    /// </summary>
    public required double HitRate { get; init; }
}

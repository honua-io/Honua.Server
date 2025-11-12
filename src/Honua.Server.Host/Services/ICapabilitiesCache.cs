// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Host.Services;

/// <summary>
/// Cache for OGC service capabilities documents (GetCapabilities responses).
/// Supports WFS, WMS, WCS, WMTS, and CSW capabilities caching with automatic invalidation.
/// </summary>
/// <remarks>
/// <para>
/// This cache addresses the performance issue identified in PERFORMANCE_OPTIMIZATION_OPPORTUNITIES.md
/// where GetCapabilities responses are regenerated on every request. By caching the generated
/// XML/JSON documents, we eliminate redundant metadata queries and XML serialization overhead.
/// </para>
/// <para>
/// <strong>Cache Key Strategy:</strong>
/// Keys are formatted as "{service_type}:{service_id}:capabilities:{version}:{accept_language}"
/// <list type="bullet">
/// <item>service_type: wfs, wms, wcs, wmts, csw</item>
/// <item>service_id: unique identifier for the service (or "global" for server-wide capabilities)</item>
/// <item>version: protocol version (e.g., "2.0.0", "1.3.0")</item>
/// <item>accept_language: client language preference (e.g., "en", "es", or "default")</item>
/// </list>
/// </para>
/// <para>
/// <strong>Performance Impact:</strong>
/// GetCapabilities requests can represent 10-20% of total traffic. Caching reduces:
/// <list type="bullet">
/// <item>XML/JSON serialization overhead (50-100ms saved per request)</item>
/// <item>Metadata registry lookups (10-20ms saved per request)</item>
/// <item>CPU usage for repeated document generation</item>
/// </list>
/// </para>
/// </remarks>
public interface ICapabilitiesCache
{
    /// <summary>
    /// Attempts to retrieve a cached capabilities document.
    /// </summary>
    /// <param name="serviceType">The OGC service type (wfs, wms, wcs, wmts, csw).</param>
    /// <param name="serviceId">The service identifier or "global" for server-wide capabilities.</param>
    /// <param name="version">The protocol version (e.g., "2.0.0").</param>
    /// <param name="acceptLanguage">The client language preference (e.g., "en") or null for default.</param>
    /// <param name="cachedDocument">The cached document when available.</param>
    /// <returns>True if a cached document was found; otherwise false.</returns>
    bool TryGetCapabilities(
        string serviceType,
        string serviceId,
        string version,
        string? acceptLanguage,
        out string? cachedDocument);

    /// <summary>
    /// Stores a capabilities document in the cache with configured TTL.
    /// </summary>
    /// <param name="serviceType">The OGC service type (wfs, wms, wcs, wmts, csw).</param>
    /// <param name="serviceId">The service identifier or "global" for server-wide capabilities.</param>
    /// <param name="version">The protocol version (e.g., "2.0.0").</param>
    /// <param name="acceptLanguage">The client language preference (e.g., "en") or null for default.</param>
    /// <param name="document">The capabilities document to cache.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetCapabilitiesAsync(
        string serviceType,
        string serviceId,
        string version,
        string? acceptLanguage,
        string document,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all cached capabilities documents for a specific service.
    /// Called when service metadata is updated.
    /// </summary>
    /// <param name="serviceType">The OGC service type to invalidate.</param>
    /// <param name="serviceId">The service identifier or null to invalidate all services of this type.</param>
    void InvalidateService(string serviceType, string? serviceId = null);

    /// <summary>
    /// Invalidates all cached capabilities documents across all services.
    /// Called when server metadata is reloaded or global configuration changes.
    /// </summary>
    void InvalidateAll();

    /// <summary>
    /// Gets cache performance statistics for monitoring.
    /// </summary>
    /// <returns>Statistics including hit rate, entry count, and eviction count.</returns>
    CapabilitiesCacheStatistics GetStatistics();
}

/// <summary>
/// Statistics for capabilities cache performance monitoring.
/// </summary>
public sealed record CapabilitiesCacheStatistics
{
    /// <summary>
    /// Total number of cache hits since startup.
    /// </summary>
    public long Hits { get; init; }

    /// <summary>
    /// Total number of cache misses since startup.
    /// </summary>
    public long Misses { get; init; }

    /// <summary>
    /// Total number of cache evictions since startup.
    /// </summary>
    public long Evictions { get; init; }

    /// <summary>
    /// Current number of entries in the cache.
    /// </summary>
    public int EntryCount { get; init; }

    /// <summary>
    /// Maximum number of entries allowed in the cache.
    /// </summary>
    public int MaxEntries { get; init; }

    /// <summary>
    /// Cache hit rate (hits / (hits + misses)).
    /// </summary>
    public double HitRate { get; init; }
}

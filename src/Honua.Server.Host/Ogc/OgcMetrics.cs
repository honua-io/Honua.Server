// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics.Metrics;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Metrics for OGC API operations and caching.
/// </summary>
internal static class OgcMetrics
{
    private const string MeterName = "Honua.Ogc";

    /// <summary>
    /// Meter instance for OGC API operations.
    /// </summary>
    public static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>
    /// Counter for collections list cache hits.
    /// </summary>
    public static readonly Counter<long> CollectionsCacheHits = Meter.CreateCounter<long>(
        "honua.ogc.collections_cache.hits",
        description: "Number of OGC API collections list cache hits");

    /// <summary>
    /// Counter for collections list cache misses.
    /// </summary>
    public static readonly Counter<long> CollectionsCacheMisses = Meter.CreateCounter<long>(
        "honua.ogc.collections_cache.misses",
        description: "Number of OGC API collections list cache misses");

    /// <summary>
    /// Counter for collections list cache invalidations.
    /// </summary>
    public static readonly Counter<long> CollectionsCacheInvalidations = Meter.CreateCounter<long>(
        "honua.ogc.collections_cache.invalidations",
        description: "Number of OGC API collections list cache invalidations");
}

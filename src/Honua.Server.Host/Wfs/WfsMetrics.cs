// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics.Metrics;

namespace Honua.Server.Host.Wfs;

/// <summary>
/// Metrics for WFS (Web Feature Service) operations.
/// </summary>
internal static class WfsMetrics
{
    private const string MeterName = "Honua.Wfs";

    /// <summary>
    /// Meter instance for WFS operations.
    /// </summary>
    public static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>
    /// Counter for schema cache hits.
    /// </summary>
    public static readonly Counter<long> SchemaCacheHits = Meter.CreateCounter<long>(
        "honua.wfs.schema_cache.hits",
        description: "Number of WFS schema cache hits");

    /// <summary>
    /// Counter for schema cache misses.
    /// </summary>
    public static readonly Counter<long> SchemaCacheMisses = Meter.CreateCounter<long>(
        "honua.wfs.schema_cache.misses",
        description: "Number of WFS schema cache misses");
}

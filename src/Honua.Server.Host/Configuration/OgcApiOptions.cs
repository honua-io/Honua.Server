// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Host.Configuration;

/// <summary>
/// Configuration options for OGC API Features and Tiles implementations.
/// Controls pagination, overlay fetching, and tile matrix parameters.
/// </summary>
public sealed class OgcApiOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "OgcApi";

    /// <summary>
    /// Default page size for feature collections when limit is not specified in the request.
    /// This applies to both OGC API Features and WFS GetFeature requests.
    /// </summary>
    [Range(1, 10000)]
    public int DefaultPageSize { get; set; } = 10;

    /// <summary>
    /// Maximum allowed limit for feature queries.
    /// This is used as a fallback when service.Ogc.ItemLimit is not specified.
    /// Individual layers can override this with their own Query.MaxRecordCount setting.
    /// </summary>
    [Range(1, 100000)]
    public int MaxItemLimit { get; set; } = 1000;

    /// <summary>
    /// Batch size for fetching overlay geometries from the database.
    /// Larger batches improve performance but consume more memory.
    /// </summary>
    [Range(10, 10000)]
    public int OverlayFetchBatchSize { get; set; } = 500;

    /// <summary>
    /// Maximum number of overlay geometries to fetch before truncating results.
    /// This prevents excessive memory usage when processing large overlay datasets.
    /// </summary>
    [Range(100, 100000)]
    public int OverlayFetchMaxFeatures { get; set; } = 10_000;

    /// <summary>
    /// Default minimum zoom level for tile matrix sets.
    /// Used when generating tile matrix metadata for OGC API Tiles.
    /// </summary>
    [Range(0, 30)]
    public int DefaultMinZoom { get; set; } = 0;

    /// <summary>
    /// Default maximum zoom level for tile matrix sets.
    /// Used when generating tile matrix metadata for OGC API Tiles.
    /// Higher zoom levels provide more detail but increase tile count exponentially.
    /// </summary>
    [Range(0, 30)]
    public int DefaultMaxZoom { get; set; } = 14;
}

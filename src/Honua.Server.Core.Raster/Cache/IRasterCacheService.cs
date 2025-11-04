// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Cache;

/// <summary>
/// Service for caching and managing converted raster data (COG/Zarr).
/// Implements the hybrid storage architecture: COG cache for spatial + Zarr for time-series.
/// </summary>
public interface IRasterCacheService
{
    /// <summary>
    /// Get COG from cache, or convert and cache if missing (lazy conversion).
    /// </summary>
    /// <param name="dataset">The raster dataset definition</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>URI to the cached COG file</returns>
    Task<string> GetOrConvertToCogAsync(RasterDatasetDefinition dataset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pre-convert source to COG and cache (ingestion pipeline).
    /// </summary>
    /// <param name="sourceUri">Source raster URI (NetCDF, HDF5, GRIB2, etc.)</param>
    /// <param name="options">Conversion options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>URI to the cached COG file</returns>
    Task<string> ConvertToCogAsync(string sourceUri, CogConversionOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if COG cache is stale (source updated more recently than cache).
    /// </summary>
    /// <param name="cachedUri">URI to cached COG</param>
    /// <param name="sourceUri">URI to source raster</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cache is stale and needs refresh</returns>
    Task<bool> IsCacheStaleAsync(string cachedUri, string sourceUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate cache entry for a specific dataset.
    /// </summary>
    /// <param name="datasetId">Dataset ID to invalidate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InvalidateCacheAsync(string datasetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cache statistics (size, hit rate, etc.).
    /// </summary>
    Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for COG conversion.
/// </summary>
public sealed record CogConversionOptions
{
    /// <summary>
    /// Compression algorithm (DEFLATE, LZW, JPEG, WEBP, ZSTD, etc.).
    /// </summary>
    public string Compression { get; init; } = "DEFLATE";

    /// <summary>
    /// Tile/block size (256, 512, etc.).
    /// </summary>
    public int BlockSize { get; init; } = 512;

    /// <summary>
    /// Overview resampling method (NEAREST, BILINEAR, CUBIC, AVERAGE, etc.).
    /// </summary>
    public string OverviewResampling { get; init; } = "BILINEAR";

    /// <summary>
    /// Number of threads for conversion (ALL_CPUS, or specific number).
    /// </summary>
    public string NumThreads { get; init; } = "ALL_CPUS";

    /// <summary>
    /// For NetCDF/HDF5: Variable name to extract.
    /// </summary>
    public string? VariableName { get; init; }

    /// <summary>
    /// For NetCDF/HDF5: Time index to extract (for time-series data).
    /// </summary>
    public int? TimeIndex { get; init; }

    /// <summary>
    /// For NetCDF/HDF5: Vertical level to extract (for 3D data).
    /// </summary>
    public double? Level { get; init; }

    /// <summary>
    /// Output CRS (EPSG code), or null to preserve source CRS.
    /// </summary>
    public string? TargetCrs { get; init; }

    /// <summary>
    /// Generate overviews (pyramids) for efficient zooming.
    /// </summary>
    public bool GenerateOverviews { get; init; } = true;

    /// <summary>
    /// Add geospatial metadata tags.
    /// </summary>
    public bool AddGeoTiffTags { get; init; } = true;
}

/// <summary>
/// Cache statistics.
/// </summary>
public sealed record CacheStatistics
{
    public long TotalEntries { get; init; }
    public long TotalSizeBytes { get; init; }
    public double HitRate { get; init; }
    public DateTime LastCleanup { get; init; }
    public long CacheHits { get; init; }
    public long CacheMisses { get; init; }
    public long CollisionDetections { get; init; }
}

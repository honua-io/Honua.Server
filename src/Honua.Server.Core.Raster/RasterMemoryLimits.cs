// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Raster;

/// <summary>
/// Memory limits for raster operations to prevent OOM conditions.
/// All limits are enforced at runtime with detailed error messages.
/// </summary>
public sealed class RasterMemoryLimits
{
    /// <summary>
    /// Maximum size in bytes for a single Zarr slice request.
    /// Default: 100 MB
    /// </summary>
    public long MaxSliceSizeBytes { get; set; } = 100_000_000;

    /// <summary>
    /// Maximum number of chunks that can be loaded in a single Zarr slice request.
    /// Prevents excessive memory usage from highly chunked arrays.
    /// Default: 100 chunks
    /// </summary>
    public int MaxChunksPerRequest { get; set; } = 100;

    /// <summary>
    /// Maximum number of datasets that can be processed simultaneously in raster algebra.
    /// Default: 5 datasets
    /// </summary>
    public int MaxSimultaneousDatasets { get; set; } = 5;

    /// <summary>
    /// Maximum number of histogram bins allowed.
    /// Prevents excessive memory usage from histogram calculations.
    /// Default: 1000 bins
    /// </summary>
    public int MaxHistogramBins { get; set; } = 1000;

    /// <summary>
    /// Maximum number of vertices allowed in a zonal statistics polygon.
    /// Prevents excessive CPU/memory usage from complex polygon operations.
    /// Default: 10000 vertices
    /// </summary>
    public int MaxZonalPolygonVertices { get; set; } = 10_000;

    /// <summary>
    /// Maximum number of zones allowed in a single zonal statistics request.
    /// Default: 1000 zones
    /// </summary>
    public int MaxZonalPolygons { get; set; } = 1000;

    /// <summary>
    /// Maximum number of points allowed in a value extraction request.
    /// Default: 10000 points
    /// </summary>
    public int MaxExtractionPoints { get; set; } = 10_000;

    /// <summary>
    /// Maximum width or height for raster algebra output.
    /// Prevents creation of excessively large output rasters.
    /// Default: 8192 pixels
    /// </summary>
    public int MaxRasterDimension { get; set; } = 8192;

    /// <summary>
    /// Maximum total pixels for raster algebra output (width * height).
    /// Default: 16 million pixels (4096x4096)
    /// </summary>
    public long MaxRasterPixels { get; set; } = 16_777_216;

    /// <summary>
    /// Validates the configuration and throws if any values are invalid.
    /// </summary>
    public void Validate()
    {
        if (MaxSliceSizeBytes <= 0)
        {
            throw new InvalidOperationException($"{nameof(MaxSliceSizeBytes)} must be greater than zero");
        }

        if (MaxChunksPerRequest <= 0)
        {
            throw new InvalidOperationException($"{nameof(MaxChunksPerRequest)} must be greater than zero");
        }

        if (MaxSimultaneousDatasets <= 0)
        {
            throw new InvalidOperationException($"{nameof(MaxSimultaneousDatasets)} must be greater than zero");
        }

        if (MaxHistogramBins <= 0)
        {
            throw new InvalidOperationException($"{nameof(MaxHistogramBins)} must be greater than zero");
        }

        if (MaxZonalPolygonVertices <= 0)
        {
            throw new InvalidOperationException($"{nameof(MaxZonalPolygonVertices)} must be greater than zero");
        }

        if (MaxZonalPolygons <= 0)
        {
            throw new InvalidOperationException($"{nameof(MaxZonalPolygons)} must be greater than zero");
        }

        if (MaxExtractionPoints <= 0)
        {
            throw new InvalidOperationException($"{nameof(MaxExtractionPoints)} must be greater than zero");
        }

        if (MaxRasterDimension <= 0)
        {
            throw new InvalidOperationException($"{nameof(MaxRasterDimension)} must be greater than zero");
        }

        if (MaxRasterPixels <= 0)
        {
            throw new InvalidOperationException($"{nameof(MaxRasterPixels)} must be greater than zero");
        }
    }
}

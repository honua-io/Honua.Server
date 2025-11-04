// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Cache;

/// <summary>
/// Service for managing time-series raster data in Zarr format.
/// Zarr is a cloud-native chunked array store optimized for multi-dimensional time-series analysis.
/// </summary>
public interface IZarrTimeSeriesService
{
    /// <summary>
    /// Convert NetCDF/HDF5 multi-temporal dataset to Zarr format.
    /// </summary>
    /// <param name="sourceUri">Source dataset URI (NetCDF, HDF5)</param>
    /// <param name="zarrUri">Output Zarr URI (directory or S3 path)</param>
    /// <param name="options">Conversion options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ConvertToZarrAsync(string sourceUri, string zarrUri, ZarrConversionOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query time range from Zarr dataset.
    /// </summary>
    /// <param name="zarrUri">Zarr dataset URI</param>
    /// <param name="variableName">Variable to query (e.g., "temperature", "precipitation")</param>
    /// <param name="startTime">Start of time range</param>
    /// <param name="endTime">End of time range</param>
    /// <param name="bbox">Bounding box [minLon, minLat, maxLon, maxLat], or null for full extent</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Multi-dimensional array [time, lat, lon]</returns>
    Task<float[,,]> QueryTimeRangeAsync(
        string zarrUri,
        string variableName,
        DateTime startTime,
        DateTime endTime,
        double[]? bbox = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query single time slice from Zarr dataset.
    /// </summary>
    /// <param name="zarrUri">Zarr dataset URI</param>
    /// <param name="variableName">Variable to query</param>
    /// <param name="time">Timestamp to query</param>
    /// <param name="bbox">Bounding box, or null for full extent</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>2D array [lat, lon]</returns>
    Task<float[,]> QueryTimeSliceAsync(
        string zarrUri,
        string variableName,
        DateTime time,
        double[]? bbox = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get metadata about Zarr dataset (dimensions, variables, attributes).
    /// </summary>
    Task<ZarrMetadata> GetMetadataAsync(string zarrUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if Zarr dataset exists and is valid.
    /// </summary>
    Task<bool> ExistsAsync(string zarrUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query a single time slice from Zarr dataset with efficient chunk-based retrieval.
    /// </summary>
    /// <param name="zarrPath">Path to Zarr dataset</param>
    /// <param name="variableName">Variable to query</param>
    /// <param name="timestamp">Timestamp to query</param>
    /// <param name="spatialExtent">Optional spatial bounding box</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Time slice data with metadata</returns>
    Task<ZarrTimeSlice> QueryTimeSliceAsync(
        string zarrPath,
        string variableName,
        DateTimeOffset timestamp,
        BoundingBox? spatialExtent = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all available timesteps from Zarr dataset.
    /// </summary>
    /// <param name="zarrPath">Path to Zarr dataset</param>
    /// <param name="variableName">Variable to query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available timestamps</returns>
    Task<IReadOnlyList<DateTimeOffset>> GetTimeStepsAsync(
        string zarrPath,
        string variableName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query a time range from Zarr dataset with optional aggregation.
    /// </summary>
    /// <param name="zarrPath">Path to Zarr dataset</param>
    /// <param name="variableName">Variable to query</param>
    /// <param name="startTime">Start of time range</param>
    /// <param name="endTime">End of time range</param>
    /// <param name="spatialExtent">Optional spatial bounding box</param>
    /// <param name="aggregationInterval">Optional time aggregation interval</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Time series data with metadata</returns>
    Task<ZarrTimeSeriesData> QueryTimeRangeAsync(
        string zarrPath,
        string variableName,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        BoundingBox? spatialExtent = null,
        TimeSpan? aggregationInterval = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a single time slice from Zarr dataset.
/// </summary>
public sealed record ZarrTimeSlice(
    DateTimeOffset Timestamp,
    int TimeIndex,
    byte[] Data,
    Readers.ZarrArrayMetadata Metadata,
    BoundingBox SpatialExtent);

/// <summary>
/// Represents time-series data from Zarr dataset.
/// </summary>
public sealed record ZarrTimeSeriesData(
    IReadOnlyList<DateTimeOffset> Timestamps,
    IReadOnlyList<byte[]> DataSlices,
    Readers.ZarrArrayMetadata Metadata,
    string AggregationMethod);

/// <summary>
/// Time-series aggregation methods.
/// </summary>
public enum ZarrAggregationMethod
{
    None,
    Mean,
    Min,
    Max,
    Sum
}

/// <summary>
/// Options for Zarr conversion.
/// </summary>
public sealed record ZarrConversionOptions
{
    /// <summary>
    /// Variable name to extract from source dataset.
    /// </summary>
    public required string VariableName { get; init; }

    /// <summary>
    /// Chunk size for time dimension.
    /// </summary>
    public int TimeChunkSize { get; init; } = 1;

    /// <summary>
    /// Chunk size for latitude dimension.
    /// </summary>
    public int LatitudeChunkSize { get; init; } = 128;

    /// <summary>
    /// Chunk size for longitude dimension.
    /// </summary>
    public int LongitudeChunkSize { get; init; } = 128;

    /// <summary>
    /// Compression algorithm (zstd, gzip, lz4, blosc).
    /// </summary>
    public string Compression { get; init; } = "zstd";

    /// <summary>
    /// Compression level (1-9 for most compressors).
    /// </summary>
    public int CompressionLevel { get; init; } = 5;

    /// <summary>
    /// Include coordinate variables (time, lat, lon).
    /// </summary>
    public bool IncludeCoordinates { get; init; } = true;

    /// <summary>
    /// Copy attributes from source dataset.
    /// </summary>
    public bool CopyAttributes { get; init; } = true;
}

/// <summary>
/// Zarr dataset metadata.
/// </summary>
public sealed record ZarrMetadata
{
    public required string Uri { get; init; }
    public required ZarrDimensionInfo[] Dimensions { get; init; }
    public required string[] Variables { get; init; }
    public required ZarrAttributeInfo[] Attributes { get; init; }
    public DateTime? TimeStart { get; init; }
    public DateTime? TimeEnd { get; init; }
    public double[]? SpatialExtent { get; init; } // [minLon, minLat, maxLon, maxLat]
}

public sealed record ZarrDimensionInfo(string Name, int Size, int ChunkSize);

public sealed record ZarrAttributeInfo(string Name, object? Value);

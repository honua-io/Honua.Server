// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Readers;

/// <summary>
/// Pure .NET reader for Cloud Optimized GeoTIFF (COG) files.
/// Supports efficient HTTP range requests for remote COGs.
/// </summary>
public interface ICogReader
{
    /// <summary>
    /// Open a COG file for reading.
    /// </summary>
    Task<CogDataset> OpenAsync(string uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read a specific tile from COG.
    /// </summary>
    Task<byte[]> ReadTileAsync(CogDataset dataset, int tileX, int tileY, int level = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read a window/region from COG.
    /// </summary>
    Task<byte[]> ReadWindowAsync(
        CogDataset dataset,
        int x,
        int y,
        int width,
        int height,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get metadata from COG without reading pixel data.
    /// </summary>
    Task<CogMetadata> GetMetadataAsync(string uri, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an opened COG dataset.
/// </summary>
public sealed class CogDataset : IDisposable
{
    public required string Uri { get; init; }
    public required CogMetadata Metadata { get; init; }
    public required Stream Stream { get; init; }
    public object? TiffHandle { get; set; }

    public void Dispose()
    {
        if (TiffHandle is IDisposable disposable)
        {
            disposable.Dispose();
        }
        Stream?.Dispose();
    }
}

/// <summary>
/// COG metadata.
/// </summary>
public sealed record CogMetadata
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int BandCount { get; init; }
    public required int TileWidth { get; init; }
    public required int TileHeight { get; init; }
    public required int BitsPerSample { get; init; }
    public required string Compression { get; init; }
    public required bool IsTiled { get; init; }
    public required bool IsCog { get; init; }
    public required int OverviewCount { get; init; }
    public GeoTransform? GeoTransform { get; init; }
    public string? ProjectionWkt { get; init; }
}

/// <summary>
/// Geospatial transform parameters.
/// </summary>
public sealed record GeoTransform
{
    public required double OriginX { get; init; }
    public required double PixelSizeX { get; init; }
    public required double RotationX { get; init; }
    public required double OriginY { get; init; }
    public required double RotationY { get; init; }
    public required double PixelSizeY { get; init; }
}

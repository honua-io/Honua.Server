// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Readers;

/// <summary>
/// Pure .NET reader for Zarr arrays.
/// Supports HTTP-based access to remote Zarr stores (S3, Azure, GCS, HTTP).
/// </summary>
public interface IZarrReader
{
    /// <summary>
    /// Open a Zarr array for reading.
    /// </summary>
    Task<ZarrArray> OpenArrayAsync(string uri, string variableName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read a chunk from Zarr array.
    /// </summary>
    Task<byte[]> ReadChunkAsync(ZarrArray array, int[] chunkCoords, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read a slice from Zarr array (e.g., specific time step).
    /// </summary>
    Task<Array> ReadSliceAsync(
        ZarrArray array,
        int[] start,
        int[] count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get Zarr array metadata.
    /// </summary>
    Task<ZarrArrayMetadata> GetMetadataAsync(string uri, string variableName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an opened Zarr array.
/// </summary>
public sealed class ZarrArray : IDisposable
{
    public required string Uri { get; init; }
    public required string VariableName { get; init; }
    public required ZarrArrayMetadata Metadata { get; init; }
    internal object? Context { get; set; }

    public void Dispose()
    {
        if (Context is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// Zarr array metadata (.zarray file content).
/// </summary>
public sealed record ZarrArrayMetadata
{
    public required int[] Shape { get; init; }
    public required int[] Chunks { get; init; }
    public required string DType { get; init; }
    public required string Compressor { get; init; }
    public required int ZarrFormat { get; init; }
    public required string Order { get; init; }
    public object? FillValue { get; init; }
    public string[]? DimensionNames { get; init; }
}

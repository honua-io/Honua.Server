// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Raster.Compression;

/// <summary>
/// Interface for compression/decompression codecs used in raster data (Zarr, etc.).
/// </summary>
public interface ICompressionCodec
{
    /// <summary>
    /// Gets the unique identifier for this codec (e.g., "blosc", "gzip", "zstd").
    /// </summary>
    string CodecName { get; }

    /// <summary>
    /// Decompresses data using this codec.
    /// </summary>
    /// <param name="compressedData">The compressed data.</param>
    /// <param name="expectedUncompressedSize">Expected size after decompression (optional, used for validation).</param>
    /// <returns>The decompressed data.</returns>
    /// <exception cref="InvalidOperationException">If decompression fails.</exception>
    byte[] Decompress(byte[] compressedData, int? expectedUncompressedSize = null);

    /// <summary>
    /// Compresses data using this codec.
    /// </summary>
    /// <param name="data">The uncompressed data.</param>
    /// <param name="compressionLevel">Compression level (codec-specific).</param>
    /// <returns>The compressed data.</returns>
    /// <exception cref="NotImplementedException">If compression is not supported.</exception>
    byte[] Compress(byte[] data, int compressionLevel = 5);

    /// <summary>
    /// Gets the support level for this codec (e.g., "Full", "Partial", "Decompression Only").
    /// </summary>
    string GetSupportLevel();
}

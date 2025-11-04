// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Raster.Compression;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Readers;

/// <summary>
/// Decompression service for Zarr chunks.
/// Delegates to CompressionCodecRegistry for codec-specific decompression.
/// </summary>
public sealed class ZarrDecompressor
{
    private readonly ILogger<ZarrDecompressor> _logger;
    private readonly CompressionCodecRegistry _codecRegistry;

    public ZarrDecompressor(
        ILogger<ZarrDecompressor> logger,
        CompressionCodecRegistry codecRegistry)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _codecRegistry = codecRegistry ?? throw new ArgumentNullException(nameof(codecRegistry));
    }

    /// <summary>
    /// Decompress a Zarr chunk based on the compressor type.
    /// </summary>
    public byte[] Decompress(byte[] compressedData, string compressor)
    {
        if (compressor.IsNullOrEmpty() || compressor == "null")
        {
            return compressedData;
        }

        try
        {
            var codec = _codecRegistry.GetCodec(compressor);
            return codec.Decompress(compressedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decompress chunk with codec '{Compressor}'", compressor);
            throw;
        }
    }

    /// <summary>
    /// Check if a compressor is supported.
    /// </summary>
    public bool IsSupported(string compressor)
    {
        return _codecRegistry.IsSupported(compressor);
    }

    /// <summary>
    /// Get support level for a compressor.
    /// </summary>
    public string GetSupportLevel(string compressor)
    {
        if (compressor.IsNullOrEmpty() || compressor == "null")
        {
            return "N/A (uncompressed)";
        }

        try
        {
            var codec = _codecRegistry.GetCodec(compressor);
            return codec.GetSupportLevel();
        }
        catch (NotSupportedException)
        {
            return "Unsupported";
        }
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Utilities;
using K4os.Compression.LZ4;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Raster.Compression;

/// <summary>
/// LZ4 decompressor for Zarr arrays.
/// Uses K4os.Compression.LZ4 for pure .NET implementation.
/// Includes decompression bomb protection with configurable size limits.
/// </summary>
public sealed class Lz4Decompressor : ICompressionCodec
{
    /// <summary>
    /// Maximum allowed decompressed size to prevent decompression bomb attacks.
    /// Default: 500MB (configurable based on application needs).
    /// </summary>
    private const int MaxDecompressedSize = 500 * 1024 * 1024; // 500MB

    private readonly ILogger<Lz4Decompressor> _logger;

    public string CodecName => "lz4";

    public Lz4Decompressor(ILogger<Lz4Decompressor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public byte[] Decompress(byte[] compressedData, int? expectedUncompressedSize = null)
    {
        Guard.NotNull(compressedData);

        // CRITICAL: Validate expected size before decompression to prevent decompression bomb attacks
        if (expectedUncompressedSize.HasValue)
        {
            if (expectedUncompressedSize.Value > MaxDecompressedSize)
            {
                throw new InvalidOperationException(
                    $"Decompression bomb detected: expected uncompressed size ({expectedUncompressedSize.Value:N0} bytes) " +
                    $"exceeds maximum allowed ({MaxDecompressedSize:N0} bytes). " +
                    $"This may be a malicious compressed payload.");
            }

            if (expectedUncompressedSize.Value < 0)
            {
                throw new InvalidOperationException(
                    $"Invalid expected uncompressed size: {expectedUncompressedSize.Value}");
            }
        }

        try
        {
            var decompressedData = LZ4Pickler.Unpickle(compressedData);

            // CRITICAL: Validate actual decompressed size
            if (decompressedData.Length > MaxDecompressedSize)
            {
                throw new InvalidOperationException(
                    $"Decompression bomb detected: decompressed size ({decompressedData.Length:N0} bytes) " +
                    $"exceeds maximum allowed ({MaxDecompressedSize:N0} bytes). " +
                    $"This may be a malicious compressed payload.");
            }

            // Validate size if expected
            if (expectedUncompressedSize.HasValue && decompressedData.Length != expectedUncompressedSize.Value)
            {
                _logger.LogWarning(
                    "LZ4 decompression size mismatch: actual={ActualSize}, expected={ExpectedSize}",
                    decompressedData.Length, expectedUncompressedSize.Value);
            }

            _logger.LogDebug(
                "LZ4 decompression successful: {CompressedSize} bytes -> {DecompressedSize} bytes (ratio: {Ratio:F2}x)",
                compressedData.Length, decompressedData.Length,
                (double)decompressedData.Length / compressedData.Length);

            return decompressedData;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogOperationFailure(ex, "LZ4 decompression");
            throw new InvalidOperationException("LZ4 decompression failed", ex);
        }
    }

    public byte[] Compress(byte[] data, int compressionLevel = 5)
    {
        Guard.NotNull(data);

        try
        {
            var level = ConvertCompressionLevel(compressionLevel);
            var compressedData = LZ4Pickler.Pickle(data, level);

            _logger.LogDebug(
                "LZ4 compression successful: {UncompressedSize} bytes -> {CompressedSize} bytes (ratio: {Ratio:F2}x)",
                data.Length, compressedData.Length,
                (double)data.Length / compressedData.Length);

            return compressedData;
        }
        catch (Exception ex)
        {
            _logger.LogOperationFailure(ex, "LZ4 compression");
            throw new InvalidOperationException("LZ4 compression failed", ex);
        }
    }

    public string GetSupportLevel()
    {
        return "Full";
    }

    private static LZ4Level ConvertCompressionLevel(int level)
    {
        return level switch
        {
            0 => LZ4Level.L00_FAST,
            <= 3 => LZ4Level.L03_HC,
            <= 6 => LZ4Level.L06_HC,
            <= 9 => LZ4Level.L09_HC,
            _ => LZ4Level.L12_MAX
        };
    }
}

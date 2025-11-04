// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Raster.Compression;

/// <summary>
/// Zstandard (Zstd) decompressor for Zarr arrays.
/// Uses ZstdSharp.Port for pure .NET implementation.
/// Includes decompression bomb protection with configurable size limits.
/// </summary>
public sealed class ZstdDecompressor : ICompressionCodec
{
    /// <summary>
    /// Maximum allowed decompressed size to prevent decompression bomb attacks.
    /// Default: 500MB (configurable based on application needs).
    /// </summary>
    private const int MaxDecompressedSize = 500 * 1024 * 1024; // 500MB

    private readonly ILogger<ZstdDecompressor> _logger;

    public string CodecName => "zstd";

    public ZstdDecompressor(ILogger<ZstdDecompressor> logger)
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
            using var decompressor = new ZstdSharp.Decompressor();
            var decompressedData = decompressor.Unwrap(compressedData);
            var result = decompressedData.ToArray();

            // CRITICAL: Validate actual decompressed size
            if (result.Length > MaxDecompressedSize)
            {
                throw new InvalidOperationException(
                    $"Decompression bomb detected: decompressed size ({result.Length:N0} bytes) " +
                    $"exceeds maximum allowed ({MaxDecompressedSize:N0} bytes). " +
                    $"This may be a malicious compressed payload.");
            }

            // Validate size if expected
            if (expectedUncompressedSize.HasValue && result.Length != expectedUncompressedSize.Value)
            {
                _logger.LogWarning(
                    "Zstd decompression size mismatch: actual={ActualSize}, expected={ExpectedSize}",
                    result.Length, expectedUncompressedSize.Value);
            }

            _logger.LogDebug(
                "Zstd decompression successful: {CompressedSize} bytes -> {DecompressedSize} bytes (ratio: {Ratio:F2}x)",
                compressedData.Length, result.Length,
                (double)result.Length / compressedData.Length);

            return result;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogOperationFailure(ex, "Zstd decompression");
            throw new InvalidOperationException("Zstd decompression failed", ex);
        }
    }

    public byte[] Compress(byte[] data, int compressionLevel = 5)
    {
        Guard.NotNull(data);

        if (compressionLevel < 1 || compressionLevel > 22)
        {
            throw new ArgumentOutOfRangeException(nameof(compressionLevel),
                "Zstd compression level must be between 1 and 22");
        }

        try
        {
            using var compressor = new ZstdSharp.Compressor(compressionLevel);
            var compressedData = compressor.Wrap(data);
            var result = compressedData.ToArray();

            _logger.LogDebug(
                "Zstd compression successful: {UncompressedSize} bytes -> {CompressedSize} bytes (ratio: {Ratio:F2}x)",
                data.Length, result.Length,
                (double)data.Length / result.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogOperationFailure(ex, "Zstd compression");
            throw new InvalidOperationException("Zstd compression failed", ex);
        }
    }

    public string GetSupportLevel()
    {
        return "Full";
    }
}

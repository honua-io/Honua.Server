// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.IO.Compression;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Raster.Compression;

/// <summary>
/// Gzip/Zlib decompressor for Zarr arrays.
/// Includes decompression bomb protection with configurable size limits.
/// </summary>
public sealed class GzipDecompressor : ICompressionCodec
{
    /// <summary>
    /// Maximum allowed decompressed size to prevent decompression bomb attacks.
    /// Default: 500MB (configurable based on application needs).
    /// </summary>
    private const int MaxDecompressedSize = 500 * 1024 * 1024; // 500MB

    private readonly ILogger<GzipDecompressor> _logger;

    public string CodecName => "gzip";

    public GzipDecompressor(ILogger<GzipDecompressor> logger)
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
            using var inputStream = new MemoryStream(compressedData);
            using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
            // Use limited-size MemoryStream to prevent unbounded growth
            using var outputStream = new MemoryStream(expectedUncompressedSize ?? 4096);

            gzipStream.CopyTo(outputStream);
            var decompressedData = outputStream.ToArray();

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
                    "Gzip decompression size mismatch: actual={ActualSize}, expected={ExpectedSize}",
                    decompressedData.Length, expectedUncompressedSize.Value);
            }

            _logger.LogDebug(
                "Gzip decompression successful: {CompressedSize} bytes -> {DecompressedSize} bytes (ratio: {Ratio:F2}x)",
                compressedData.Length, decompressedData.Length,
                (double)decompressedData.Length / compressedData.Length);

            return decompressedData;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogOperationFailure(ex, "Gzip decompression");
            throw new InvalidOperationException("Gzip decompression failed", ex);
        }
    }

    public byte[] Compress(byte[] data, int compressionLevel = 5)
    {
        Guard.NotNull(data);

        try
        {
            using var outputStream = new MemoryStream();
            using (var gzipStream = new GZipStream(outputStream,
                ConvertCompressionLevel(compressionLevel)))
            {
                gzipStream.Write(data, 0, data.Length);
            }

            var compressedData = outputStream.ToArray();

            _logger.LogDebug(
                "Gzip compression successful: {UncompressedSize} bytes -> {CompressedSize} bytes (ratio: {Ratio:F2}x)",
                data.Length, compressedData.Length,
                (double)data.Length / compressedData.Length);

            return compressedData;
        }
        catch (Exception ex)
        {
            _logger.LogOperationFailure(ex, "Gzip compression");
            throw new InvalidOperationException("Gzip compression failed", ex);
        }
    }

    public string GetSupportLevel()
    {
        return "Full";
    }

    private static CompressionLevel ConvertCompressionLevel(int level)
    {
        return level switch
        {
            0 => CompressionLevel.NoCompression,
            <= 3 => CompressionLevel.Fastest,
            >= 7 => CompressionLevel.SmallestSize,
            _ => CompressionLevel.Optimal
        };
    }
}

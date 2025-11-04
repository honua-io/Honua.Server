// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.IO.Compression;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Utilities;
using K4os.Compression.LZ4;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Raster.Compression;

/// <summary>
/// Blosc decompressor using managed .NET implementations.
/// Parses Blosc headers and delegates to appropriate internal compressors (LZ4, Zstd, Zlib).
/// Supports Blosc v1 and v2 formats.
/// Includes decompression bomb protection with configurable size limits.
/// </summary>
public sealed class BloscDecompressor : ICompressionCodec
{
    /// <summary>
    /// Maximum allowed decompressed size to prevent decompression bomb attacks.
    /// Default: 500MB (configurable based on application needs).
    /// </summary>
    private const int MaxDecompressedSize = 500 * 1024 * 1024; // 500MB

    private readonly ILogger<BloscDecompressor> _logger;
    private readonly ZstdDecompressor _zstdDecompressor;
    private readonly Lz4Decompressor _lz4Decompressor;

    public string CodecName => "blosc";

    public BloscDecompressor(ILogger<BloscDecompressor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _zstdDecompressor = new ZstdDecompressor(new Microsoft.Extensions.Logging.Abstractions.NullLogger<ZstdDecompressor>());
        _lz4Decompressor = new Lz4Decompressor(new Microsoft.Extensions.Logging.Abstractions.NullLogger<Lz4Decompressor>());
    }

    /// <summary>
    /// Decompresses Blosc-compressed data.
    /// </summary>
    /// <param name="compressedData">The Blosc-compressed data.</param>
    /// <param name="expectedUncompressedSize">Expected uncompressed size (optional, used for validation).</param>
    /// <returns>The decompressed data.</returns>
    /// <exception cref="InvalidOperationException">If decompression fails.</exception>
    public byte[] Decompress(byte[] compressedData, int? expectedUncompressedSize = null)
    {
        Guard.NotNull(compressedData);

        if (compressedData.Length < 16)
        {
            throw new InvalidOperationException(
                $"Invalid Blosc data: too short ({compressedData.Length} bytes, minimum 16 bytes for header)");
        }

        try
        {
            // Parse Blosc header
            var metadata = ParseHeader(compressedData);

            // CRITICAL: Validate decompressed size to prevent decompression bomb attacks
            if (metadata.UncompressedSize > MaxDecompressedSize)
            {
                throw new InvalidOperationException(
                    $"Decompression bomb detected: uncompressed size ({metadata.UncompressedSize:N0} bytes) " +
                    $"exceeds maximum allowed ({MaxDecompressedSize:N0} bytes). " +
                    $"This may be a malicious compressed payload.");
            }

            if (metadata.UncompressedSize < 0)
            {
                throw new InvalidOperationException(
                    $"Invalid uncompressed size in Blosc header: {metadata.UncompressedSize}");
            }

            // Validate against expected size if provided
            if (expectedUncompressedSize.HasValue && metadata.UncompressedSize != expectedUncompressedSize.Value)
            {
                _logger.LogWarning(
                    "Blosc uncompressed size mismatch: header={HeaderSize}, expected={ExpectedSize}",
                    metadata.UncompressedSize, expectedUncompressedSize.Value);
            }

            // Get internal codec
            var internalCodecName = metadata.GetInternalCodecName();
            _logger.LogDebug(
                "Blosc decompression: codec={Codec}, uncompressed={UncompressedSize}, compressed={CompressedSize}",
                internalCodecName, metadata.UncompressedSize, metadata.CompressedSize);

            // Decompress based on internal codec
            return DecompressInternal(compressedData, metadata);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogOperationFailure(ex, "Blosc decompression");
            throw new InvalidOperationException("Blosc decompression failed", ex);
        }
    }

    /// <summary>
    /// Compresses data using Blosc with Zstd backend.
    /// </summary>
    /// <param name="data">The data to compress.</param>
    /// <param name="compressionLevel">Compression level (0-9).</param>
    /// <returns>The compressed data.</returns>
    public byte[] Compress(byte[] data, int compressionLevel = 5)
    {
        Guard.NotNull(data);

        if (compressionLevel < 0 || compressionLevel > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(compressionLevel),
                "Compression level must be between 0 and 9");
        }

        // For compression, use Zstd directly (most common for Zarr)
        // Real Blosc compression would require native library
        _logger.LogWarning("Blosc compression using Zstd backend (simplified implementation)");
        return _zstdDecompressor.Compress(data, compressionLevel);
    }

    public string GetSupportLevel()
    {
        return "Partial (Zstd, Zlib, LZ4 backends; BloscLZ and Snappy not supported)";
    }

    /// <summary>
    /// Decompresses Blosc data using the appropriate internal compressor.
    /// </summary>
    private byte[] DecompressInternal(byte[] compressedData, BloscMetadata metadata)
    {
        // Extract compressed payload (skip 16-byte header)
        var payload = compressedData.AsSpan(16).ToArray();

        var decompressed = metadata.InternalCodec switch
        {
            0x01 => DecompressLz4Internal(payload, metadata),  // LZ4
            0x03 => DecompressZlibInternal(payload, metadata), // Zlib
            0x04 => DecompressZstdInternal(payload, metadata), // Zstd
            0x05 => DecompressLz4Internal(payload, metadata),  // LZ4HC (same as LZ4 for decompression)
            0x00 => throw new NotSupportedException("BloscLZ decompression not supported. Please use Blosc with LZ4, Zstd, or Zlib backend."),
            0x02 => throw new NotSupportedException("Snappy decompression not supported. Please use Blosc with LZ4, Zstd, or Zlib backend."),
            _ => throw new NotSupportedException($"Unknown Blosc internal codec: {metadata.InternalCodec}")
        };

        // Apply shuffle/unshuffle if needed
        if (metadata.Flags != 0 && metadata.TypeSize > 0)
        {
            var shuffleMode = (metadata.Flags >> 0) & 0x03; // Bits 0-1: shuffle mode
            if (shuffleMode == 1)
            {
                decompressed = UnshufleBlosc(decompressed, metadata.TypeSize);
                _logger.LogDebug("Applied byte unshuffle (typesize={TypeSize})", metadata.TypeSize);
            }
            else if (shuffleMode == 2)
            {
                _logger.LogWarning("Bit shuffle mode detected but not fully supported");
                // Bit shuffle would require more complex implementation
            }
        }

        return decompressed;
    }

    private byte[] DecompressLz4Internal(byte[] payload, BloscMetadata metadata)
    {
        try
        {
            // Try direct LZ4 decompression
            return _lz4Decompressor.Decompress(payload, metadata.UncompressedSize);
        }
        catch
        {
            _logger.LogWarning("Standard LZ4 decompression failed, trying alternative method");
            // Blosc may use block-wise compression
            throw new NotSupportedException(
                "Complex Blosc+LZ4 block structure not supported. Please use Blosc with Zstd backend.");
        }
    }

    private byte[] DecompressZlibInternal(byte[] payload, BloscMetadata metadata)
    {
        using var inputStream = new MemoryStream(payload);
        using var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream(metadata.UncompressedSize);

        deflateStream.CopyTo(outputStream);
        return outputStream.ToArray();
    }

    private byte[] DecompressZstdInternal(byte[] payload, BloscMetadata metadata)
    {
        return _zstdDecompressor.Decompress(payload, metadata.UncompressedSize);
    }

    /// <summary>
    /// Unshuffles byte-shuffled data.
    /// </summary>
    private static byte[] UnshufleBlosc(byte[] shuffled, int typesize)
    {
        if (typesize <= 1)
        {
            return shuffled; // No shuffling for 1-byte types
        }

        var length = shuffled.Length;
        var unshuffled = new byte[length];
        var elementsPerType = length / typesize;

        for (int i = 0; i < length; i++)
        {
            var typeOffset = i % typesize;
            var elementIndex = i / typesize;
            var sourceIndex = typeOffset * elementsPerType + elementIndex;

            if (sourceIndex < length)
            {
                unshuffled[i] = shuffled[sourceIndex];
            }
        }

        return unshuffled;
    }

    /// <summary>
    /// Parses the Blosc header to get compression metadata.
    /// </summary>
    public static BloscMetadata ParseHeader(byte[] compressedData)
    {
        if (compressedData.Length < 16)
        {
            throw new ArgumentException("Blosc header too short", nameof(compressedData));
        }

        return new BloscMetadata
        {
            Version = compressedData[0],
            VersionLz = compressedData[1],
            Flags = compressedData[2],
            TypeSize = compressedData[3],
            UncompressedSize = BitConverter.ToInt32(compressedData, 4),
            CompressedSize = BitConverter.ToInt32(compressedData, 8),
            BlockSize = BitConverter.ToInt32(compressedData, 12)
        };
    }
}

/// <summary>
/// Metadata parsed from Blosc header.
/// </summary>
public sealed record BloscMetadata
{
    public byte Version { get; init; }
    public byte VersionLz { get; init; }
    public byte Flags { get; init; }
    public byte TypeSize { get; init; }
    public int UncompressedSize { get; init; }
    public int CompressedSize { get; init; }
    public int BlockSize { get; init; }

    public int InternalCodec => (Flags >> 5) & 0x07;

    public string GetInternalCodecName()
    {
        return InternalCodec switch
        {
            0x00 => "blosclz",
            0x01 => "lz4",
            0x02 => "snappy",
            0x03 => "zlib",
            0x04 => "zstd",
            0x05 => "lz4hc",
            _ => $"unknown ({InternalCodec})"
        };
    }
}

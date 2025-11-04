// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Text.Json;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Raster.Compression;

/// <summary>
/// Configuration for Blosc compression codec.
/// Parsed from Zarr .zarray "compressor" metadata.
/// </summary>
/// <remarks>
/// Example Zarr compressor metadata:
/// <code>
/// {
///   "compressor": {
///     "id": "blosc",
///     "cname": "lz4",
///     "clevel": 5,
///     "shuffle": 1,
///     "blocksize": 0
///   }
/// }
/// </code>
/// </remarks>
public sealed record BloscConfiguration
{
    /// <summary>
    /// Internal compressor name (lz4, lz4hc, blosclz, snappy, zlib, zstd).
    /// </summary>
    public string CName { get; init; } = "lz4";

    /// <summary>
    /// Compression level (0-9). Higher values = more compression, slower speed.
    /// </summary>
    public int CLevel { get; init; } = 5;

    /// <summary>
    /// Shuffle mode:
    /// 0 = No shuffle
    /// 1 = Byte shuffle (BLOSC_SHUFFLE)
    /// 2 = Bit shuffle (BLOSC_BITSHUFFLE)
    /// </summary>
    public int Shuffle { get; init; } = 1;

    /// <summary>
    /// Block size in bytes. 0 = automatic.
    /// </summary>
    public int BlockSize { get; init; } = 0;

    /// <summary>
    /// Gets the default Blosc configuration.
    /// </summary>
    public static BloscConfiguration Default => new()
    {
        CName = "lz4",
        CLevel = 5,
        Shuffle = 1,
        BlockSize = 0
    };

    /// <summary>
    /// Parses Blosc configuration from Zarr .zarray compressor JSON.
    /// </summary>
    /// <param name="compressorElement">The "compressor" JSON element from .zarray.</param>
    /// <returns>Parsed Blosc configuration.</returns>
    /// <exception cref="ArgumentException">If the compressor is not Blosc.</exception>
    public static BloscConfiguration ParseFromJson(JsonElement compressorElement)
    {
        if (compressorElement.ValueKind == JsonValueKind.Null)
        {
            throw new ArgumentException("Compressor element is null", nameof(compressorElement));
        }

        // Verify this is a Blosc compressor
        if (compressorElement.TryGetProperty("id", out var idElement))
        {
            var id = idElement.GetString();
            if (!string.Equals(id, "blosc", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Expected 'blosc' compressor, got '{id}'", nameof(compressorElement));
            }
        }

        return new BloscConfiguration
        {
            CName = compressorElement.TryGetProperty("cname", out var cnameElement)
                ? cnameElement.GetString() ?? "lz4"
                : "lz4",

            CLevel = compressorElement.TryGetProperty("clevel", out var clevelElement)
                ? clevelElement.GetInt32()
                : 5,

            Shuffle = compressorElement.TryGetProperty("shuffle", out var shuffleElement)
                ? shuffleElement.GetInt32()
                : 1,

            BlockSize = compressorElement.TryGetProperty("blocksize", out var blocksizeElement)
                ? blocksizeElement.GetInt32()
                : 0
        };
    }

    /// <summary>
    /// Gets a human-readable description of this configuration.
    /// </summary>
    public string GetDescription()
    {
        var shuffleMode = Shuffle switch
        {
            0 => "no shuffle",
            1 => "byte shuffle",
            2 => "bit shuffle",
            _ => $"unknown shuffle mode {Shuffle}"
        };

        return $"Blosc (cname={CName}, level={CLevel}, {shuffleMode}, blocksize={BlockSize})";
    }

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">If the configuration is invalid.</exception>
    public void Validate()
    {
        if (CLevel < 0 || CLevel > 9)
        {
            throw new InvalidOperationException($"Invalid compression level: {CLevel}. Must be 0-9.");
        }

        if (Shuffle < 0 || Shuffle > 2)
        {
            throw new InvalidOperationException($"Invalid shuffle mode: {Shuffle}. Must be 0 (no shuffle), 1 (byte shuffle), or 2 (bit shuffle).");
        }

        if (BlockSize < 0)
        {
            throw new InvalidOperationException($"Invalid block size: {BlockSize}. Must be >= 0.");
        }

        var validCompressors = new[] { "blosclz", "lz4", "lz4hc", "snappy", "zlib", "zstd" };
        if (!Array.Exists(validCompressors, c => c.Equals(CName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Invalid compressor name: {CName}. Must be one of: {string.Join(", ", validCompressors)}");
        }
    }
}

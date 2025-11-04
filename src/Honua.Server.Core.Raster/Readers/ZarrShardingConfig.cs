// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Readers;

/// <summary>
/// Zarr v3 sharding configuration.
/// Sharding consolidates multiple chunks into single shard objects for improved storage efficiency.
/// </summary>
/// <remarks>
/// Sharding is a Zarr v3 feature that reduces the number of small objects in cloud storage
/// by combining multiple chunks into larger shard files. Each shard contains an index
/// that maps chunk coordinates to byte offsets within the shard.
/// </remarks>
public sealed record ShardingConfig
{
    /// <summary>
    /// Number of chunks per shard in each dimension.
    /// For example, [2, 2] means a 2x2 grid of chunks per shard.
    /// </summary>
    public required int[] ChunksPerShard { get; init; }

    /// <summary>
    /// Codec used for the shard index (typically "bytes" for raw binary).
    /// </summary>
    public string IndexCodec { get; init; } = "bytes";

    /// <summary>
    /// Location of the index within the shard.
    /// 0 = beginning of shard, 1 = end of shard.
    /// </summary>
    public int IndexLocation { get; init; } = 1; // Default to end (more common)

    /// <summary>
    /// Size of each index entry in bytes.
    /// Standard Zarr v3 uses 16 bytes per entry (8 bytes offset + 8 bytes length).
    /// </summary>
    public int IndexEntrySize { get; init; } = 16;

    /// <summary>
    /// Validates the sharding configuration.
    /// </summary>
    public void Validate()
    {
        if (ChunksPerShard == null || ChunksPerShard.Length == 0)
        {
            throw new ArgumentException("ChunksPerShard must be specified and non-empty", nameof(ChunksPerShard));
        }

        foreach (var chunkCount in ChunksPerShard)
        {
            if (chunkCount <= 0)
            {
                throw new ArgumentException(
                    $"All ChunksPerShard values must be positive. Found: {chunkCount}",
                    nameof(ChunksPerShard));
            }
        }

        if (IndexLocation != 0 && IndexLocation != 1)
        {
            throw new ArgumentException(
                $"IndexLocation must be 0 (start) or 1 (end). Found: {IndexLocation}",
                nameof(IndexLocation));
        }

        if (IndexEntrySize <= 0)
        {
            throw new ArgumentException(
                $"IndexEntrySize must be positive. Found: {IndexEntrySize}",
                nameof(IndexEntrySize));
        }
    }

    /// <summary>
    /// Calculates the total number of chunks in a shard.
    /// </summary>
    public int GetTotalChunksPerShard()
    {
        var total = 1;
        foreach (var count in ChunksPerShard)
        {
            total *= count;
        }
        return total;
    }

    /// <summary>
    /// Calculates the shard coordinates for a given chunk coordinate.
    /// </summary>
    public int[] GetShardCoordinates(int[] chunkCoords)
    {
        if (chunkCoords.Length != ChunksPerShard.Length)
        {
            throw new ArgumentException(
                $"Chunk coordinates dimension ({chunkCoords.Length}) must match " +
                $"sharding dimension ({ChunksPerShard.Length})",
                nameof(chunkCoords));
        }

        var shardCoords = new int[chunkCoords.Length];
        for (int i = 0; i < chunkCoords.Length; i++)
        {
            shardCoords[i] = chunkCoords[i] / ChunksPerShard[i];
        }
        return shardCoords;
    }

    /// <summary>
    /// Calculates the chunk's index within its shard (0-based).
    /// </summary>
    public int GetChunkIndexInShard(int[] chunkCoords)
    {
        if (chunkCoords.Length != ChunksPerShard.Length)
        {
            throw new ArgumentException(
                $"Chunk coordinates dimension ({chunkCoords.Length}) must match " +
                $"sharding dimension ({ChunksPerShard.Length})",
                nameof(chunkCoords));
        }

        // Calculate chunk position within shard (C-order)
        var localCoords = new int[chunkCoords.Length];
        for (int i = 0; i < chunkCoords.Length; i++)
        {
            localCoords[i] = chunkCoords[i] % ChunksPerShard[i];
        }

        // Convert to linear index (C-order)
        var index = 0;
        var stride = 1;
        for (int i = localCoords.Length - 1; i >= 0; i--)
        {
            index += localCoords[i] * stride;
            stride *= ChunksPerShard[i];
        }

        return index;
    }
}

/// <summary>
/// Represents a parsed shard index that maps chunk positions to byte ranges.
/// </summary>
public sealed class ShardIndex
{
    /// <summary>
    /// Array of chunk offsets within the shard. Index corresponds to chunk position in shard.
    /// </summary>
    public required long[] Offsets { get; init; }

    /// <summary>
    /// Array of chunk lengths within the shard. Index corresponds to chunk position in shard.
    /// </summary>
    public required long[] Lengths { get; init; }

    /// <summary>
    /// Gets the byte range for a chunk at the specified index within the shard.
    /// </summary>
    /// <param name="chunkIndexInShard">Index of the chunk within the shard (0-based)</param>
    /// <returns>Tuple of (offset, length) or null if chunk doesn't exist</returns>
    public (long offset, long length)? GetChunkRange(int chunkIndexInShard)
    {
        if (chunkIndexInShard < 0 || chunkIndexInShard >= Offsets.Length)
        {
            return null;
        }

        var offset = Offsets[chunkIndexInShard];
        var length = Lengths[chunkIndexInShard];

        // A length of 0 or offset of -1 indicates a missing chunk (sparse array)
        if (length == 0 || offset < 0)
        {
            return null;
        }

        return (offset, length);
    }
}

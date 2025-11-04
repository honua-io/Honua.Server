// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Readers;

/// <summary>
/// Reads chunks from Zarr v3 sharded arrays.
/// Handles shard index parsing and chunk extraction from consolidated shard files.
/// </summary>
public sealed class ShardedChunkReader
{
    private readonly ILogger<ShardedChunkReader> _logger;

    public ShardedChunkReader(ILogger<ShardedChunkReader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Reads and parses the shard index from a shard stream.
    /// </summary>
    /// <param name="shardStream">Stream containing the complete shard data</param>
    /// <param name="config">Sharding configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed shard index</returns>
    public async Task<ShardIndex> ReadShardIndexAsync(
        Stream shardStream,
        ShardingConfig config,
        CancellationToken cancellationToken = default)
    {
        if (shardStream == null)
        {
            throw new ArgumentNullException(nameof(shardStream));
        }

        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        config.Validate();

        var totalChunks = config.GetTotalChunksPerShard();
        var indexSize = totalChunks * config.IndexEntrySize;

        _logger.LogDebug(
            "Reading shard index: {TotalChunks} chunks, {IndexSize} bytes, location: {Location}",
            totalChunks, indexSize, config.IndexLocation == 0 ? "start" : "end");

        // Read index bytes from stream
        byte[] indexBytes;
        if (config.IndexLocation == 0)
        {
            // Index at start of shard
            shardStream.Seek(0, SeekOrigin.Begin);
            indexBytes = new byte[indexSize];
            await ReadExactlyAsync(shardStream, indexBytes, cancellationToken);
        }
        else
        {
            // Index at end of shard
            if (!shardStream.CanSeek)
            {
                throw new InvalidOperationException(
                    "Shard stream must be seekable when index location is at end");
            }

            var indexStart = shardStream.Length - indexSize;
            if (indexStart < 0)
            {
                throw new InvalidDataException(
                    $"Shard too small to contain index. Shard size: {shardStream.Length}, " +
                    $"expected index size: {indexSize}");
            }

            shardStream.Seek(indexStart, SeekOrigin.Begin);
            indexBytes = new byte[indexSize];
            await ReadExactlyAsync(shardStream, indexBytes, cancellationToken);
        }

        // Parse index entries
        return ParseShardIndex(indexBytes, config);
    }

    /// <summary>
    /// Parses raw shard index bytes into a ShardIndex object.
    /// </summary>
    private ShardIndex ParseShardIndex(byte[] indexBytes, ShardingConfig config)
    {
        var totalChunks = config.GetTotalChunksPerShard();
        var offsets = new long[totalChunks];
        var lengths = new long[totalChunks];

        // Standard Zarr v3 format: each entry is 16 bytes (8 bytes offset + 8 bytes length)
        // Both values are little-endian uint64
        for (int i = 0; i < totalChunks; i++)
        {
            var entryOffset = i * config.IndexEntrySize;

            if (entryOffset + config.IndexEntrySize > indexBytes.Length)
            {
                throw new InvalidDataException(
                    $"Index buffer too small. Expected {totalChunks * config.IndexEntrySize} bytes, " +
                    $"got {indexBytes.Length} bytes");
            }

            // Read offset (8 bytes, little-endian)
            var offset = BinaryPrimitives.ReadInt64LittleEndian(
                indexBytes.AsSpan(entryOffset, 8));

            // Read length (8 bytes, little-endian)
            var length = BinaryPrimitives.ReadInt64LittleEndian(
                indexBytes.AsSpan(entryOffset + 8, 8));

            offsets[i] = offset;
            lengths[i] = length;

            _logger.LogTrace(
                "Chunk {Index} in shard: offset={Offset}, length={Length}",
                i, offset, length);
        }

        return new ShardIndex
        {
            Offsets = offsets,
            Lengths = lengths
        };
    }

    /// <summary>
    /// Extracts a specific chunk from a shard stream.
    /// </summary>
    /// <param name="shardStream">Stream containing the complete shard data</param>
    /// <param name="offset">Byte offset of the chunk within the shard</param>
    /// <param name="length">Length of the chunk in bytes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Chunk data (compressed if applicable)</returns>
    public async Task<byte[]> ExtractChunkFromShardAsync(
        Stream shardStream,
        long offset,
        long length,
        CancellationToken cancellationToken = default)
    {
        if (shardStream == null)
        {
            throw new ArgumentNullException(nameof(shardStream));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset), offset, "Offset must be non-negative");
        }

        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length), length, "Length must be positive");
        }

        if (length > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length), length, $"Chunk too large: {length} bytes exceeds maximum {int.MaxValue}");
        }

        _logger.LogDebug("Extracting chunk from shard: offset={Offset}, length={Length}", offset, length);

        if (!shardStream.CanSeek)
        {
            throw new InvalidOperationException("Shard stream must be seekable for chunk extraction");
        }

        // Seek to chunk location
        shardStream.Seek(offset, SeekOrigin.Begin);

        // Read chunk data
        var chunkData = new byte[(int)length];
        await ReadExactlyAsync(shardStream, chunkData, cancellationToken);

        return chunkData;
    }

    /// <summary>
    /// Calculates the byte offset and length for a chunk within a shard.
    /// </summary>
    /// <param name="chunkCoords">Coordinates of the chunk in the array</param>
    /// <param name="config">Sharding configuration</param>
    /// <param name="index">Parsed shard index</param>
    /// <returns>Tuple of (offset, length) or null if chunk doesn't exist</returns>
    public (long offset, long length)? CalculateChunkOffsetInShard(
        int[] chunkCoords,
        ShardingConfig config,
        ShardIndex index)
    {
        if (chunkCoords == null)
        {
            throw new ArgumentNullException(nameof(chunkCoords));
        }

        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (index == null)
        {
            throw new ArgumentNullException(nameof(index));
        }

        // Get the chunk's index within the shard
        var chunkIndexInShard = config.GetChunkIndexInShard(chunkCoords);

        _logger.LogTrace(
            "Chunk {Coords} maps to index {Index} in shard",
            string.Join(",", chunkCoords), chunkIndexInShard);

        // Look up byte range in index
        return index.GetChunkRange(chunkIndexInShard);
    }

    /// <summary>
    /// Helper to read exactly the requested number of bytes from a stream.
    /// </summary>
    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(
                buffer.AsMemory(totalRead, buffer.Length - totalRead),
                cancellationToken);

            if (bytesRead == 0)
            {
                throw new EndOfStreamException(
                    $"Unexpected end of stream. Expected {buffer.Length} bytes, read {totalRead} bytes");
            }

            totalRead += bytesRead;
        }
    }

    /// <summary>
    /// Synchronous version of ReadShardIndexAsync.
    /// </summary>
    public ShardIndex ReadShardIndex(Stream shardStream, ShardingConfig config)
    {
        if (shardStream == null)
        {
            throw new ArgumentNullException(nameof(shardStream));
        }

        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        config.Validate();

        var totalChunks = config.GetTotalChunksPerShard();
        var indexSize = totalChunks * config.IndexEntrySize;

        _logger.LogDebug(
            "Reading shard index: {TotalChunks} chunks, {IndexSize} bytes, location: {Location}",
            totalChunks, indexSize, config.IndexLocation == 0 ? "start" : "end");

        // Read index bytes from stream
        byte[] indexBytes;
        if (config.IndexLocation == 0)
        {
            // Index at start of shard
            shardStream.Seek(0, SeekOrigin.Begin);
            indexBytes = new byte[indexSize];
            ReadExactly(shardStream, indexBytes);
        }
        else
        {
            // Index at end of shard
            if (!shardStream.CanSeek)
            {
                throw new InvalidOperationException(
                    "Shard stream must be seekable when index location is at end");
            }

            var indexStart = shardStream.Length - indexSize;
            if (indexStart < 0)
            {
                throw new InvalidDataException(
                    $"Shard too small to contain index. Shard size: {shardStream.Length}, " +
                    $"expected index size: {indexSize}");
            }

            shardStream.Seek(indexStart, SeekOrigin.Begin);
            indexBytes = new byte[indexSize];
            ReadExactly(shardStream, indexBytes);
        }

        return ParseShardIndex(indexBytes, config);
    }

    /// <summary>
    /// Synchronous version of ExtractChunkFromShardAsync.
    /// </summary>
    public byte[] ExtractChunkFromShard(Stream shardStream, long offset, long length)
    {
        if (shardStream == null)
        {
            throw new ArgumentNullException(nameof(shardStream));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset), offset, "Offset must be non-negative");
        }

        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length), length, "Length must be positive");
        }

        if (length > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length), length, $"Chunk too large: {length} bytes exceeds maximum {int.MaxValue}");
        }

        _logger.LogDebug("Extracting chunk from shard: offset={Offset}, length={Length}", offset, length);

        if (!shardStream.CanSeek)
        {
            throw new InvalidOperationException("Shard stream must be seekable for chunk extraction");
        }

        // Seek to chunk location
        shardStream.Seek(offset, SeekOrigin.Begin);

        // Read chunk data
        var chunkData = new byte[(int)length];
        ReadExactly(shardStream, chunkData);

        return chunkData;
    }

    /// <summary>
    /// Synchronous helper to read exactly the requested number of bytes from a stream.
    /// </summary>
    private static void ReadExactly(Stream stream, byte[] buffer)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var bytesRead = stream.Read(buffer, totalRead, buffer.Length - totalRead);

            if (bytesRead == 0)
            {
                throw new EndOfStreamException(
                    $"Unexpected end of stream. Expected {buffer.Length} bytes, read {totalRead} bytes");
            }

            totalRead += bytesRead;
        }
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Observability;
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Readers;

/// <summary>
/// Stream implementation for reading Zarr arrays with lazy chunk loading.
/// Provides Stream interface over chunk-based Zarr storage, reading chunks on-demand.
/// Supports spatial windowing to read only requested regions.
/// </summary>
/// <remarks>
/// This stream reads Zarr chunks lazily, without loading the entire array into memory.
/// For 2D/3D raster data, chunks are fetched only when the read position enters their region.
/// Supports both sequential and random access patterns.
/// </remarks>
public sealed class ZarrStream : Stream
{
    private readonly IZarrReader _zarrReader;
    private readonly ZarrArray _zarrArray;
    private readonly ILogger<ZarrStream> _logger;
    private readonly ZarrStreamMetrics? _metrics;
    private readonly int[] _sliceStart;
    private readonly int[] _sliceCount;
    private readonly int _elementSize;
    private readonly long _totalBytes;
    private long _position;
    private byte[]? _currentChunkData;
    private int _currentChunkIndex = -1;
    private bool _disposed;

    // Chunk mapping for spatial data (assuming C-order: time, y, x or y, x)
    private readonly int[][] _chunkCoordinates;
    private readonly long[] _chunkByteOffsets;
    private readonly int[] _chunkByteSizes;
    private readonly int _chunksPerRow;
    private readonly int _totalChunks;

    private static readonly ActivitySource ActivitySource = new("Honua.Raster.ZarrStream");

    /// <summary>
    /// Creates a new ZarrStream for reading a region of a Zarr array.
    /// </summary>
    /// <param name="zarrReader">The Zarr reader to use for fetching chunks</param>
    /// <param name="zarrArray">The opened Zarr array</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="metrics">Optional metrics collector</param>
    /// <param name="sliceStart">Starting indices for the region to read (null = read from origin)</param>
    /// <param name="sliceCount">Count of elements in each dimension (null = read entire array)</param>
    public ZarrStream(
        IZarrReader zarrReader,
        ZarrArray zarrArray,
        ILogger<ZarrStream> logger,
        ZarrStreamMetrics? metrics = null,
        int[]? sliceStart = null,
        int[]? sliceCount = null)
    {
        _zarrReader = zarrReader ?? throw new ArgumentNullException(nameof(zarrReader));
        _zarrArray = zarrArray ?? throw new ArgumentNullException(nameof(zarrArray));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics;

        // Parse dtype to get element size
        _elementSize = GetElementSize(zarrArray.Metadata.DType);

        // Default to reading entire array if not specified
        _sliceStart = sliceStart ?? new int[zarrArray.Metadata.Shape.Length];
        _sliceCount = sliceCount ?? zarrArray.Metadata.Shape.ToArray();

        // Validate slice parameters
        ValidateSliceParameters();

        // Calculate total bytes in the requested slice
        _totalBytes = CalculateTotalBytes();

        // Build chunk mapping (which chunks cover the requested region)
        (_chunkCoordinates, _chunkByteOffsets, _chunkByteSizes, _chunksPerRow, _totalChunks) =
            BuildChunkMapping();

        _logger.LogDebug(
            "ZarrStream created: array={ArrayUri}/{Variable}, slice=start[{Start}] count[{Count}], " +
            "totalBytes={TotalBytes}, totalChunks={TotalChunks}",
            zarrArray.Uri, zarrArray.VariableName,
            string.Join(",", _sliceStart), string.Join(",", _sliceCount),
            _totalBytes, _totalChunks);

        _metrics?.StreamsCreated.Add(1);
    }

    public override bool CanRead => !_disposed;
    public override bool CanSeek => !_disposed;
    public override bool CanWrite => false;
    public override long Length => _totalBytes;

    public override long Position
    {
        get => _position;
        set
        {
            if (value < 0 || value > _totalBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Position out of range");
            }
            _position = value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        // NOTE: Synchronous Read() required by Stream base class.
        // Calls async implementation with blocking. This is safe because:
        // 1. Raster operations run in background threads (not ASP.NET request context)
        // 2. Stream API does not provide async-only option
        // 3. Callers should prefer ReadAsync() when possible
        return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || offset >= buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (_position >= _totalBytes || count == 0)
        {
            return 0;
        }

        return await ActivityScope.ExecuteAsync(
            ActivitySource,
            "ZarrStream.Read",
            [("position", _position), ("count", count)],
            async activity =>
            {
                var totalBytesRead = 0;
                var remainingToRead = Math.Min(count, (int)(_totalBytes - _position));

                while (remainingToRead > 0 && _position < _totalBytes)
                {
                    // Determine which chunk we're currently reading from
                    var chunkIndex = GetChunkIndexForPosition(_position);

                    // Load chunk if needed
                    await EnsureChunkLoadedAsync(chunkIndex, cancellationToken);

                    if (_currentChunkData == null)
                    {
                        // Sparse array - chunk doesn't exist, fill with zeros
                        var zeroBytes = Math.Min(remainingToRead, _chunkByteSizes[chunkIndex]);
                        Array.Clear(buffer, offset + totalBytesRead, zeroBytes);
                        totalBytesRead += zeroBytes;
                        remainingToRead -= zeroBytes;
                        _position += zeroBytes;
                        continue;
                    }

                    // Calculate offset within current chunk
                    var chunkStartPosition = _chunkByteOffsets[chunkIndex];
                    var offsetInChunk = (int)(_position - chunkStartPosition);
                    var availableInChunk = _chunkByteSizes[chunkIndex] - offsetInChunk;
                    var bytesToCopy = Math.Min(remainingToRead, availableInChunk);

                    // Copy data from chunk to output buffer
                    Array.Copy(_currentChunkData, offsetInChunk, buffer, offset + totalBytesRead, bytesToCopy);

                    totalBytesRead += bytesToCopy;
                    remainingToRead -= bytesToCopy;
                    _position += bytesToCopy;
                }

                _metrics?.BytesRead.Add(totalBytesRead);
                _logger.LogTrace("ZarrStream read {BytesRead} bytes at position {Position}", totalBytesRead, _position - totalBytesRead);

                return totalBytesRead;
            });
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _totalBytes + offset,
            _ => throw new ArgumentException("Invalid seek origin", nameof(origin))
        };

        if (newPosition < 0 || newPosition > _totalBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Seek position out of range");
        }

        _position = newPosition;
        return _position;
    }

    public override void Flush()
    {
        // No-op for read-only stream
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException("Cannot set length on read-only Zarr stream");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("Cannot write to read-only Zarr stream");
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _currentChunkData = null;
                _metrics?.StreamsDisposed.Add(1);
                _logger.LogDebug("ZarrStream disposed: {ArrayUri}/{Variable}",
                    _zarrArray.Uri, _zarrArray.VariableName);
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }

    private async Task EnsureChunkLoadedAsync(int chunkIndex, CancellationToken cancellationToken)
    {
        if (_currentChunkIndex == chunkIndex && _currentChunkData != null)
        {
            return; // Already loaded
        }

        var chunkCoords = _chunkCoordinates[chunkIndex];

        await ActivityScope.ExecuteAsync(
            ActivitySource,
            "ZarrStream.LoadChunk",
            [("chunkIndex", chunkIndex), ("chunkCoords", string.Join(",", chunkCoords))],
            async activity =>
            {
                using (var measurement = PerformanceMeasurement.Measure(
                    "ZarrStream.LoadChunk",
                    duration => _metrics?.ChunkLoadTimeMs.Record((long)duration.TotalMilliseconds)))
                {
                    try
                    {
                        _currentChunkData = await _zarrReader.ReadChunkAsync(_zarrArray, chunkCoords, cancellationToken);
                        _currentChunkIndex = chunkIndex;

                        _metrics?.ChunksLoaded.Add(1);
                        _logger.LogTrace(
                            "Loaded Zarr chunk {ChunkIndex} coords=[{ChunkCoords}] size={Size} bytes in {Duration}ms",
                            chunkIndex, string.Join(",", chunkCoords), _currentChunkData?.Length ?? 0, measurement.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Failed to load Zarr chunk {ChunkIndex} coords=[{ChunkCoords}], treating as sparse",
                            chunkIndex, string.Join(",", chunkCoords));

                        // Treat as sparse chunk (all zeros)
                        _currentChunkData = null;
                        _currentChunkIndex = chunkIndex;
                        _metrics?.ChunkErrors.Add(1);
                    }
                }
            });
    }

    private int GetChunkIndexForPosition(long position)
    {
        // Binary search through chunk offsets to find which chunk contains this position
        var index = Array.BinarySearch(_chunkByteOffsets, position);

        if (index >= 0)
        {
            // Exact match - position is at chunk boundary
            return index;
        }
        else
        {
            // Not exact - BinarySearch returns bitwise complement of next larger element
            // We want the chunk that contains this position
            var nextIndex = ~index;
            return Math.Max(0, nextIndex - 1);
        }
    }

    private void ValidateSliceParameters()
    {
        var ndim = _zarrArray.Metadata.Shape.Length;

        if (_sliceStart.Length != ndim)
        {
            throw new ArgumentException($"Slice start must have {ndim} dimensions, got {_sliceStart.Length}");
        }

        if (_sliceCount.Length != ndim)
        {
            throw new ArgumentException($"Slice count must have {ndim} dimensions, got {_sliceCount.Length}");
        }

        for (int i = 0; i < ndim; i++)
        {
            if (_sliceStart[i] < 0 || _sliceStart[i] >= _zarrArray.Metadata.Shape[i])
            {
                throw new ArgumentOutOfRangeException(
                    $"Slice start[{i}]={_sliceStart[i]} out of range [0, {_zarrArray.Metadata.Shape[i]})");
            }

            if (_sliceCount[i] <= 0 || _sliceStart[i] + _sliceCount[i] > _zarrArray.Metadata.Shape[i])
            {
                throw new ArgumentOutOfRangeException(
                    $"Slice count[{i}]={_sliceCount[i]} out of range with start={_sliceStart[i]}, shape={_zarrArray.Metadata.Shape[i]}");
            }
        }
    }

    private long CalculateTotalBytes()
    {
        long totalElements = 1;
        foreach (var count in _sliceCount)
        {
            totalElements *= count;
        }
        return totalElements * _elementSize;
    }

    private (int[][] chunkCoords, long[] byteOffsets, int[] byteSizes, int chunksPerRow, int totalChunks) BuildChunkMapping()
    {
        var ndim = _zarrArray.Metadata.Shape.Length;
        var chunks = _zarrArray.Metadata.Chunks;

        // Calculate which chunks overlap with the requested slice
        var startChunk = new int[ndim];
        var endChunk = new int[ndim];

        for (int i = 0; i < ndim; i++)
        {
            startChunk[i] = _sliceStart[i] / chunks[i];
            endChunk[i] = (_sliceStart[i] + _sliceCount[i] - 1) / chunks[i];
        }

        // Generate all chunk coordinates in the range
        var chunkCoordsList = new System.Collections.Generic.List<int[]>();
        GenerateChunkCoordinates(startChunk, endChunk, 0, new int[ndim], chunkCoordsList);

        // Calculate byte offsets and sizes for each chunk in the slice
        var byteOffsets = new long[chunkCoordsList.Count];
        var byteSizes = new int[chunkCoordsList.Count];
        long currentOffset = 0;

        // For 2D/3D data, calculate chunks per row for efficient indexing
        var chunksPerRow = ndim >= 2 ? (endChunk[ndim - 1] - startChunk[ndim - 1] + 1) : 1;

        for (int i = 0; i < chunkCoordsList.Count; i++)
        {
            var chunkCoord = chunkCoordsList[i];

            // Calculate the overlap between this chunk and the requested slice
            var overlapCount = new int[ndim];
            for (int d = 0; d < ndim; d++)
            {
                var chunkStart = chunkCoord[d] * chunks[d];
                var chunkEnd = Math.Min(chunkStart + chunks[d], _zarrArray.Metadata.Shape[d]);
                var sliceEnd = _sliceStart[d] + _sliceCount[d];

                var overlapStart = Math.Max(_sliceStart[d], chunkStart);
                var overlapEnd = Math.Min(sliceEnd, chunkEnd);
                overlapCount[d] = Math.Max(0, overlapEnd - overlapStart);
            }

            // Calculate bytes in this chunk's overlap region
            var chunkElements = overlapCount.Aggregate(1, (a, b) => a * b);
            var chunkBytes = chunkElements * _elementSize;

            byteOffsets[i] = currentOffset;
            byteSizes[i] = chunkBytes;
            currentOffset += chunkBytes;
        }

        return (chunkCoordsList.ToArray(), byteOffsets, byteSizes, chunksPerRow, chunkCoordsList.Count);
    }

    private void GenerateChunkCoordinates(
        int[] start,
        int[] end,
        int dim,
        int[] current,
        System.Collections.Generic.List<int[]> result)
    {
        if (dim == start.Length)
        {
            result.Add((int[])current.Clone());
            return;
        }

        for (int i = start[dim]; i <= end[dim]; i++)
        {
            current[dim] = i;
            GenerateChunkCoordinates(start, end, dim + 1, current, result);
        }
    }

    private int GetElementSize(string dtype)
    {
        // Parse Zarr dtype to get element size
        // Remove endianness prefix if present
        var typeChar = dtype;
        if (dtype.Length > 1 && (dtype[0] == '<' || dtype[0] == '>' || dtype[0] == '|'))
        {
            typeChar = dtype.Substring(1);
        }

        if (typeChar.Contains("f4") || typeChar.Contains("float32") || typeChar.Contains("i4") || typeChar.Contains("int32") || typeChar.Contains("u4") || typeChar.Contains("uint32"))
            return 4;
        if (typeChar.Contains("f8") || typeChar.Contains("float64") || typeChar.Contains("i8") || typeChar.Contains("int64") || typeChar.Contains("u8") || typeChar.Contains("uint64"))
            return 8;
        if (typeChar.Contains("i2") || typeChar.Contains("int16") || typeChar.Contains("u2") || typeChar.Contains("uint16"))
            return 2;
        if (typeChar.Contains("i1") || typeChar.Contains("int8") || typeChar.Contains("u1") || typeChar.Contains("uint8"))
            return 1;

        _logger.LogWarning("Unknown dtype {DType}, assuming 4 bytes", dtype);
        return 4;
    }

    /// <summary>
    /// Create a ZarrStream for reading an entire Zarr array.
    /// </summary>
    public static async Task<ZarrStream> CreateAsync(
        IZarrReader zarrReader,
        string uri,
        string variableName,
        ILogger<ZarrStream> logger,
        ZarrStreamMetrics? metrics = null,
        CancellationToken cancellationToken = default)
    {
        var array = await zarrReader.OpenArrayAsync(uri, variableName, cancellationToken);
        try
        {
            return new ZarrStream(zarrReader, array, logger, metrics);
        }
        catch
        {
            // Dispose array if ZarrStream construction fails
            if (array is IDisposable disposable)
            {
                disposable.Dispose();
            }
            throw;
        }
    }

    /// <summary>
    /// Create a ZarrStream for reading a specific region (spatial windowing).
    /// </summary>
    public static async Task<ZarrStream> CreateWithWindowAsync(
        IZarrReader zarrReader,
        string uri,
        string variableName,
        int[] sliceStart,
        int[] sliceCount,
        ILogger<ZarrStream> logger,
        ZarrStreamMetrics? metrics = null,
        CancellationToken cancellationToken = default)
    {
        var array = await zarrReader.OpenArrayAsync(uri, variableName, cancellationToken);
        try
        {
            return new ZarrStream(zarrReader, array, logger, metrics, sliceStart, sliceCount);
        }
        catch
        {
            // Dispose array if ZarrStream construction fails
            if (array is IDisposable disposable)
            {
                disposable.Dispose();
            }
            throw;
        }
    }
}

/// <summary>
/// Metrics collector for ZarrStream operations.
/// Uses OpenTelemetry Metrics API for observability.
/// </summary>
public sealed class ZarrStreamMetrics
{
    private static readonly Meter Meter = new("Honua.Raster.ZarrStream", "1.0.0");

    public Counter<long> StreamsCreated { get; }
    public Counter<long> StreamsDisposed { get; }
    public Counter<long> BytesRead { get; }
    public Counter<long> ChunksLoaded { get; }
    public Counter<long> ChunkErrors { get; }
    public Histogram<long> ChunkLoadTimeMs { get; }

    public ZarrStreamMetrics()
    {
        StreamsCreated = Meter.CreateCounter<long>(
            "zarr_stream_created",
            "streams",
            "Number of ZarrStream instances created");

        StreamsDisposed = Meter.CreateCounter<long>(
            "zarr_stream_disposed",
            "streams",
            "Number of ZarrStream instances disposed");

        BytesRead = Meter.CreateCounter<long>(
            "zarr_stream_bytes_read",
            "bytes",
            "Total bytes read from Zarr streams");

        ChunksLoaded = Meter.CreateCounter<long>(
            "zarr_stream_chunks_loaded",
            "chunks",
            "Number of Zarr chunks loaded");

        ChunkErrors = Meter.CreateCounter<long>(
            "zarr_stream_chunk_errors",
            "chunks",
            "Number of Zarr chunk load errors");

        ChunkLoadTimeMs = Meter.CreateHistogram<long>(
            "zarr_stream_chunk_load_time_ms",
            "milliseconds",
            "Time to load a Zarr chunk");
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Raster.Cache;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Readers;

/// <summary>
/// HTTP-based Zarr reader for remote Zarr stores (S3, Azure, GCS, HTTP).
/// Supports Zarr v2 format with HTTP range requests.
/// Handles endianness conversion for cross-platform compatibility.
/// </summary>
/// <remarks>
/// Supported dtype formats:
/// - Little-endian: &lt;f4 (float32), &lt;f8 (float64), &lt;i2 (int16), &lt;i4 (int32), &lt;i8 (int64)
/// - Big-endian: &gt;f4 (float32), &gt;f8 (float64), &gt;i2 (int16), &gt;i4 (int32), &gt;i8 (int64)
/// - Native/Not applicable: |u1 (uint8), |i1 (int8)
/// - Legacy formats without prefix: f4, f8, i4, etc.
/// </remarks>
public sealed class HttpZarrReader : IZarrReader
{
    private readonly ILogger<HttpZarrReader> _logger;
    private readonly HttpClient _httpClient;
    private readonly ZarrDecompressor _decompressor;
    private readonly ZarrChunkCache? _chunkCache;
    private readonly RasterMemoryLimits _memoryLimits;
    private readonly ResiliencePipeline<byte[]> _resiliencePipeline;

    public HttpZarrReader(
        ILogger<HttpZarrReader> logger,
        HttpClient httpClient,
        ZarrDecompressor decompressor,
        RasterMemoryLimits? memoryLimits = null,
        ZarrChunkCache? chunkCache = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClient.Timeout = TimeSpan.FromSeconds(60); // Raster operations
        _decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
        _memoryLimits = memoryLimits ?? new RasterMemoryLimits();
        _memoryLimits.Validate();
        _chunkCache = chunkCache;

        // Build resilience pipeline with circuit breaker, retry, and timeout
        _resiliencePipeline = new ResiliencePipelineBuilder<byte[]>()
            .AddRetry(new RetryStrategyOptions<byte[]>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(100),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder<byte[]>()
                    .Handle<HttpRequestException>(ex =>
                        ex.StatusCode == null ||  // Network error
                        (int)ex.StatusCode >= 500 || // Server error
                        ex.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                    .Handle<TaskCanceledException>()
                    .Handle<TimeoutRejectedException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Zarr chunk read failed (attempt {Attempt} of {MaxAttempts}), retrying after {Delay}ms: {Exception}",
                        args.AttemptNumber + 1, 3, args.RetryDelay.TotalMilliseconds, args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<byte[]>
            {
                FailureRatio = 0.5,  // Open circuit if 50% failures
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 10,
                BreakDuration = TimeSpan.FromSeconds(30),
                OnOpened = args =>
                {
                    _logger.LogError(
                        "Circuit breaker OPENED for Zarr storage. " +
                        "Remote storage is unavailable. Breaking for 30 seconds.");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Circuit breaker CLOSED for Zarr storage. Service recovered.");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation("Circuit breaker HALF-OPEN for Zarr storage. Testing if service recovered.");
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();
    }

    public async Task<ZarrArray> OpenArrayAsync(string uri, string variableName, CancellationToken cancellationToken = default)
    {
        var metadata = await GetMetadataAsync(uri, variableName, cancellationToken);

        return new ZarrArray
        {
            Uri = uri,
            VariableName = variableName,
            Metadata = metadata
        };
    }

    public async Task<byte[]> ReadChunkAsync(ZarrArray array, int[] chunkCoords, CancellationToken cancellationToken = default)
    {
        // Use cache if available
        if (_chunkCache != null)
        {
            return await _chunkCache.GetOrFetchAsync(
                array.Uri,
                array.VariableName,
                chunkCoords,
                () => FetchAndDecompressChunkAsync(array, chunkCoords, cancellationToken),
                cancellationToken);
        }

        // No cache - fetch directly
        return await FetchAndDecompressChunkAsync(array, chunkCoords, cancellationToken);
    }

    private async Task<byte[]> FetchAndDecompressChunkAsync(
        ZarrArray array,
        int[] chunkCoords,
        CancellationToken cancellationToken)
    {
        var chunkPath = string.Join(".", chunkCoords);
        var chunkUri = BuildChunkUri(array.Uri, array.VariableName, chunkPath);

        try
        {
            // Execute with circuit breaker, retry, and timeout
            return await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                _logger.LogDebug("Fetching Zarr chunk: {ChunkUri}", chunkUri);

                var response = await _httpClient.GetAsync(chunkUri, ct).ConfigureAwait(false);

                // Handle 404 errors - sparse arrays may have missing chunks
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogDebug("Zarr chunk not found (sparse array): {ChunkUri}", chunkUri);
                    // Return empty chunk for sparse arrays
                    var (elementSize, _) = ParseDtype(array.Metadata.DType);
                    var chunkSize = array.Metadata.Chunks.Aggregate(1, (a, b) => a * b) * elementSize;
                    // Use ArrayPool for large chunks to avoid LOH allocations
                    var usePool = chunkSize > 85000;
                    if (usePool)
                    {
                        var poolBuffer = ArrayPool<byte>.Shared.Rent(chunkSize);
                        var result = new byte[chunkSize];
                        Array.Clear(poolBuffer, 0, chunkSize);
                        Array.Copy(poolBuffer, result, chunkSize);
                        ArrayPool<byte>.Shared.Return(poolBuffer);
                        return result;
                    }
                    return new byte[chunkSize];
                }

                response.EnsureSuccessStatusCode();

                var compressedData = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

                _logger.LogDebug("Decompressing {Bytes} bytes for chunk {ChunkPath}",
                    compressedData.Length, chunkPath);

                // Decompress if needed
                var decompressedData = DecompressChunk(compressedData, array.Metadata.Compressor);

                // Apply byte-order conversion if needed
                return ConvertByteOrder(decompressedData, array.Metadata.DType);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex,
                "Circuit breaker is OPEN for Zarr storage {Uri}. " +
                "Remote storage is temporarily unavailable.",
                array.Uri);

            throw new InvalidOperationException(
                $"Zarr storage temporarily unavailable: {array.Uri}. " +
                "The service will retry automatically when storage recovers.", ex);
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex, "Timeout reading Zarr chunk {ChunkUri} after 30 seconds", chunkUri);

            throw new InvalidOperationException(
                $"Zarr chunk read timeout after 30 seconds: {chunkUri}", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HTTP error fetching Zarr chunk {ChunkUri}: {StatusCode}",
                chunkUri, ex.StatusCode);

            throw new InvalidOperationException(
                $"Failed to fetch Zarr chunk: {ex.Message}", ex);
        }
    }

    public async Task<Array> ReadSliceAsync(
        ZarrArray array,
        int[] start,
        int[] count,
        CancellationToken cancellationToken = default)
    {
        // Validate slice size against memory limits BEFORE loading any data
        var (elementSize, _) = ParseDtype(array.Metadata.DType);
        long totalElements = count.Aggregate(1L, (a, b) => a * b);
        long totalBytes = totalElements * elementSize;

        if (totalBytes > _memoryLimits.MaxSliceSizeBytes)
        {
            throw new InvalidOperationException(
                $"Requested slice size ({totalBytes:N0} bytes) exceeds maximum allowed " +
                $"({_memoryLimits.MaxSliceSizeBytes:N0} bytes). " +
                $"Slice dimensions: [{string.Join(", ", count)}], element size: {elementSize} bytes. " +
                $"Consider reducing the slice size or increasing RasterMemoryLimits.MaxSliceSizeBytes.");
        }

        // Determine which chunks are needed
        var chunks = array.Metadata.Chunks;
        var chunkRanges = CalculateChunkRanges(start, count, chunks);

        // Validate chunk count against memory limits
        if (chunkRanges.Length > _memoryLimits.MaxChunksPerRequest)
        {
            throw new InvalidOperationException(
                $"Requested slice requires loading {chunkRanges.Length} chunks, which exceeds maximum allowed " +
                $"({_memoryLimits.MaxChunksPerRequest} chunks). " +
                $"Slice range: start=[{string.Join(", ", start)}], count=[{string.Join(", ", count)}], " +
                $"chunk size=[{string.Join(", ", chunks)}]. " +
                $"Consider reducing the slice size, using larger chunk sizes, or increasing RasterMemoryLimits.MaxChunksPerRequest.");
        }

        _logger.LogDebug(
            "Reading Zarr slice: {TotalBytes:N0} bytes across {ChunkCount} chunks",
            totalBytes, chunkRanges.Length);

        // Read all necessary chunks
        var chunkData = new System.Collections.Generic.List<(int[] coords, byte[] data)>(chunkRanges.Length);

        foreach (var chunkCoord in chunkRanges)
        {
            try
            {
                var data = await ReadChunkAsync(array, chunkCoord, cancellationToken);
                chunkData.Add((chunkCoord, data));
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Sparse array - chunk doesn't exist (expected)
                _logger.LogDebug("Chunk {ChunkCoord} not found, assuming sparse array", string.Join(",", chunkCoord));
                // Continue to next chunk - sparse arrays may have missing chunks
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                                                   ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogError(ex, "Access denied reading Zarr chunk {ChunkCoord}", string.Join(",", chunkCoord));
                throw new UnauthorizedAccessException($"Access denied to Zarr chunk: {string.Join(",", chunkCoord)}", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Timeout reading Zarr chunk {ChunkCoord}", string.Join(",", chunkCoord));
                throw new TimeoutException($"Timeout reading Zarr chunk: {string.Join(",", chunkCoord)}", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error reading Zarr chunk {ChunkCoord}: {StatusCode}",
                    string.Join(",", chunkCoord), ex.StatusCode);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error reading Zarr chunk {ChunkCoord}", string.Join(",", chunkCoord));
                throw new InvalidOperationException($"Failed to read Zarr chunk: {string.Join(",", chunkCoord)}", ex);
            }
        }

        // Assemble chunks into requested slice
        return AssembleSlice(chunkData, array.Metadata, start, count);
    }

    public async Task<ZarrArrayMetadata> GetMetadataAsync(string uri, string variableName, CancellationToken cancellationToken = default)
    {
        // Read .zarray metadata file
        var metadataUri = BuildMetadataUri(uri, variableName);

        _logger.LogDebug("Reading Zarr metadata: {MetadataUri}", metadataUri);

        var response = await _httpClient.GetAsync(metadataUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Parse .zarray JSON
        var shape = root.GetProperty("shape").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        var chunks = root.GetProperty("chunks").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        var dtype = root.GetProperty("dtype").GetString() ?? "float32";
        var compressor = root.TryGetProperty("compressor", out var comp) && comp.ValueKind != JsonValueKind.Null
            ? comp.GetProperty("id").GetString() ?? "null"
            : "null";
        var zarrFormat = root.GetProperty("zarr_format").GetInt32();
        var order = root.GetProperty("order").GetString() ?? "C";
        object? fillValue = null;
        if (root.TryGetProperty("fill_value", out var fv) && fv.ValueKind != JsonValueKind.Null)
        {
            fillValue = fv.ValueKind switch
            {
                JsonValueKind.Number when fv.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number => fv.GetDouble(),
                JsonValueKind.String => fv.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        return new ZarrArrayMetadata
        {
            Shape = shape,
            Chunks = chunks,
            DType = dtype,
            Compressor = compressor,
            ZarrFormat = zarrFormat,
            Order = order,
            FillValue = fillValue
        };
    }

    private string BuildChunkUri(string baseUri, string variableName, string chunkPath)
    {
        // Zarr store structure: {baseUri}/{variableName}/{chunkPath}
        return $"{baseUri.TrimEnd('/')}/{variableName}/{chunkPath}";
    }

    private string BuildMetadataUri(string baseUri, string variableName)
    {
        return $"{baseUri.TrimEnd('/')}/{variableName}/.zarray";
    }

    private byte[] DecompressChunk(byte[] compressedData, string compressor)
    {
        try
        {
            return _decompressor.Decompress(compressedData, compressor);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Unsupported compression codec '{Compressor}'. Support: {SupportLevel}",
                compressor, _decompressor.GetSupportLevel(compressor));
            throw;
        }
    }

    private int[][] CalculateChunkRanges(int[] start, int[] count, int[] chunks)
    {
        var ndim = start.Length;
        var ranges = new System.Collections.Generic.List<int[]>();

        // Validate chunk sizes to prevent division by zero
        for (int i = 0; i < chunks.Length; i++)
        {
            if (chunks[i] <= 0)
            {
                throw new ArgumentException($"Invalid chunk size at dimension {i}: {chunks[i]}. Chunk sizes must be greater than zero.", nameof(chunks));
            }
        }

        // Calculate chunk coordinates that overlap with requested region
        var startChunk = new int[ndim];
        var endChunk = new int[ndim];

        for (int i = 0; i < ndim; i++)
        {
            startChunk[i] = start[i] / chunks[i];
            endChunk[i] = (start[i] + count[i] - 1) / chunks[i];
        }

        // Generate all chunk coordinates in range
        GenerateChunkCoordinates(startChunk, endChunk, 0, new int[ndim], ranges);

        return ranges.ToArray();
    }

    private void GenerateChunkCoordinates(int[] start, int[] end, int dim, int[] current, System.Collections.Generic.List<int[]> result)
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

    private Array AssembleSlice(
        System.Collections.Generic.List<(int[] coords, byte[] data)> chunkData,
        ZarrArrayMetadata metadata,
        int[] start,
        int[] count)
    {
        // Create output array based on dtype
        var (elementSize, _) = ParseDtype(metadata.DType);

        // Use checked arithmetic to detect overflow for large arrays
        long totalElements;
        try
        {
            totalElements = checked(count.Aggregate(1L, (a, b) => a * b));
            var totalBytes = checked(totalElements * elementSize);

            if (totalBytes > int.MaxValue)
            {
                throw new OverflowException($"Requested slice is too large: {totalElements} elements ({totalBytes} bytes exceeds maximum array size)");
            }

            // Double-check against memory limits (should have been validated earlier, but defensive)
            if (totalBytes > _memoryLimits.MaxSliceSizeBytes)
            {
                throw new InvalidOperationException(
                    $"Slice assembly would exceed memory limits: {totalBytes:N0} bytes > {_memoryLimits.MaxSliceSizeBytes:N0} bytes");
            }
        }
        catch (OverflowException ex)
        {
            throw new ArgumentException($"Array dimensions are too large and cause overflow. Dimensions: [{string.Join(", ", count)}]", nameof(count), ex);
        }

        // Allocate result buffer - use standard allocation as we're returning to caller
        // who owns the lifetime. ArrayPool would require caller to manage rental/return.
        var resultSize = (int)(totalElements * elementSize);

        // Use ArrayPool for large results to avoid LOH allocations
        var usePool = resultSize > 85000;
        var result = usePool ? ArrayPool<byte>.Shared.Rent(resultSize) : new byte[resultSize];

        try
        {
            if (chunkData.Count == 0)
            {
                // No chunks found (sparse array or out of bounds)
                // Return only the used portion if using pool
                if (usePool)
                {
                    var emptyResult = new byte[resultSize];
                    return emptyResult;
                }
                return result;
            }

            var ndim = start.Length;
            var chunks = metadata.Chunks;
            var isFortranOrder = metadata.Order == "F";

            // Assemble chunks into result array
            foreach (var (chunkCoord, chunkBytes) in chunkData)
            {
                // Calculate chunk start position in global coordinates
                var chunkStart = new int[ndim];
                for (int i = 0; i < ndim; i++)
                {
                    chunkStart[i] = chunkCoord[i] * chunks[i];
                }

                // Calculate overlap between chunk and requested slice
                var overlapStart = new int[ndim];
                var overlapCount = new int[ndim];
                var chunkOffset = new int[ndim];
                var resultOffset = new int[ndim];

                bool hasOverlap = true;
                for (int i = 0; i < ndim; i++)
                {
                    // Start of overlap in global coordinates
                    overlapStart[i] = Math.Max(start[i], chunkStart[i]);

                    // End of overlap in global coordinates
                    var requestEnd = start[i] + count[i];
                    var chunkEnd = chunkStart[i] + chunks[i];
                    var overlapEnd = Math.Min(requestEnd, chunkEnd);

                    if (overlapStart[i] >= overlapEnd)
                    {
                        hasOverlap = false;
                        break;
                    }

                    overlapCount[i] = overlapEnd - overlapStart[i];
                    chunkOffset[i] = overlapStart[i] - chunkStart[i];
                    resultOffset[i] = overlapStart[i] - start[i];
                }

                if (!hasOverlap)
                {
                    continue;
                }

                // Copy overlapping region from chunk to result
                CopyChunkRegion(
                    chunkBytes, chunks, chunkOffset, overlapCount,
                    result, count, resultOffset,
                    elementSize, isFortranOrder);
            }

            // Return only the used portion if using pool
            if (usePool)
            {
                var finalResult = new byte[resultSize];
                Array.Copy(result, 0, finalResult, 0, resultSize);
                return finalResult;
            }

            return result;
        }
        finally
        {
            if (usePool)
            {
                ArrayPool<byte>.Shared.Return(result);
            }
        }
    }

    private void CopyChunkRegion(
        byte[] sourceChunk, int[] chunkShape, int[] sourceOffset, int[] copyCount,
        byte[] destArray, int[] destShape, int[] destOffset,
        int elementSize, bool isFortranOrder)
    {
        // For simplicity, implement C-order (row-major) copying
        // Multi-dimensional indexing with nested loops based on dimensionality
        var ndim = chunkShape.Length;

        if (ndim == 2)
        {
            // Optimized 2D case
            for (int i = 0; i < copyCount[0]; i++)
            {
                for (int j = 0; j < copyCount[1]; j++)
                {
                    var srcIdx = ((sourceOffset[0] + i) * chunkShape[1] + (sourceOffset[1] + j)) * elementSize;
                    var dstIdx = ((destOffset[0] + i) * destShape[1] + (destOffset[1] + j)) * elementSize;

                    if (srcIdx + elementSize <= sourceChunk.Length && dstIdx + elementSize <= destArray.Length)
                    {
                        Buffer.BlockCopy(sourceChunk, srcIdx, destArray, dstIdx, elementSize);
                    }
                }
            }
        }
        else if (ndim == 3)
        {
            // Optimized 3D case (common for time-series: time, lat, lon)
            for (int i = 0; i < copyCount[0]; i++)
            {
                for (int j = 0; j < copyCount[1]; j++)
                {
                    for (int k = 0; k < copyCount[2]; k++)
                    {
                        var srcIdx = (((sourceOffset[0] + i) * chunkShape[1] + (sourceOffset[1] + j)) * chunkShape[2] + (sourceOffset[2] + k)) * elementSize;
                        var dstIdx = (((destOffset[0] + i) * destShape[1] + (destOffset[1] + j)) * destShape[2] + (destOffset[2] + k)) * elementSize;

                        if (srcIdx + elementSize <= sourceChunk.Length && dstIdx + elementSize <= destArray.Length)
                        {
                            Buffer.BlockCopy(sourceChunk, srcIdx, destArray, dstIdx, elementSize);
                        }
                    }
                }
            }
        }
        else
        {
            // Generic N-dimensional case (slower but works for any dimension)
            CopyNDimensional(sourceChunk, chunkShape, sourceOffset, copyCount, destArray, destShape, destOffset, elementSize, ndim);
        }
    }

    private void CopyNDimensional(
        byte[] sourceChunk, int[] chunkShape, int[] sourceOffset, int[] copyCount,
        byte[] destArray, int[] destShape, int[] destOffset,
        int elementSize, int ndim)
    {
        // Use checked arithmetic to detect overflow for large copy operations
        long totalElements;
        try
        {
            totalElements = checked(copyCount.Aggregate(1L, (a, b) => a * b));
            if (totalElements > int.MaxValue)
            {
                throw new OverflowException($"Copy region is too large: {totalElements} elements exceeds maximum");
            }
        }
        catch (OverflowException ex)
        {
            throw new ArgumentException($"Copy dimensions are too large and cause overflow. Dimensions: [{string.Join(", ", copyCount)}]", nameof(copyCount), ex);
        }

        var indices = new int[ndim];

        for (int linearIdx = 0; linearIdx < (int)totalElements; linearIdx++)
        {
            // Convert linear index to multi-dimensional indices
            var temp = linearIdx;
            for (int d = ndim - 1; d >= 0; d--)
            {
                indices[d] = temp % copyCount[d];
                temp /= copyCount[d];
            }

            // Calculate source index
            int srcFlatIdx = 0;
            int srcStride = 1;
            for (int d = ndim - 1; d >= 0; d--)
            {
                srcFlatIdx += (sourceOffset[d] + indices[d]) * srcStride;
                srcStride *= chunkShape[d];
            }

            // Calculate destination index
            int dstFlatIdx = 0;
            int dstStride = 1;
            for (int d = ndim - 1; d >= 0; d--)
            {
                dstFlatIdx += (destOffset[d] + indices[d]) * dstStride;
                dstStride *= destShape[d];
            }

            var srcIdx = srcFlatIdx * elementSize;
            var dstIdx = dstFlatIdx * elementSize;

            if (srcIdx + elementSize <= sourceChunk.Length && dstIdx + elementSize <= destArray.Length)
            {
                Buffer.BlockCopy(sourceChunk, srcIdx, destArray, dstIdx, elementSize);
            }
        }
    }

    /// <summary>
    /// Parses a Zarr dtype string and extracts element size and endianness.
    /// </summary>
    /// <param name="dtype">Dtype string (e.g., "&lt;f4", "&gt;f8", "f4", "|u1")</param>
    /// <returns>Tuple of (element size in bytes, endianness)</returns>
    /// <exception cref="ArgumentException">Thrown when dtype format is invalid</exception>
    /// <remarks>
    /// Supports standard Zarr dtype formats:
    /// - Endianness markers: &lt; (little), &gt; (big), | (not applicable)
    /// - Type codes: f4/f8 (float), i1/i2/i4/i8 (int), u1/u2/u4/u8 (uint)
    /// - Legacy formats without endianness prefix (assumed little-endian)
    /// </remarks>
    private (int elementSize, Endianness endianness) ParseDtype(string dtype)
    {
        if (string.IsNullOrWhiteSpace(dtype))
        {
            throw new ArgumentException("Dtype string cannot be null or empty", nameof(dtype));
        }

        var endianness = Endianness.LittleEndian; // default for legacy formats
        var typeChar = dtype;

        // Parse endianness prefix if present
        if (dtype.Length > 1)
        {
            var prefix = dtype[0];
            if (prefix == '<')
            {
                endianness = Endianness.LittleEndian;
                typeChar = dtype.Substring(1);
            }
            else if (prefix == '>')
            {
                endianness = Endianness.BigEndian;
                typeChar = dtype.Substring(1);
            }
            else if (prefix == '|')
            {
                endianness = Endianness.NotApplicable;
                typeChar = dtype.Substring(1);
            }
        }

        // Parse element size from type character
        var elementSize = GetElementSizeFromTypeChar(typeChar);
        return (elementSize, endianness);
    }

    /// <summary>
    /// Gets the element size in bytes from a type character string.
    /// </summary>
    private int GetElementSizeFromTypeChar(string typeChar)
    {
        // Parse numpy dtype string (without endianness prefix)
        if (typeChar.Contains("float32") || typeChar.Contains("f4"))
        {
            return 4;
        }
        if (typeChar.Contains("float64") || typeChar.Contains("f8"))
        {
            return 8;
        }
        if (typeChar.Contains("int64") || typeChar.Contains("i8"))
        {
            return 8;
        }
        if (typeChar.Contains("int32") || typeChar.Contains("i4"))
        {
            return 4;
        }
        if (typeChar.Contains("int16") || typeChar.Contains("i2"))
        {
            return 2;
        }
        if (typeChar.Contains("int8") || typeChar.Contains("i1"))
        {
            return 1;
        }
        if (typeChar.Contains("uint64") || typeChar.Contains("u8"))
        {
            return 8;
        }
        if (typeChar.Contains("uint32") || typeChar.Contains("u4"))
        {
            return 4;
        }
        if (typeChar.Contains("uint16") || typeChar.Contains("u2"))
        {
            return 2;
        }
        if (typeChar.Contains("uint8") || typeChar.Contains("u1"))
        {
            return 1;
        }

        _logger.LogWarning("Unknown dtype {TypeChar}, assuming 4 bytes", typeChar);
        return 4;
    }

    /// <summary>
    /// Converts byte order of array data if needed based on dtype endianness.
    /// </summary>
    /// <param name="data">Raw byte array from Zarr chunk</param>
    /// <param name="dtype">Zarr dtype string</param>
    /// <returns>Data with correct byte order for the system</returns>
    /// <remarks>
    /// Only performs conversion when:
    /// 1. Array endianness differs from system endianness
    /// 2. Element size > 1 byte (single bytes don't need conversion)
    /// Uses efficient in-place reversal for multi-byte elements.
    /// </remarks>
    private byte[] ConvertByteOrder(byte[] data, string dtype)
    {
        var (elementSize, arrayEndianness) = ParseDtype(dtype);

        // No conversion needed for single-byte types
        if (elementSize == 1 || arrayEndianness == Endianness.NotApplicable)
        {
            return data;
        }

        // Determine if conversion is needed
        var systemIsLittleEndian = BitConverter.IsLittleEndian;
        var needsConversion = (arrayEndianness == Endianness.LittleEndian && !systemIsLittleEndian) ||
                             (arrayEndianness == Endianness.BigEndian && systemIsLittleEndian);

        if (!needsConversion)
        {
            return data;
        }

        _logger.LogDebug("Converting byte order for dtype {DType} (element size: {Size}, system is {Endian})",
            dtype, elementSize, systemIsLittleEndian ? "little-endian" : "big-endian");

        // Convert byte order in-place
        var elementCount = data.Length / elementSize;
        for (int i = 0; i < elementCount; i++)
        {
            var offset = i * elementSize;
            ReverseBytes(data, offset, elementSize);
        }

        return data;
    }

    /// <summary>
    /// Reverses bytes for a single element in-place.
    /// </summary>
    private void ReverseBytes(byte[] data, int offset, int elementSize)
    {
        switch (elementSize)
        {
            case 2:
                // Swap 2 bytes
                (data[offset], data[offset + 1]) = (data[offset + 1], data[offset]);
                break;

            case 4:
                // Swap 4 bytes
                (data[offset], data[offset + 3]) = (data[offset + 3], data[offset]);
                (data[offset + 1], data[offset + 2]) = (data[offset + 2], data[offset + 1]);
                break;

            case 8:
                // Swap 8 bytes
                (data[offset], data[offset + 7]) = (data[offset + 7], data[offset]);
                (data[offset + 1], data[offset + 6]) = (data[offset + 6], data[offset + 1]);
                (data[offset + 2], data[offset + 5]) = (data[offset + 5], data[offset + 2]);
                (data[offset + 3], data[offset + 4]) = (data[offset + 4], data[offset + 3]);
                break;

            default:
                // Generic reversal for other sizes
                Array.Reverse(data, offset, elementSize);
                break;
        }
    }
}

/// <summary>
/// Byte order / endianness of array data.
/// </summary>
public enum Endianness
{
    /// <summary>
    /// Little-endian byte order (least significant byte first).
    /// Common on x86/x64 systems.
    /// </summary>
    LittleEndian,

    /// <summary>
    /// Big-endian byte order (most significant byte first).
    /// Common on some network protocols and older systems.
    /// </summary>
    BigEndian,

    /// <summary>
    /// Not applicable - used for single-byte types or string types.
    /// </summary>
    NotApplicable
}

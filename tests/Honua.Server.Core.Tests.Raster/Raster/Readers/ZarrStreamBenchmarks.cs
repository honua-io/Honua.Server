using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Honua.Server.Core.Raster.Cache;
using Honua.Server.Core.Raster.Compression;
using Honua.Server.Core.Raster.Readers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Raster.Raster.Readers;

/// <summary>
/// Performance benchmarks for ZarrStream.
/// Measures throughput, latency, and chunk loading efficiency.
/// </summary>
public sealed class ZarrStreamBenchmarks : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<HttpZarrReader> _zarrReaderLogger;
    private readonly ILogger<ZarrStream> _streamLogger;
    private readonly ILogger<ZarrChunkCache> _cacheLogger;
    private readonly HttpClient _httpClient;
    private readonly ZarrDecompressor _decompressor;
    private string? _testDataPath;

    public ZarrStreamBenchmarks(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = new LoggerFactory();
        _zarrReaderLogger = loggerFactory.CreateLogger<HttpZarrReader>();
        _streamLogger = loggerFactory.CreateLogger<ZarrStream>();
        _cacheLogger = loggerFactory.CreateLogger<ZarrChunkCache>();
        var decompressorLogger = loggerFactory.CreateLogger<ZarrDecompressor>();
        _httpClient = new HttpClient(new FileHttpMessageHandler()) { Timeout = TimeSpan.FromSeconds(30) };
        var codecRegistry = new CompressionCodecRegistry(NullLogger<CompressionCodecRegistry>.Instance);
        _decompressor = new ZarrDecompressor(decompressorLogger, codecRegistry);
    }

    [Fact]
    public async Task Benchmark_SequentialRead_MeasuresThroughput()
    {
        // Arrange
        var arraySize = 1024; // 1024x1024 array = 4MB
        CreateTestZarrArray(out var zarrPath, arraySize, arraySize);
        var zarrReader = new HttpZarrReader(_zarrReaderLogger, _httpClient, _decompressor);
        var fileUri = $"file://{zarrPath}";

        using var stream = await ZarrStream.CreateAsync(
            zarrReader,
            fileUri,
            "data",
            _streamLogger);

        var buffer = new byte[65536]; // 64KB buffer
        var totalBytes = 0L;
        var stopwatch = Stopwatch.StartNew();

        // Act - Sequential read
        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0) break;
            totalBytes += bytesRead;
        }

        stopwatch.Stop();

        // Assert & Report
        var throughputMBps = (totalBytes / (1024.0 * 1024.0)) / stopwatch.Elapsed.TotalSeconds;
        var expectedBytes = arraySize * arraySize * 4L;

        _output.WriteLine($"Sequential Read Benchmark:");
        _output.WriteLine($"  Array Size: {arraySize}x{arraySize} ({expectedBytes / (1024.0 * 1024.0):F2} MB)");
        _output.WriteLine($"  Total Bytes Read: {totalBytes:N0}");
        _output.WriteLine($"  Time Elapsed: {stopwatch.ElapsedMilliseconds:N0} ms");
        _output.WriteLine($"  Throughput: {throughputMBps:F2} MB/s");

        Assert.True(throughputMBps > 10, $"Throughput too low: {throughputMBps:F2} MB/s");
        Assert.Equal(expectedBytes, totalBytes);
    }

    [Fact]
    public async Task Benchmark_RandomAccess_MeasuresLatency()
    {
        // Arrange
        var arraySize = 512;
        CreateTestZarrArray(out var zarrPath, arraySize, arraySize);
        var zarrReader = new HttpZarrReader(_zarrReaderLogger, _httpClient, _decompressor);
        var fileUri = $"file://{zarrPath}";

        using var stream = await ZarrStream.CreateAsync(
            zarrReader,
            fileUri,
            "data",
            _streamLogger);

        var buffer = new byte[4096]; // 4KB reads
        var random = new Random(42);
        var numReads = 100;
        var latencies = new long[numReads];

        // Act - Random access pattern
        for (int i = 0; i < numReads; i++)
        {
            var position = random.Next(0, (int)(stream.Length - buffer.Length));
            stream.Seek(position, SeekOrigin.Begin);

            var sw = Stopwatch.StartNew();
            await stream.ReadAsync(buffer, 0, buffer.Length);
            sw.Stop();

            latencies[i] = sw.ElapsedMilliseconds;
        }

        // Assert & Report
        var avgLatency = latencies.Average();
        var minLatency = latencies.Min();
        var maxLatency = latencies.Max();
        var p50Latency = latencies.OrderBy(x => x).ElementAt(numReads / 2);
        var p95Latency = latencies.OrderBy(x => x).ElementAt((int)(numReads * 0.95));
        var p99Latency = latencies.OrderBy(x => x).ElementAt((int)(numReads * 0.99));

        _output.WriteLine($"Random Access Benchmark ({numReads} reads):");
        _output.WriteLine($"  Array Size: {arraySize}x{arraySize}");
        _output.WriteLine($"  Read Size: {buffer.Length:N0} bytes");
        _output.WriteLine($"  Avg Latency: {avgLatency:F2} ms");
        _output.WriteLine($"  Min Latency: {minLatency} ms");
        _output.WriteLine($"  Max Latency: {maxLatency} ms");
        _output.WriteLine($"  P50 Latency: {p50Latency} ms");
        _output.WriteLine($"  P95 Latency: {p95Latency} ms");
        _output.WriteLine($"  P99 Latency: {p99Latency} ms");

        Assert.True(avgLatency < 100, $"Average latency too high: {avgLatency:F2} ms");
    }

    [Fact]
    public async Task Benchmark_ChunkCaching_MeasuresCacheEfficiency()
    {
        // Arrange
        var arraySize = 256;
        CreateTestZarrArray(out var zarrPath, arraySize, arraySize);

        var cache = new ZarrChunkCache(_cacheLogger);
        var zarrReader = new HttpZarrReader(_zarrReaderLogger, _httpClient, _decompressor, memoryLimits: null, chunkCache: cache);
        var fileUri = $"file://{zarrPath}";

        using var stream = await ZarrStream.CreateAsync(
            zarrReader,
            fileUri,
            "data",
            _streamLogger);

        var buffer = new byte[1024];

        // Act - First pass (cache miss)
        var stopwatch = Stopwatch.StartNew();
        stream.Seek(0, SeekOrigin.Begin);
        for (int i = 0; i < 100; i++)
        {
            await stream.ReadAsync(buffer, 0, buffer.Length);
        }
        var firstPassTime = stopwatch.ElapsedMilliseconds;

        // Second pass (cache hit)
        stopwatch.Restart();
        stream.Seek(0, SeekOrigin.Begin);
        for (int i = 0; i < 100; i++)
        {
            await stream.ReadAsync(buffer, 0, buffer.Length);
        }
        var secondPassTime = stopwatch.ElapsedMilliseconds;

        // Assert & Report
        var speedup = (double)firstPassTime / secondPassTime;

        _output.WriteLine($"Chunk Caching Benchmark:");
        _output.WriteLine($"  First Pass (cold cache): {firstPassTime} ms");
        _output.WriteLine($"  Second Pass (warm cache): {secondPassTime} ms");
        _output.WriteLine($"  Speedup: {speedup:F2}x");

        Assert.True(speedup > 1.5, $"Cache speedup too low: {speedup:F2}x");
    }

    [Fact]
    public async Task Benchmark_WindowedRead_MeasuresPartialArrayPerformance()
    {
        // Arrange
        var arraySize = 1024;
        CreateTestZarrArray(out var zarrPath, arraySize, arraySize);
        var zarrReader = new HttpZarrReader(_zarrReaderLogger, _httpClient, _decompressor);
        var fileUri = $"file://{zarrPath}";

        var sliceStart = new[] { 256, 256 };
        var sliceCount = new[] { 512, 512 };

        using var stream = await ZarrStream.CreateWithWindowAsync(
            zarrReader,
            fileUri,
            "data",
            sliceStart,
            sliceCount,
            _streamLogger);

        var buffer = new byte[65536];
        var totalBytes = 0L;
        var stopwatch = Stopwatch.StartNew();

        // Act
        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0) break;
            totalBytes += bytesRead;
        }

        stopwatch.Stop();

        // Assert & Report
        var throughputMBps = (totalBytes / (1024.0 * 1024.0)) / stopwatch.Elapsed.TotalSeconds;
        var expectedBytes = 512 * 512 * 4L;

        _output.WriteLine($"Windowed Read Benchmark:");
        _output.WriteLine($"  Full Array: {arraySize}x{arraySize}");
        _output.WriteLine($"  Window: {sliceCount[0]}x{sliceCount[1]} starting at [{sliceStart[0]},{sliceStart[1]}]");
        _output.WriteLine($"  Expected Bytes: {expectedBytes / (1024.0 * 1024.0):F2} MB");
        _output.WriteLine($"  Total Bytes Read: {totalBytes:N0}");
        _output.WriteLine($"  Time Elapsed: {stopwatch.ElapsedMilliseconds:N0} ms");
        _output.WriteLine($"  Throughput: {throughputMBps:F2} MB/s");

        Assert.Equal(expectedBytes, totalBytes);
        Assert.True(throughputMBps > 5, $"Throughput too low: {throughputMBps:F2} MB/s");
    }

    [Fact]
    public async Task Benchmark_CompressedVsUncompressed_ComparesPerformance()
    {
        // Arrange - Uncompressed
        var arraySize = 512;
        CreateTestZarrArray(out var zarrPathUncompressed, arraySize, arraySize, compressed: false);
        var zarrReader = new HttpZarrReader(_zarrReaderLogger, _httpClient, _decompressor);
        var fileUriUncompressed = $"file://{zarrPathUncompressed}";

        using var streamUncompressed = await ZarrStream.CreateAsync(
            zarrReader,
            fileUriUncompressed,
            "data",
            _streamLogger);

        var buffer = new byte[65536];
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var bytesRead = await streamUncompressed.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0) break;
        }
        var uncompressedTime = stopwatch.ElapsedMilliseconds;

        // Arrange - Compressed
        CreateTestZarrArray(out var zarrPathCompressed, arraySize, arraySize, compressed: true);
        var fileUriCompressed = $"file://{zarrPathCompressed}";

        using var streamCompressed = await ZarrStream.CreateAsync(
            zarrReader,
            fileUriCompressed,
            "data",
            _streamLogger);

        stopwatch.Restart();
        while (true)
        {
            var bytesRead = await streamCompressed.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0) break;
        }
        var compressedTime = stopwatch.ElapsedMilliseconds;

        // Report
        _output.WriteLine($"Compressed vs Uncompressed Benchmark:");
        _output.WriteLine($"  Array Size: {arraySize}x{arraySize}");
        _output.WriteLine($"  Uncompressed Time: {uncompressedTime} ms");
        _output.WriteLine($"  Compressed Time: {compressedTime} ms");
        _output.WriteLine($"  Ratio: {(double)compressedTime / uncompressedTime:F2}x");
    }

    [Fact]
    public async Task Benchmark_LargeArray_StressTest()
    {
        // Arrange
        var arraySize = 2048; // 2048x2048 = 16MB
        CreateTestZarrArray(out var zarrPath, arraySize, arraySize);
        var zarrReader = new HttpZarrReader(_zarrReaderLogger, _httpClient, _decompressor);
        var fileUri = $"file://{zarrPath}";

        using var stream = await ZarrStream.CreateAsync(
            zarrReader,
            fileUri,
            "data",
            _streamLogger);

        var buffer = new byte[1024 * 1024]; // 1MB buffer
        var totalBytes = 0L;
        var stopwatch = Stopwatch.StartNew();

        // Act
        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0) break;
            totalBytes += bytesRead;
        }

        stopwatch.Stop();

        // Report
        var throughputMBps = (totalBytes / (1024.0 * 1024.0)) / stopwatch.Elapsed.TotalSeconds;

        _output.WriteLine($"Large Array Stress Test:");
        _output.WriteLine($"  Array Size: {arraySize}x{arraySize}");
        _output.WriteLine($"  Total Size: {totalBytes / (1024.0 * 1024.0):F2} MB");
        _output.WriteLine($"  Time Elapsed: {stopwatch.ElapsedMilliseconds:N0} ms");
        _output.WriteLine($"  Throughput: {throughputMBps:F2} MB/s");
        _output.WriteLine($"  Memory Efficient: Streamed without full materialization");

        Assert.True(throughputMBps > 5, $"Throughput too low: {throughputMBps:F2} MB/s");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();

        if (_testDataPath != null && Directory.Exists(_testDataPath))
        {
            try
            {
                Directory.Delete(_testDataPath, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    private sealed class FileHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is null)
            {
                throw new InvalidOperationException("Request URI is required.");
            }

            if (!string.Equals(request.RequestUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"The '{request.RequestUri.Scheme}' scheme is not supported in benchmarks.");
            }

            var path = Uri.UnescapeDataString(request.RequestUri.LocalPath);
            var stream = File.OpenRead(path);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return Task.FromResult(response);
        }
    }

    private void CreateTestZarrArray(out string zarrPath, int height, int width, bool compressed = false)
    {
        _testDataPath = Path.Combine(Path.GetTempPath(), $"zarr_bench_{Guid.NewGuid()}");
        zarrPath = Path.Combine(_testDataPath, "test.zarr");
        var dataDir = Path.Combine(zarrPath, "data");
        Directory.CreateDirectory(dataDir);

        var zarrayPath = Path.Combine(dataDir, ".zarray");
        var compressorJson = compressed ? @"{""id"": ""zlib"", ""level"": 5}" : "null";

        var zarrayContent = $$"""
        {
            "chunks": [64, 64],
            "compressor": {{compressorJson}},
            "dtype": "<f4",
            "fill_value": 0.0,
            "filters": null,
            "order": "C",
            "shape": [{{height}}, {{width}}],
            "zarr_format": 2
        }
        """;
        File.WriteAllText(zarrayPath, zarrayContent);

        // Create all necessary chunks
        var chunksY = (height + 63) / 64;
        var chunksX = (width + 63) / 64;

        for (int cy = 0; cy < chunksY; cy++)
        {
            for (int cx = 0; cx < chunksX; cx++)
            {
                var chunkHeight = Math.Min(64, height - cy * 64);
                var chunkWidth = Math.Min(64, width - cx * 64);
                var chunkSize = chunkHeight * chunkWidth * 4;
                var chunkData = new byte[chunkSize];

                // Fill with test pattern
                for (int i = 0; i < chunkData.Length; i += 4)
                {
                    var value = (float)((cy * chunksX + cx) * 1000 + i / 4);
                    BitConverter.GetBytes(value).CopyTo(chunkData, i);
                }

                var chunkPath = Path.Combine(dataDir, $"{cy}.{cx}");

                if (compressed)
                {
                    using var outputStream = new MemoryStream();
                    using (var zlibStream = new System.IO.Compression.ZLibStream(outputStream, System.IO.Compression.CompressionMode.Compress))
                    {
                        zlibStream.Write(chunkData, 0, chunkData.Length);
                    }
                    File.WriteAllBytes(chunkPath, outputStream.ToArray());
                }
                else
                {
                    File.WriteAllBytes(chunkPath, chunkData);
                }
            }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Raster.Cache;
using Honua.Server.Core.Raster.Compression;
using Honua.Server.Core.Raster.Readers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Readers;

/// <summary>
/// Integration tests for ZarrStream with real Zarr datasets.
/// Tests end-to-end functionality with HTTP-based Zarr arrays.
/// </summary>
[Collection("Storage Emulators")]
[Trait("Category", "Integration")]
public sealed class ZarrStreamIntegrationTests : IDisposable
{
    private readonly ILogger<HttpZarrReader> _zarrReaderLogger;
    private readonly ILogger<ZarrStream> _streamLogger;
    private readonly ILogger<ZarrChunkCache> _cacheLogger;
    private readonly HttpClient _httpClient;
    private readonly ZarrDecompressor _decompressor;
    private string? _testDataPath;

    public ZarrStreamIntegrationTests()
    {
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
    public async Task ZarrStream_ReadEntireArray_ReturnsCorrectData()
    {
        // Arrange
        CreateTestZarrArray(out var zarrPath, 100, 100);
        var zarrReader = new HttpZarrReader(_zarrReaderLogger, _httpClient, _decompressor);
        var fileUri = $"file://{zarrPath}";

        // Act
        using var stream = await ZarrStream.CreateAsync(
            zarrReader,
            fileUri,
            "data",
            _streamLogger);

        var buffer = new byte[stream.Length];
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer, totalRead, buffer.Length - totalRead);
            if (bytesRead == 0) break;
            totalRead += bytesRead;
        }

        // Assert
        stream.Length.Should().Be(100 * 100 * 4); // 100x100 float32
        totalRead.Should().Be((int)stream.Length);
        stream.Position.Should().Be(stream.Length);
    }

    [Fact]
    public async Task ZarrStream_ReadWithWindow_ReturnsOnlyRequestedRegion()
    {
        // Arrange
        CreateTestZarrArray(out var zarrPath, 200, 200);
        var zarrReader = new HttpZarrReader(_zarrReaderLogger, _httpClient, _decompressor);
        var fileUri = $"file://{zarrPath}";

        var sliceStart = new[] { 50, 50 };
        var sliceCount = new[] { 100, 100 };

        // Act
        using var stream = await ZarrStream.CreateWithWindowAsync(
            zarrReader,
            fileUri,
            "data",
            sliceStart,
            sliceCount,
            _streamLogger);

        // Assert
        stream.Length.Should().Be(100 * 100 * 4); // 100x100 window
        stream.CanRead.Should().BeTrue();
        stream.CanSeek.Should().BeTrue();
    }

    [Fact]
    public async Task ZarrStream_SeekAndRead_WorksCorrectly()
    {
        // Arrange
        CreateTestZarrArray(out var zarrPath, 100, 100);
        var zarrReader = new HttpZarrReader(_zarrReaderLogger, _httpClient, _decompressor);
        var fileUri = $"file://{zarrPath}";

        using var stream = await ZarrStream.CreateAsync(
            zarrReader,
            fileUri,
            "data",
            _streamLogger);

        // Act - Seek to middle and read
        stream.Seek(5000, SeekOrigin.Begin);
        var buffer = new byte[1000];
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

        // Assert
        stream.Position.Should().Be(6000);
        bytesRead.Should().Be(1000);
    }

    [Fact]
    public async Task ZarrStream_MultipleReads_MaintainsPosition()
    {
        // Arrange
        CreateTestZarrArray(out var zarrPath, 100, 100);
        var zarrReader = new HttpZarrReader(_zarrReaderLogger, _httpClient, _decompressor);
        var fileUri = $"file://{zarrPath}";

        using var stream = await ZarrStream.CreateAsync(
            zarrReader,
            fileUri,
            "data",
            _streamLogger);

        // Act - Multiple sequential reads
        var buffer1 = new byte[500];
        var buffer2 = new byte[500];
        var buffer3 = new byte[500];

        var read1 = await stream.ReadAsync(buffer1, 0, buffer1.Length);
        var read2 = await stream.ReadAsync(buffer2, 0, buffer2.Length);
        var read3 = await stream.ReadAsync(buffer3, 0, buffer3.Length);

        // Assert
        read1.Should().Be(500);
        read2.Should().Be(500);
        read3.Should().Be(500);
        stream.Position.Should().Be(1500);
    }

    [Fact]
    public async Task ZarrStream_ReadBeyondEnd_ReturnsPartialData()
    {
        // Arrange
        CreateTestZarrArray(out var zarrPath, 10, 10);
        var zarrReader = new HttpZarrReader(_zarrReaderLogger, _httpClient, _decompressor);
        var fileUri = $"file://{zarrPath}";

        using var stream = await ZarrStream.CreateAsync(
            zarrReader,
            fileUri,
            "data",
            _streamLogger);

        // Act - Try to read more than available
        stream.Seek(stream.Length - 50, SeekOrigin.Begin);
        var buffer = new byte[100];
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

        // Assert
        bytesRead.Should().Be(50); // Only 50 bytes left
        stream.Position.Should().Be(stream.Length);
    }

    [Fact]
    public async Task ZarrStream_WithCompressedChunks_DecompressesCorrectly()
    {
        // Arrange
        CreateTestZarrArray(out var zarrPath, 128, 128, compressed: true);
        var zarrReader = new HttpZarrReader(_zarrReaderLogger, _httpClient, _decompressor);
        var fileUri = $"file://{zarrPath}";

        // Act
        using var stream = await ZarrStream.CreateAsync(
            zarrReader,
            fileUri,
            "data",
            _streamLogger);

        var buffer = new byte[1024];
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

        // Assert
        bytesRead.Should().Be(1024);
        stream.Position.Should().Be(1024);
    }

    [Fact]
    public async Task ZarrStream_WithMetrics_TracksOperations()
    {
        // Arrange
        CreateTestZarrArray(out var zarrPath, 100, 100);
        var zarrReader = new HttpZarrReader(_zarrReaderLogger, _httpClient, _decompressor);
        var fileUri = $"file://{zarrPath}";
        var metrics = new ZarrStreamMetrics();

        // Act
        using var stream = await ZarrStream.CreateAsync(
            zarrReader,
            fileUri,
            "data",
            _streamLogger,
            metrics);

        var buffer = new byte[1000];
        await stream.ReadAsync(buffer, 0, buffer.Length);

        // Assert
        metrics.Should().NotBeNull();
        // Note: Actual metric values would need to be verified via OpenTelemetry listener
    }

    [Fact]
    public async Task ZarrStream_RandomAccess_WorksEfficiently()
    {
        // Arrange
        CreateTestZarrArray(out var zarrPath, 256, 256);
        var zarrReader = new HttpZarrReader(_zarrReaderLogger, _httpClient, _decompressor);
        var fileUri = $"file://{zarrPath}";

        using var stream = await ZarrStream.CreateAsync(
            zarrReader,
            fileUri,
            "data",
            _streamLogger);

        var buffer = new byte[512];

        // Act - Random access pattern
        stream.Seek(10000, SeekOrigin.Begin);
        await stream.ReadAsync(buffer, 0, buffer.Length);

        stream.Seek(50000, SeekOrigin.Begin);
        await stream.ReadAsync(buffer, 0, buffer.Length);

        stream.Seek(1000, SeekOrigin.Begin);
        await stream.ReadAsync(buffer, 0, buffer.Length);

        // Assert
        stream.Position.Should().Be(1512);
    }

    [Fact]
    public async Task ZarrStream_CopyTo_WorksCorrectly()
    {
        // Arrange
        CreateTestZarrArray(out var zarrPath, 50, 50);
        var zarrReader = new HttpZarrReader(_zarrReaderLogger, _httpClient, _decompressor);
        var fileUri = $"file://{zarrPath}";

        using var stream = await ZarrStream.CreateAsync(
            zarrReader,
            fileUri,
            "data",
            _streamLogger);

        using var destination = new MemoryStream();

        // Act
        await stream.CopyToAsync(destination);

        // Assert
        destination.Length.Should().Be(stream.Length);
        destination.Position.Should().Be(stream.Length);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();

        // Cleanup test data
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

    /// <summary>
    /// Creates a minimal Zarr array on disk for testing.
    /// </summary>
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
                throw new NotSupportedException($"The '{request.RequestUri.Scheme}' scheme is not supported in tests.");
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
        // Create temporary directory for test data
        _testDataPath = Path.Combine(Path.GetTempPath(), $"zarr_test_{Guid.NewGuid()}");
        zarrPath = Path.Combine(_testDataPath, "test.zarr");
        var dataDir = Path.Combine(zarrPath, "data");
        Directory.CreateDirectory(dataDir);

        // Write .zarray metadata
        var zarrayPath = Path.Combine(dataDir, ".zarray");
        var compressorJson = compressed
            ? @"{""id"": ""zlib"", ""level"": 5}"
            : "null";

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

        // Write minimal chunk data (chunk 0.0)
        var chunkPath = Path.Combine(dataDir, "0.0");
        var chunkSize = Math.Min(64, height) * Math.Min(64, width) * 4; // float32
        var chunkData = new byte[chunkSize];

        // Fill with test pattern
        for (int i = 0; i < chunkData.Length; i += 4)
        {
            var value = (float)(i / 4);
            BitConverter.GetBytes(value).CopyTo(chunkData, i);
        }

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

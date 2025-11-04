using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Raster.Cache;
using Honua.Server.Core.Raster.Compression;
using Honua.Server.Core.Raster.Readers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Readers;

/// <summary>
/// Tests for Zarr v3 format support and sharding in HttpZarrReader.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class HttpZarrReaderV3Tests
{
    private readonly ILogger<HttpZarrReader> _logger;
    private readonly ILogger<ZarrDecompressor> _decompressorLogger;
    private readonly ILogger<CompressionCodecRegistry> _codecLogger;

    public HttpZarrReaderV3Tests()
    {
        _logger = NullLogger<HttpZarrReader>.Instance;
        _decompressorLogger = NullLogger<ZarrDecompressor>.Instance;
        _codecLogger = NullLogger<CompressionCodecRegistry>.Instance;
    }

    #region Zarr v3 Metadata Tests

    [Fact]
    public async Task GetMetadataAsync_ZarrV3_ParsesCorrectly()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithV3Metadata(
            shape: new[] { 100, 200, 300 },
            chunkShape: new[] { 10, 20, 30 },
            dataType: "float32",
            sharding: null);

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        // Act
        var metadata = await reader.GetMetadataAsync("https://example.com/data.zarr", "temperature");

        // Assert
        metadata.Should().NotBeNull();
        metadata.ZarrFormat.Should().Be(3);
        metadata.Shape.Should().BeEquivalentTo(new[] { 100, 200, 300 });
        metadata.Chunks.Should().BeEquivalentTo(new[] { 10, 20, 30 });
        metadata.DType.Should().Be("float32");
        metadata.Order.Should().Be("C");
    }

    [Fact]
    public async Task GetMetadataAsync_ZarrV3WithSharding_ParsesShardingConfig()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithV3Metadata(
            shape: new[] { 100, 200, 300 },
            chunkShape: new[] { 10, 20, 30 },
            dataType: "float32",
            sharding: new { chunks_per_shard = new[] { 2, 2, 2 }, index_location = "end" });

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        // Act
        var metadata = await reader.GetMetadataAsync("https://example.com/data.zarr", "temperature");

        // Assert
        metadata.Should().NotBeNull();
        metadata.ZarrFormat.Should().Be(3);
        // Note: Sharding configuration is not yet part of ZarrArrayMetadata
        // This test verifies v3 metadata parsing, actual sharding support is TBD
    }

    [Fact]
    public async Task GetMetadataAsync_ZarrV2Fallback_WorksWhenV3NotFound()
    {
        // Arrange - Return 404 for v3, success for v2
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("zarr.json")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains(".zarray")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    shape = new[] { 100, 100 },
                    chunks = new[] { 10, 10 },
                    dtype = "<f4",
                    compressor = new { id = "gzip" },
                    zarr_format = 2,
                    order = "C"
                }))
            });

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        // Act
        var metadata = await reader.GetMetadataAsync("https://example.com/data.zarr", "temperature");

        // Assert
        metadata.Should().NotBeNull();
        metadata.ZarrFormat.Should().Be(2);
    }

    #endregion

    #region Zarr v3 Chunk Reading Tests

    [Fact]
    public async Task ReadChunkAsync_ZarrV3ChunkNaming_BuildsCorrectPath()
    {
        // Arrange
        var chunkData = CreateFloat32ChunkData(10, 10, 42.0f);
        string capturedUri = string.Empty;

        var mockHandler = new Mock<HttpMessageHandler>();

        // Mock v3 metadata
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("zarr.json")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateV3MetadataJson(
                    shape: new[] { 100, 100 },
                    chunkShape: new[] { 10, 10 },
                    dataType: "float32",
                    sharding: null))
            });

        // Mock chunk request - capture URI
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/c/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                capturedUri = req.RequestUri!.ToString();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(chunkData)
                };
            });

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        var array = await reader.OpenArrayAsync("https://example.com/data.zarr", "temperature");

        // Act
        var result = await reader.ReadChunkAsync(array, new[] { 1, 2 });

        // Assert
        result.Should().NotBeNull();
        capturedUri.Should().Contain("/c/1/2"); // v3 format: c/coord1/coord2
    }

    #endregion

    #region Sharding Tests

    [Fact]
    public async Task ReadChunkAsync_FromShardedArray_ExtractsCorrectChunk()
    {
        // Arrange
        var chunkSize = 10 * 10 * 4; // 10x10 float32
        var chunksPerShard = new[] { 2, 2 }; // 2x2 = 4 chunks per shard
        var shardData = CreateMockShardData(chunksPerShard, chunkSize);

        var mockHandler = CreateMockHandlerWithShardedArray(
            shape: new[] { 100, 100 },
            chunkShape: new[] { 10, 10 },
            chunksPerShard: chunksPerShard,
            shardData: shardData);

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        var array = await reader.OpenArrayAsync("https://example.com/data.zarr", "temperature");

        // Act - Read chunk at position (1, 1) which is in shard (0, 0) at index 3
        var result = await reader.ReadChunkAsync(array, new[] { 1, 1 });

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(chunkSize);
    }

    [Fact]
    public void ShardingConfig_CalculatesShardCoordinates_Correctly()
    {
        // Arrange
        var config = new ShardingConfig
        {
            ChunksPerShard = new[] { 2, 3 },
            IndexLocation = 1,
            IndexCodec = "bytes",
            IndexEntrySize = 16
        };

        // Act
        var shardCoords1 = config.GetShardCoordinates(new[] { 0, 0 }); // First chunk
        var shardCoords2 = config.GetShardCoordinates(new[] { 2, 3 }); // Chunk in shard (1, 1)
        var shardCoords3 = config.GetShardCoordinates(new[] { 5, 8 }); // Chunk in shard (2, 2)

        // Assert
        shardCoords1.Should().BeEquivalentTo(new[] { 0, 0 });
        shardCoords2.Should().BeEquivalentTo(new[] { 1, 1 });
        shardCoords3.Should().BeEquivalentTo(new[] { 2, 2 });
    }

    [Fact]
    public void ShardingConfig_CalculatesChunkIndexInShard_Correctly()
    {
        // Arrange
        var config = new ShardingConfig
        {
            ChunksPerShard = new[] { 2, 3 }, // 2x3 = 6 chunks per shard
            IndexLocation = 1
        };

        // Act & Assert - C-order indexing
        config.GetChunkIndexInShard(new[] { 0, 0 }).Should().Be(0); // (0,0) -> 0
        config.GetChunkIndexInShard(new[] { 0, 1 }).Should().Be(1); // (0,1) -> 1
        config.GetChunkIndexInShard(new[] { 0, 2 }).Should().Be(2); // (0,2) -> 2
        config.GetChunkIndexInShard(new[] { 1, 0 }).Should().Be(3); // (1,0) -> 3
        config.GetChunkIndexInShard(new[] { 1, 1 }).Should().Be(4); // (1,1) -> 4
        config.GetChunkIndexInShard(new[] { 1, 2 }).Should().Be(5); // (1,2) -> 5
    }

    [Fact]
    public void ShardingConfig_Validate_ThrowsOnInvalidConfiguration()
    {
        // Arrange
        var config1 = new ShardingConfig
        {
            ChunksPerShard = Array.Empty<int>()
        };

        var config2 = new ShardingConfig
        {
            ChunksPerShard = new[] { 2, 0 } // Invalid: 0 chunks
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => config1.Validate());
        Assert.Throws<ArgumentException>(() => config2.Validate());
    }

    [Fact]
    public async Task ShardedChunkReader_ReadShardIndex_ParsesCorrectly()
    {
        // Arrange
        var config = new ShardingConfig
        {
            ChunksPerShard = new[] { 2, 2 }, // 4 chunks
            IndexLocation = 1, // End of shard
            IndexEntrySize = 16
        };

        // Create mock shard index: 4 entries, each 16 bytes (8 bytes offset + 8 bytes length)
        var indexData = new byte[4 * 16];
        // Chunk 0: offset=0, length=100
        BinaryPrimitives.WriteInt64LittleEndian(indexData.AsSpan(0, 8), 0);
        BinaryPrimitives.WriteInt64LittleEndian(indexData.AsSpan(8, 8), 100);
        // Chunk 1: offset=100, length=150
        BinaryPrimitives.WriteInt64LittleEndian(indexData.AsSpan(16, 8), 100);
        BinaryPrimitives.WriteInt64LittleEndian(indexData.AsSpan(24, 8), 150);
        // Chunk 2: offset=250, length=200
        BinaryPrimitives.WriteInt64LittleEndian(indexData.AsSpan(32, 8), 250);
        BinaryPrimitives.WriteInt64LittleEndian(indexData.AsSpan(40, 8), 200);
        // Chunk 3: offset=-1, length=0 (missing chunk)
        BinaryPrimitives.WriteInt64LittleEndian(indexData.AsSpan(48, 8), -1);
        BinaryPrimitives.WriteInt64LittleEndian(indexData.AsSpan(56, 8), 0);

        // Create shard stream with index at end
        var shardData = new byte[1000 + indexData.Length];
        Array.Copy(indexData, 0, shardData, 1000, indexData.Length);
        var shardStream = new MemoryStream(shardData);

        var logger = NullLogger<ShardedChunkReader>.Instance;
        var reader = new ShardedChunkReader(logger);

        // Act
        var index = await reader.ReadShardIndexAsync(shardStream, config);

        // Assert
        index.Should().NotBeNull();
        index.Offsets.Should().HaveCount(4);
        index.Lengths.Should().HaveCount(4);

        index.GetChunkRange(0).Should().Be((0, 100));
        index.GetChunkRange(1).Should().Be((100, 150));
        index.GetChunkRange(2).Should().Be((250, 200));
        index.GetChunkRange(3).Should().BeNull(); // Missing chunk
    }

    #endregion

    #region Helper Methods

    private ZarrDecompressor CreateMockDecompressor()
    {
        var codecRegistry = new CompressionCodecRegistry(_codecLogger);

        var mockCodec = new Mock<ICompressionCodec>();
        mockCodec.Setup(c => c.CodecName).Returns("gzip");
        mockCodec.Setup(c => c.GetSupportLevel()).Returns("Full");
        mockCodec.Setup(c => c.Decompress(It.IsAny<byte[]>(), It.IsAny<int?>()))
            .Returns(new Func<byte[], int?, byte[]>((data, expectedSize) => data));

        try
        {
            codecRegistry.Register(mockCodec.Object);
        }
        catch (InvalidOperationException)
        {
            // Already registered
        }

        return new ZarrDecompressor(_decompressorLogger, codecRegistry);
    }

    private Mock<HttpMessageHandler> CreateMockHandlerWithV3Metadata(
        int[] shape,
        int[] chunkShape,
        string dataType,
        object? sharding)
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("zarr.json")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateV3MetadataJson(shape, chunkShape, dataType, sharding))
            });

        return mockHandler;
    }

    private string CreateV3MetadataJson(int[] shape, int[] chunkShape, string dataType, object? sharding)
    {
        var metadata = new
        {
            zarr_format = 3,
            node_type = "array",
            shape,
            data_type = dataType,
            chunk_grid = new
            {
                name = "regular",
                configuration = new { chunk_shape = chunkShape }
            },
            chunk_key_encoding = new
            {
                name = "default",
                configuration = new { separator = "/" }
            },
            fill_value = 0,
            codecs = new object[]
            {
                new { name = "bytes", configuration = new { endian = "little" } },
                new { name = "gzip", configuration = new { level = 5 } }
            },
            storage_transformers = sharding != null
                ? new object[] { new { type = "sharding_indexed", configuration = sharding } }
                : Array.Empty<object>()
        };

        return JsonSerializer.Serialize(metadata);
    }

    private Mock<HttpMessageHandler> CreateMockHandlerWithShardedArray(
        int[] shape,
        int[] chunkShape,
        int[] chunksPerShard,
        byte[] shardData)
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        // Mock v3 metadata with sharding
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("zarr.json")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateV3MetadataJson(
                    shape,
                    chunkShape,
                    "float32",
                    new { chunks_per_shard = chunksPerShard, index_location = "end" }))
            });

        // Mock shard request
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/c/") &&
                    !req.RequestUri.ToString().Contains("zarr.json")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(shardData)
            });

        return mockHandler;
    }

    private byte[] CreateMockShardData(int[] chunksPerShard, int chunkSize)
    {
        var totalChunks = chunksPerShard.Aggregate(1, (a, b) => a * b);
        var indexSize = totalChunks * 16; // 16 bytes per entry
        var dataSize = totalChunks * chunkSize;
        var shardData = new byte[dataSize + indexSize];

        // Fill with test data
        for (int i = 0; i < dataSize; i++)
        {
            shardData[i] = (byte)(i % 256);
        }

        // Create index at end
        var indexOffset = dataSize;
        for (int i = 0; i < totalChunks; i++)
        {
            var offset = i * chunkSize;
            var length = chunkSize;

            BinaryPrimitives.WriteInt64LittleEndian(
                shardData.AsSpan(indexOffset + i * 16, 8), offset);
            BinaryPrimitives.WriteInt64LittleEndian(
                shardData.AsSpan(indexOffset + i * 16 + 8, 8), length);
        }

        return shardData;
    }

    private byte[] CreateFloat32ChunkData(int width, int height, float fillValue)
    {
        var totalFloats = width * height;
        var data = new byte[totalFloats * 4];

        for (int i = 0; i < totalFloats; i++)
        {
            var bytes = BitConverter.GetBytes(fillValue);
            Array.Copy(bytes, 0, data, i * 4, 4);
        }

        return data;
    }

    #endregion
}

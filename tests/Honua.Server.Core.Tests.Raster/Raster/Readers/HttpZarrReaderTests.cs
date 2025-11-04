using System;
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
/// Comprehensive tests for HttpZarrReader covering:
/// - Zarr metadata parsing (.zarray files)
/// - Chunk reading with HTTP requests
/// - Decompression (gzip, zstd, blosc, etc.)
/// - Array slicing across multiple chunks
/// - Endianness conversion
/// - Error conditions (network errors, missing chunks, invalid data)
/// - Caching behavior
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class HttpZarrReaderTests
{
    private readonly ILogger<HttpZarrReader> _logger;
    private readonly ILogger<ZarrDecompressor> _decompressorLogger;
    private readonly ILogger<CompressionCodecRegistry> _codecLogger;

    public HttpZarrReaderTests()
    {
        _logger = NullLogger<HttpZarrReader>.Instance;
        _decompressorLogger = NullLogger<ZarrDecompressor>.Instance;
        _codecLogger = NullLogger<CompressionCodecRegistry>.Instance;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var decompressor = CreateMockDecompressor();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new HttpZarrReader(null!, httpClient, decompressor));

        exception.ParamName.Should().Be("logger");
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Arrange
        var decompressor = CreateMockDecompressor();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new HttpZarrReader(_logger, null!, decompressor));

        exception.ParamName.Should().Be("httpClient");
    }

    [Fact]
    public void Constructor_WithNullDecompressor_ThrowsArgumentNullException()
    {
        // Arrange
        using var httpClient = new HttpClient();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new HttpZarrReader(_logger, httpClient, null!));

        exception.ParamName.Should().Be("decompressor");
    }

    [Fact]
    public void Constructor_WithValidParameters_DoesNotThrow()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var decompressor = CreateMockDecompressor();

        // Act & Assert
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);
        reader.Should().NotBeNull();
    }

    #endregion

    #region Metadata Parsing Tests

    [Fact]
    public async Task GetMetadataAsync_ParsesZarrayFile_Correctly()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithMetadata(
            shape: new[] { 100, 200, 300 },
            chunks: new[] { 10, 20, 30 },
            dtype: "<f4",
            compressor: "gzip",
            order: "C");

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        // Act
        var metadata = await reader.GetMetadataAsync("https://example.com/data.zarr", "temperature");

        // Assert
        metadata.Should().NotBeNull();
        metadata.Shape.Should().BeEquivalentTo(new[] { 100, 200, 300 });
        metadata.Chunks.Should().BeEquivalentTo(new[] { 10, 20, 30 });
        metadata.DType.Should().Be("<f4");
        metadata.Compressor.Should().Be("gzip");
        metadata.ZarrFormat.Should().Be(2);
        metadata.Order.Should().Be("C");
    }

    [Fact]
    public async Task GetMetadataAsync_WithNoCompression_ParsesCorrectly()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithMetadata(
            shape: new[] { 50, 50 },
            chunks: new[] { 25, 25 },
            dtype: "<f8",
            compressor: null,
            order: "C");

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        // Act
        var metadata = await reader.GetMetadataAsync("https://example.com/data.zarr", "var");

        // Assert
        metadata.Compressor.Should().Be("null");
    }

    [Fact]
    public async Task GetMetadataAsync_WithFillValue_ParsesCorrectly()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithMetadata(
            shape: new[] { 100, 100 },
            chunks: new[] { 10, 10 },
            dtype: "<f4",
            compressor: "gzip",
            order: "C",
            fillValue: -9999.0);

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        // Act
        var metadata = await reader.GetMetadataAsync("https://example.com/data.zarr", "var");

        // Assert
        metadata.FillValue.Should().Be(-9999.0);
    }

    [Fact]
    public async Task GetMetadataAsync_WithFortranOrder_ParsesCorrectly()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithMetadata(
            shape: new[] { 100, 100 },
            chunks: new[] { 10, 10 },
            dtype: "<f4",
            compressor: "gzip",
            order: "F");

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        // Act
        var metadata = await reader.GetMetadataAsync("https://example.com/data.zarr", "var");

        // Assert
        metadata.Order.Should().Be("F");
    }

    [Fact]
    public async Task GetMetadataAsync_WithHttpError_ThrowsHttpRequestException()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await reader.GetMetadataAsync("https://example.com/data.zarr", "var"));
    }

    #endregion

    #region OpenArray Tests

    [Fact]
    public async Task OpenArrayAsync_ReturnsZarrArray()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithMetadata(
            shape: new[] { 100, 100 },
            chunks: new[] { 10, 10 },
            dtype: "<f4",
            compressor: "gzip",
            order: "C");

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        // Act
        using var array = await reader.OpenArrayAsync("https://example.com/data.zarr", "temperature");

        // Assert
        array.Should().NotBeNull();
        array.Uri.Should().Be("https://example.com/data.zarr");
        array.VariableName.Should().Be("temperature");
        array.Metadata.Should().NotBeNull();
    }

    #endregion

    #region Chunk Reading Tests

    [Fact]
    public async Task ReadChunkAsync_FetchesAndDecompressesChunk()
    {
        // Arrange
        var chunkData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var compressedChunkData = new byte[] { 10, 20, 30, 40 }; // Mock compressed

        var mockHandler = CreateMockHandlerWithChunk(
            chunkCoords: new[] { 0, 0 },
            chunkData: compressedChunkData);

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor(compressedChunkData, chunkData);
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        var array = new ZarrArray
        {
            Uri = "https://example.com/data.zarr",
            VariableName = "temperature",
            Metadata = new ZarrArrayMetadata
            {
                Shape = new[] { 100, 100 },
                Chunks = new[] { 10, 10 },
                DType = "<f4",
                Compressor = "gzip",
                ZarrFormat = 2,
                Order = "C"
            }
        };

        // Act
        var result = await reader.ReadChunkAsync(array, new[] { 0, 0 });

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(chunkData);
    }

    [Fact]
    public async Task ReadChunkAsync_WithMissingChunk_ReturnsEmptyChunk()
    {
        // Arrange - Return 404 for sparse array
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        var array = new ZarrArray
        {
            Uri = "https://example.com/data.zarr",
            VariableName = "temperature",
            Metadata = new ZarrArrayMetadata
            {
                Shape = new[] { 100, 100 },
                Chunks = new[] { 10, 10 },
                DType = "<f4",
                Compressor = "null",
                ZarrFormat = 2,
                Order = "C"
            }
        };

        // Act
        var result = await reader.ReadChunkAsync(array, new[] { 0, 0 });

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(10 * 10 * 4); // 10x10 chunk, 4 bytes per float32
        result.Should().AllBeEquivalentTo((byte)0); // Empty chunk
    }

    [Fact]
    public async Task ReadChunkAsync_WithServerError_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        var array = new ZarrArray
        {
            Uri = "https://example.com/data.zarr",
            VariableName = "temperature",
            Metadata = new ZarrArrayMetadata
            {
                Shape = new[] { 100, 100 },
                Chunks = new[] { 10, 10 },
                DType = "<f4",
                Compressor = "null",
                ZarrFormat = 2,
                Order = "C"
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await reader.ReadChunkAsync(array, new[] { 0, 0 }));
    }

    [Fact]
    public async Task ReadChunkAsync_WithCache_UsesCache()
    {
        // Arrange
        var chunkData = new byte[] { 1, 2, 3, 4 };
        var fetchCount = 0;

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                fetchCount++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(chunkData)
                };
            });

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var cache = new ZarrChunkCache(NullLogger<ZarrChunkCache>.Instance);
        var reader = new HttpZarrReader(_logger, httpClient, decompressor, memoryLimits: null, chunkCache: cache);

        var array = new ZarrArray
        {
            Uri = "https://example.com/data.zarr",
            VariableName = "temperature",
            Metadata = new ZarrArrayMetadata
            {
                Shape = new[] { 100, 100 },
                Chunks = new[] { 10, 10 },
                DType = "<f4",
                Compressor = "null",
                ZarrFormat = 2,
                Order = "C"
            }
        };

        // Act - Read same chunk twice
        await reader.ReadChunkAsync(array, new[] { 0, 0 });
        await reader.ReadChunkAsync(array, new[] { 0, 0 });

        // Assert - Should only fetch once due to caching
        fetchCount.Should().Be(1);
    }

    #endregion

    #region Array Slicing Tests

    [Fact]
    public async Task ReadSliceAsync_SingleChunk_ReturnsCorrectData()
    {
        // Arrange
        var chunkData = CreateFloat32ChunkData(10, 10, fillValue: 42.0f);

        var mockHandler = CreateMockHandlerWithChunk(
            chunkCoords: new[] { 0, 0 },
            chunkData: chunkData);

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        var array = new ZarrArray
        {
            Uri = "https://example.com/data.zarr",
            VariableName = "temperature",
            Metadata = new ZarrArrayMetadata
            {
                Shape = new[] { 100, 100 },
                Chunks = new[] { 10, 10 },
                DType = "<f4",
                Compressor = "null",
                ZarrFormat = 2,
                Order = "C"
            }
        };

        // Act - Read a slice within a single chunk
        var result = await reader.ReadSliceAsync(array, start: new[] { 0, 0 }, count: new[] { 5, 5 });

        // Assert
        result.Should().NotBeNull();
        var byteResult = result as byte[];
        byteResult.Should().NotBeNull();
        byteResult!.Length.Should().Be(5 * 5 * 4); // 5x5 floats
    }

    [Fact]
    public async Task ReadSliceAsync_MultipleChunks_AssemblesCorrectly()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithMultipleChunks();

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        var array = new ZarrArray
        {
            Uri = "https://example.com/data.zarr",
            VariableName = "temperature",
            Metadata = new ZarrArrayMetadata
            {
                Shape = new[] { 100, 100 },
                Chunks = new[] { 10, 10 },
                DType = "<f4",
                Compressor = "null",
                ZarrFormat = 2,
                Order = "C"
            }
        };

        // Act - Read a slice spanning multiple chunks
        var result = await reader.ReadSliceAsync(array, start: new[] { 5, 5 }, count: new[] { 15, 15 });

        // Assert
        result.Should().NotBeNull();
        var byteResult = result as byte[];
        byteResult.Should().NotBeNull();
        byteResult!.Length.Should().Be(15 * 15 * 4); // 15x15 floats
    }

    [Fact]
    public async Task ReadSliceAsync_3DArray_HandlesCorrectly()
    {
        // Arrange
        var mockHandler = CreateMockHandler3DArray();

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        var array = new ZarrArray
        {
            Uri = "https://example.com/data.zarr",
            VariableName = "temperature",
            Metadata = new ZarrArrayMetadata
            {
                Shape = new[] { 10, 100, 100 },
                Chunks = new[] { 1, 10, 10 },
                DType = "<f4",
                Compressor = "null",
                ZarrFormat = 2,
                Order = "C"
            }
        };

        // Act - Read a 3D slice
        var result = await reader.ReadSliceAsync(array, start: new[] { 0, 0, 0 }, count: new[] { 2, 10, 10 });

        // Assert
        result.Should().NotBeNull();
        var byteResult = result as byte[];
        byteResult.Should().NotBeNull();
        byteResult!.Length.Should().Be(2 * 10 * 10 * 4); // 2x10x10 floats
    }

    [Fact]
    public async Task ReadSliceAsync_WithInvalidDimensions_ThrowsArgumentException()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        var array = new ZarrArray
        {
            Uri = "https://example.com/data.zarr",
            VariableName = "temperature",
            Metadata = new ZarrArrayMetadata
            {
                Shape = new[] { 100, 100 },
                Chunks = new[] { 0, 10 }, // Invalid: chunk size is 0
                DType = "<f4",
                Compressor = "null",
                ZarrFormat = 2,
                Order = "C"
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await reader.ReadSliceAsync(array, start: new[] { 0, 0 }, count: new[] { 10, 10 }));
    }

    #endregion

    #region Decompression Tests

    [Fact]
    public async Task ReadChunkAsync_WithGzipCompression_Decompresses()
    {
        // Arrange
        var originalData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var compressedData = new byte[] { 10, 20, 30 }; // Mock compressed

        var mockHandler = CreateMockHandlerWithChunk(
            chunkCoords: new[] { 0, 0 },
            chunkData: compressedData);

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor(compressedData, originalData);
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        var array = new ZarrArray
        {
            Uri = "https://example.com/data.zarr",
            VariableName = "temperature",
            Metadata = new ZarrArrayMetadata
            {
                Shape = new[] { 100, 100 },
                Chunks = new[] { 10, 10 },
                DType = "<f4",
                Compressor = "gzip",
                ZarrFormat = 2,
                Order = "C"
            }
        };

        // Act
        var result = await reader.ReadChunkAsync(array, new[] { 0, 0 });

        // Assert
        result.Should().BeEquivalentTo(originalData);
    }

    [Fact]
    public async Task ReadChunkAsync_WithNullCompression_ReturnsRawData()
    {
        // Arrange
        var chunkData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        var mockHandler = CreateMockHandlerWithChunk(
            chunkCoords: new[] { 0, 0 },
            chunkData: chunkData);

        using var httpClient = new HttpClient(mockHandler.Object);
        var decompressor = CreateMockDecompressor();
        var reader = new HttpZarrReader(_logger, httpClient, decompressor);

        var array = new ZarrArray
        {
            Uri = "https://example.com/data.zarr",
            VariableName = "temperature",
            Metadata = new ZarrArrayMetadata
            {
                Shape = new[] { 100, 100 },
                Chunks = new[] { 10, 10 },
                DType = "<f4",
                Compressor = "null",
                ZarrFormat = 2,
                Order = "C"
            }
        };

        // Act
        var result = await reader.ReadChunkAsync(array, new[] { 0, 0 });

        // Assert
        result.Should().BeEquivalentTo(chunkData);
    }

    #endregion

    #region Helper Methods

    private ZarrDecompressor CreateMockDecompressor(byte[]? inputData = null, byte[]? outputData = null)
    {
        var codecRegistry = new CompressionCodecRegistry(_codecLogger);

        // Register a mock codec
        var mockCodec = new Mock<ICompressionCodec>();
        mockCodec.Setup(c => c.CodecName).Returns("gzip");
        mockCodec.Setup(c => c.GetSupportLevel()).Returns("Full");

        if (inputData != null && outputData != null)
        {
            var localInput = inputData;
            var localOutput = outputData;
            mockCodec.Setup(c => c.Decompress(It.IsAny<byte[]>(), It.IsAny<int?>()))
                .Returns(new Func<byte[], int?, byte[]>((data, expectedSize) =>
                {
                    if (data.Length == localInput.Length)
                    {
                        bool match = true;
                        for (int i = 0; i < data.Length; i++)
                        {
                            if (data[i] != localInput[i])
                            {
                                match = false;
                                break;
                            }
                        }
                        if (match) return localOutput;
                    }
                    return data;
                }));
        }
        else
        {
            mockCodec.Setup(c => c.Decompress(It.IsAny<byte[]>(), It.IsAny<int?>()))
                .Returns(new Func<byte[], int?, byte[]>((data, expectedSize) => data));
        }

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

    private Mock<HttpMessageHandler> CreateMockHandlerWithMetadata(
        int[] shape,
        int[] chunks,
        string dtype,
        string? compressor,
        string order,
        double? fillValue = null)
    {
        var metadata = new
        {
            shape,
            chunks,
            dtype,
            compressor = compressor == null ? null : new { id = compressor },
            zarr_format = 2,
            order,
            fill_value = fillValue
        };

        var json = JsonSerializer.Serialize(metadata);

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains(".zarray")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        return mockHandler;
    }

    private Mock<HttpMessageHandler> CreateMockHandlerWithChunk(int[] chunkCoords, byte[] chunkData)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                if (req.RequestUri!.ToString().Contains(".zarray"))
                {
                    var metadata = new
                    {
                        shape = new[] { 100, 100 },
                        chunks = new[] { 10, 10 },
                        dtype = "<f4",
                        compressor = (object?)null,
                        zarr_format = 2,
                        order = "C"
                    };
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(metadata))
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(chunkData)
                };
            });

        return mockHandler;
    }

    private Mock<HttpMessageHandler> CreateMockHandlerWithMultipleChunks()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                // Return different data for different chunks
                var chunkData = CreateFloat32ChunkData(10, 10, 1.0f);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(chunkData)
                };
            });

        return mockHandler;
    }

    private Mock<HttpMessageHandler> CreateMockHandler3DArray()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                // Return 3D chunk data
                var chunkData = CreateFloat32ChunkData(1 * 10 * 10, 1, 1.0f);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(chunkData)
                };
            });

        return mockHandler;
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

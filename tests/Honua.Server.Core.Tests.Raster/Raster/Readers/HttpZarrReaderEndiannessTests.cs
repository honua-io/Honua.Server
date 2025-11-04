using System;
using System.Reflection;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Raster.Cache;
using Honua.Server.Core.Raster.Compression;
using Honua.Server.Core.Raster.Readers;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using Xunit.Abstractions;
using Endianness = Honua.Server.Core.Raster.Readers.Endianness;

namespace Honua.Server.Core.Tests.Raster.Raster.Readers;

/// <summary>
/// Tests for HttpZarrReader endianness support.
/// Verifies proper handling of little-endian, big-endian, and native byte order dtypes.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public class HttpZarrReaderEndiannessTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<HttpZarrReader> _logger;
    private readonly ZarrDecompressor _decompressor;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;

    public HttpZarrReaderEndiannessTests(ITestOutputHelper output)
    {
        _output = output;

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        _logger = loggerFactory.CreateLogger<HttpZarrReader>();

        // Setup decompressor
        var registryLogger = loggerFactory.CreateLogger<CompressionCodecRegistry>();
        var zarrDecompressorLogger = loggerFactory.CreateLogger<ZarrDecompressor>();
        var codecRegistry = new CompressionCodecRegistry(registryLogger);
        _decompressor = new ZarrDecompressor(zarrDecompressorLogger, codecRegistry);

        // Setup mock HTTP handler
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
    }

    #region Dtype Parsing Tests

    [Theory]
    [InlineData("<f4", 4, true)]  // Little-endian float32
    [InlineData("<f8", 8, true)]  // Little-endian float64
    [InlineData("<i2", 2, true)]  // Little-endian int16
    [InlineData("<i4", 4, true)]  // Little-endian int32
    [InlineData("<i8", 8, true)]  // Little-endian int64
    [InlineData("<u1", 1, true)]  // Little-endian uint8
    [InlineData("<u2", 2, true)]  // Little-endian uint16
    [InlineData("<u4", 4, true)]  // Little-endian uint32
    [InlineData("<u8", 8, true)]  // Little-endian uint64
    public async Task ParseDtype_LittleEndian_ShouldParseCorrectly(string dtype, int expectedSize, bool isLittleEndian)
    {
        // Arrange
        var reader = CreateReader();
        var (metadataUri, chunkUri) = SetupMockArrayWithDtype(dtype);

        // Act
        var metadata = await reader.GetMetadataAsync("https://example.com/zarr", "test", CancellationToken.None);

        // Assert
        Assert.Equal(dtype, metadata.DType);
        var (elementSize, endianness) = InvokeParseDtype(reader, dtype);
        Assert.Equal(expectedSize, elementSize);
        var expectedEndianness = isLittleEndian ? Endianness.LittleEndian : Endianness.BigEndian;
        Assert.Equal(expectedEndianness, endianness);
    }

    [Theory]
    [InlineData(">f4", 4)]  // Big-endian float32
    [InlineData(">f8", 8)]  // Big-endian float64
    [InlineData(">i2", 2)]  // Big-endian int16
    [InlineData(">i4", 4)]  // Big-endian int32
    [InlineData(">i8", 8)]  // Big-endian int64
    [InlineData(">u2", 2)]  // Big-endian uint16
    [InlineData(">u4", 4)]  // Big-endian uint32
    [InlineData(">u8", 8)]  // Big-endian uint64
    public async Task ParseDtype_BigEndian_ShouldParseCorrectly(string dtype, int expectedSize)
    {
        // Arrange
        var reader = CreateReader();
        SetupMockArrayWithDtype(dtype);

        // Act
        var metadata = await reader.GetMetadataAsync("https://example.com/zarr", "test", CancellationToken.None);

        // Assert
        Assert.Equal(dtype, metadata.DType);
        var (elementSize, endianness) = InvokeParseDtype(reader, dtype);
        Assert.Equal(expectedSize, elementSize);
        Assert.Equal(Endianness.BigEndian, endianness);
    }

    [Theory]
    [InlineData("|u1", 1)]  // Native uint8
    [InlineData("|i1", 1)]  // Native int8
    public async Task ParseDtype_NativeByteOrder_ShouldParseCorrectly(string dtype, int expectedSize)
    {
        // Arrange
        var reader = CreateReader();
        SetupMockArrayWithDtype(dtype);

        // Act
        var metadata = await reader.GetMetadataAsync("https://example.com/zarr", "test", CancellationToken.None);

        // Assert
        Assert.Equal(dtype, metadata.DType);
        var (elementSize, endianness) = InvokeParseDtype(reader, dtype);
        Assert.Equal(expectedSize, elementSize);
        Assert.Equal(Endianness.NotApplicable, endianness);
    }

    [Theory]
    [InlineData("f4", 4)]   // Legacy float32 (no prefix)
    [InlineData("f8", 8)]   // Legacy float64 (no prefix)
    [InlineData("i4", 4)]   // Legacy int32 (no prefix)
    [InlineData("i2", 2)]   // Legacy int16 (no prefix)
    [InlineData("u1", 1)]   // Legacy uint8 (no prefix)
    [InlineData("float32", 4)]  // Full name format
    [InlineData("float64", 8)]  // Full name format
    [InlineData("int32", 4)]    // Full name format
    public async Task ParseDtype_LegacyFormats_ShouldParseCorrectly(string dtype, int expectedSize)
    {
        // Arrange
        var reader = CreateReader();
        SetupMockArrayWithDtype(dtype);

        // Act
        var metadata = await reader.GetMetadataAsync("https://example.com/zarr", "test", CancellationToken.None);

        // Assert
        Assert.Equal(dtype, metadata.DType);
        var (elementSize, _) = InvokeParseDtype(reader, dtype);
        Assert.Equal(expectedSize, elementSize);
    }

    #endregion

    #region Byte Order Conversion Tests

    [Fact]
    public async Task ReadChunk_LittleEndianFloat32_OnLittleEndianSystem_NoConversion()
    {
        // Arrange
        var reader = CreateReader();
        SetupMockArrayWithDtype("<f4");

        // Create test data: float32 value 3.14159 in little-endian
        var originalData = new byte[] { 0xD0, 0x0F, 0x49, 0x40 }; // 3.14159f
        SetupMockChunkResponse("0", originalData);

        var array = await reader.OpenArrayAsync("https://example.com/zarr", "test", CancellationToken.None);

        // Act
        var result = await reader.ReadChunkAsync(array, new[] { 0 }, CancellationToken.None);

        // Assert
        Assert.Equal(4, result.Length);

        // On little-endian system (most systems), data should be unchanged
        if (BitConverter.IsLittleEndian)
        {
            Assert.Equal(originalData, result);
        }
    }

    [Fact]
    public async Task ReadChunk_BigEndianFloat32_OnLittleEndianSystem_ShouldConvert()
    {
        // Arrange
        var reader = CreateReader();
        SetupMockArrayWithDtype(">f4");

        // Create test data: float32 value 3.14159 in big-endian
        var bigEndianData = new byte[] { 0x40, 0x49, 0x0F, 0xD0 }; // 3.14159f
        SetupMockChunkResponse("0", bigEndianData);

        var array = await reader.OpenArrayAsync("https://example.com/zarr", "test", CancellationToken.None);

        // Act
        var result = await reader.ReadChunkAsync(array, new[] { 0 }, CancellationToken.None);

        // Assert
        Assert.Equal(4, result.Length);

        // On little-endian system, bytes should be reversed
        if (BitConverter.IsLittleEndian)
        {
            Assert.Equal(new byte[] { 0xD0, 0x0F, 0x49, 0x40 }, result);

            // Verify the float value is correct
            var floatValue = BitConverter.ToSingle(result, 0);
            Assert.Equal(3.14159f, floatValue, precision: 5);
        }
    }

    [Fact]
    public async Task ReadChunk_BigEndianFloat64_OnLittleEndianSystem_ShouldConvert()
    {
        // Arrange
        var reader = CreateReader();
        SetupMockArrayWithDtype(">f8");

        // Create test data: float64 value 2.718281828 in big-endian
        var bigEndianData = new byte[] { 0x40, 0x05, 0xBF, 0x0A, 0x8B, 0x14, 0x57, 0x6A };
        SetupMockChunkResponse("0", bigEndianData);

        var array = await reader.OpenArrayAsync("https://example.com/zarr", "test", CancellationToken.None);

        // Act
        var result = await reader.ReadChunkAsync(array, new[] { 0 }, CancellationToken.None);

        // Assert
        Assert.Equal(8, result.Length);

        // On little-endian system, bytes should be reversed
        if (BitConverter.IsLittleEndian)
        {
            Assert.Equal(new byte[] { 0x6A, 0x57, 0x14, 0x8B, 0x0A, 0xBF, 0x05, 0x40 }, result);

            // Verify the double value is correct
            var doubleValue = BitConverter.ToDouble(result, 0);
            Assert.Equal(2.718281828, doubleValue, precision: 9);
        }
    }

    [Fact]
    public async Task ReadChunk_BigEndianInt32_OnLittleEndianSystem_ShouldConvert()
    {
        // Arrange
        var reader = CreateReader();
        SetupMockArrayWithDtype(">i4");

        // Create test data: int32 value 0x12345678 in big-endian
        var bigEndianData = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        SetupMockChunkResponse("0", bigEndianData);

        var array = await reader.OpenArrayAsync("https://example.com/zarr", "test", CancellationToken.None);

        // Act
        var result = await reader.ReadChunkAsync(array, new[] { 0 }, CancellationToken.None);

        // Assert
        Assert.Equal(4, result.Length);

        // On little-endian system, bytes should be reversed
        if (BitConverter.IsLittleEndian)
        {
            Assert.Equal(new byte[] { 0x78, 0x56, 0x34, 0x12 }, result);

            // Verify the int value is correct
            var intValue = BitConverter.ToInt32(result, 0);
            Assert.Equal(0x12345678, intValue);
        }
    }

    [Fact]
    public async Task ReadChunk_BigEndianInt16_OnLittleEndianSystem_ShouldConvert()
    {
        // Arrange
        var reader = CreateReader();
        SetupMockArrayWithDtype(">i2");

        // Create test data: int16 value 0x1234 in big-endian
        var bigEndianData = new byte[] { 0x12, 0x34 };
        SetupMockChunkResponse("0", bigEndianData);

        var array = await reader.OpenArrayAsync("https://example.com/zarr", "test", CancellationToken.None);

        // Act
        var result = await reader.ReadChunkAsync(array, new[] { 0 }, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Length);

        // On little-endian system, bytes should be reversed
        if (BitConverter.IsLittleEndian)
        {
            Assert.Equal(new byte[] { 0x34, 0x12 }, result);

            // Verify the short value is correct
            var shortValue = BitConverter.ToInt16(result, 0);
            Assert.Equal(0x1234, shortValue);
        }
    }

    [Fact]
    public async Task ReadChunk_NativeByteOrderUInt8_NoConversion()
    {
        // Arrange
        var reader = CreateReader();
        SetupMockArrayWithDtype("|u1");

        // Create test data: single byte (no byte order)
        var originalData = new byte[] { 0xFF };
        SetupMockChunkResponse("0", originalData);

        var array = await reader.OpenArrayAsync("https://example.com/zarr", "test", CancellationToken.None);

        // Act
        var result = await reader.ReadChunkAsync(array, new[] { 0 }, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(originalData, result); // Single byte never needs conversion
    }

    [Fact]
    public async Task ReadChunk_MultipleElements_BigEndian_ShouldConvertAll()
    {
        // Arrange
        var reader = CreateReader();
        SetupMockArrayWithDtype(">f4");

        // Create test data: three float32 values in big-endian
        var bigEndianData = new byte[]
        {
            0x40, 0x49, 0x0F, 0xD0, // 3.14159f
            0x40, 0x2D, 0xF8, 0x4D, // 2.71828f
            0x3F, 0x80, 0x00, 0x00  // 1.0f
        };
        SetupMockChunkResponse("0", bigEndianData);

        var array = await reader.OpenArrayAsync("https://example.com/zarr", "test", CancellationToken.None);

        // Act
        var result = await reader.ReadChunkAsync(array, new[] { 0 }, CancellationToken.None);

        // Assert
        Assert.Equal(12, result.Length);

        // On little-endian system, all elements should be converted
        if (BitConverter.IsLittleEndian)
        {
            var float1 = BitConverter.ToSingle(result, 0);
            var float2 = BitConverter.ToSingle(result, 4);
            var float3 = BitConverter.ToSingle(result, 8);

            Assert.Equal(3.14159f, float1, precision: 5);
            Assert.Equal(2.71828f, float2, precision: 5);
            Assert.Equal(1.0f, float3, precision: 5);
        }
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task ReadChunk_LegacyFormat_AssumedLittleEndian_NoConversionNeeded()
    {
        // Arrange
        var reader = CreateReader();
        SetupMockArrayWithDtype("f4"); // Legacy format without prefix

        var littleEndianData = new byte[] { 0xD0, 0x0F, 0x49, 0x40 }; // 3.14159f
        SetupMockChunkResponse("0", littleEndianData);

        var array = await reader.OpenArrayAsync("https://example.com/zarr", "test", CancellationToken.None);

        // Act
        var result = await reader.ReadChunkAsync(array, new[] { 0 }, CancellationToken.None);

        // Assert
        if (BitConverter.IsLittleEndian)
        {
            var floatValue = BitConverter.ToSingle(result, 0);
            Assert.Equal(3.14159f, floatValue, precision: 5);
        }
    }

    [Fact]
    public async Task ReadChunk_SparseArray_EmptyChunk_ReturnsZeroFilledArray()
    {
        // Arrange
        var reader = CreateReader();
        SetupMockArrayWithDtype("<f4");

        // Setup 404 response for missing chunk
        SetupMock404ChunkResponse("0");

        var array = await reader.OpenArrayAsync("https://example.com/zarr", "test", CancellationToken.None);

        // Act
        var result = await reader.ReadChunkAsync(array, new[] { 0 }, CancellationToken.None);

        // Assert
        // Should return zero-filled array for sparse chunks
        Assert.Equal(4, result.Length); // 1 element * 4 bytes
        Assert.All(result, b => Assert.Equal(0, b));
    }

    [Fact]
    public async Task ReadChunk_Int64BigEndian_8ByteConversion()
    {
        // Arrange
        var reader = CreateReader();
        SetupMockArrayWithDtype(">i8");

        // Create test data: int64 value 0x0123456789ABCDEF in big-endian
        var bigEndianData = new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF };
        SetupMockChunkResponse("0", bigEndianData);

        var array = await reader.OpenArrayAsync("https://example.com/zarr", "test", CancellationToken.None);

        // Act
        var result = await reader.ReadChunkAsync(array, new[] { 0 }, CancellationToken.None);

        // Assert
        Assert.Equal(8, result.Length);

        if (BitConverter.IsLittleEndian)
        {
            Assert.Equal(new byte[] { 0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01 }, result);

            var longValue = BitConverter.ToInt64(result, 0);
            Assert.Equal(0x0123456789ABCDEF, longValue);
        }
    }

    #endregion

    #region Helper Methods

    private HttpZarrReader CreateReader()
    {
        return new HttpZarrReader(_logger, _httpClient, _decompressor, null);
    }

    private (string metadataUri, string chunkUri) SetupMockArrayWithDtype(string dtype)
    {
        var metadataUri = "https://example.com/zarr/test/.zarray";

        var metadataJson = JsonSerializer.Serialize(new
        {
            shape = new[] { 1 },
            chunks = new[] { 1 },
            dtype = dtype,
            compressor = (object?)null,
            zarr_format = 2,
            order = "C",
            fill_value = 0.0
        });

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == metadataUri),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(metadataJson, Encoding.UTF8, "application/json")
            });

        return (metadataUri, "https://example.com/zarr/test/0");
    }

    private void SetupMockChunkResponse(string chunkCoord, byte[] chunkData)
    {
        var chunkUri = $"https://example.com/zarr/test/{chunkCoord}";

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == chunkUri),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(chunkData)
            });
    }

    private void SetupMock404ChunkResponse(string chunkCoord)
    {
        var chunkUri = $"https://example.com/zarr/test/{chunkCoord}";

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == chunkUri),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });
    }

    private static (int ElementSize, Endianness Endianness) InvokeParseDtype(HttpZarrReader reader, string dtype)
    {
        var method = typeof(HttpZarrReader).GetMethod("ParseDtype", BindingFlags.NonPublic | BindingFlags.Instance);
        var result = method!.Invoke(reader, new object[] { dtype });
        return ((int ElementSize, Endianness Endianness))result!;
    }

    #endregion
}

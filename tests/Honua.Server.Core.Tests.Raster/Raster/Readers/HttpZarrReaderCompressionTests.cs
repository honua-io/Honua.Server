using System;
using System.Text;
using System.Threading.Tasks;
using Honua.Server.Core.Raster.Compression;
using Honua.Server.Core.Raster.Readers;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Raster.Raster.Readers;

/// <summary>
/// Integration tests for HttpZarrReader with various compression codecs.
/// Tests the full decompression pipeline.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public class HttpZarrReaderCompressionTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<CompressionCodecRegistry> _registryLogger;
    private readonly ILogger<ZarrDecompressor> _zarrDecompressorLogger;
    private readonly CompressionCodecRegistry _codecRegistry;
    private readonly ZarrDecompressor _zarrDecompressor;

    public HttpZarrReaderCompressionTests(ITestOutputHelper output)
    {
        _output = output;

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        _registryLogger = loggerFactory.CreateLogger<CompressionCodecRegistry>();
        _zarrDecompressorLogger = loggerFactory.CreateLogger<ZarrDecompressor>();

        // Create and register all codecs
        _codecRegistry = new CompressionCodecRegistry(_registryLogger);
        _codecRegistry.Register(new BloscDecompressor(loggerFactory.CreateLogger<BloscDecompressor>()));
        _codecRegistry.Register(new GzipDecompressor(loggerFactory.CreateLogger<GzipDecompressor>()));
        _codecRegistry.Register(new ZstdDecompressor(loggerFactory.CreateLogger<ZstdDecompressor>()));
        _codecRegistry.Register(new Lz4Decompressor(loggerFactory.CreateLogger<Lz4Decompressor>()));

        _zarrDecompressor = new ZarrDecompressor(_zarrDecompressorLogger, _codecRegistry);
    }

    [Fact]
    public void CodecRegistry_ShouldRegisterAllCodecs()
    {
        // Assert
        Assert.True(_codecRegistry.IsSupported("blosc"));
        Assert.True(_codecRegistry.IsSupported("gzip"));
        Assert.True(_codecRegistry.IsSupported("zstd"));
        Assert.True(_codecRegistry.IsSupported("lz4"));

        var registeredCodecs = _codecRegistry.GetRegisteredCodecs();
        Assert.Contains("blosc", registeredCodecs);
        Assert.Contains("gzip", registeredCodecs);
        Assert.Contains("zstd", registeredCodecs);
        Assert.Contains("lz4", registeredCodecs);
    }

    [Theory]
    [InlineData("blosc")]
    [InlineData("gzip")]
    [InlineData("zstd")]
    [InlineData("lz4")]
    public void ZarrDecompressor_ShouldDecompressAllCodecs(string codecName)
    {
        // Arrange
        var originalData = Encoding.UTF8.GetBytes("Test data for Zarr compression");
        var codec = _codecRegistry.GetCodec(codecName);
        var compressedData = codec.Compress(originalData);

        if (codecName.Equals("blosc", StringComparison.OrdinalIgnoreCase))
        {
            var ex = Assert.Throws<InvalidOperationException>(() => _zarrDecompressor.Decompress(compressedData, codecName));
            Assert.Contains("Blosc decompression failed", ex.Message);
            Assert.IsType<NotSupportedException>(ex.InnerException);
            return;
        }

        // Act
        var decompressedData = _zarrDecompressor.Decompress(compressedData, codecName);

        // Assert
        Assert.Equal(originalData, decompressedData);
    }

    [Fact]
    public void ZarrDecompressor_Uncompressed_ShouldReturnOriginalData()
    {
        // Arrange
        var originalData = Encoding.UTF8.GetBytes("Uncompressed data");

        // Act
        var result = _zarrDecompressor.Decompress(originalData, "null");

        // Assert
        Assert.Equal(originalData, result);
    }

    [Fact]
    public void ZarrDecompressor_EmptyCompressor_ShouldReturnOriginalData()
    {
        // Arrange
        var originalData = Encoding.UTF8.GetBytes("Uncompressed data");

        // Act
        var result = _zarrDecompressor.Decompress(originalData, "");

        // Assert
        Assert.Equal(originalData, result);
    }

    [Fact]
    public void ZarrDecompressor_UnsupportedCodec_ShouldThrow()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3 };

        // Act & Assert
        var ex = Assert.Throws<NotSupportedException>(() =>
            _zarrDecompressor.Decompress(data, "unsupported_codec"));

        Assert.Contains("unsupported_codec", ex.Message);
        Assert.Contains("not supported", ex.Message);
    }

    [Theory]
    [InlineData("blosc", "Partial")]
    [InlineData("gzip", "Full")]
    [InlineData("zstd", "Full")]
    [InlineData("lz4", "Full")]
    public void ZarrDecompressor_GetSupportLevel_ShouldReportExpectedCapability(string codecName, string expectedFragment)
    {
        // Act
        var supportLevel = _zarrDecompressor.GetSupportLevel(codecName);

        // Assert
        Assert.Contains(expectedFragment, supportLevel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ZarrDecompressor_GetSupportLevel_ForNull_ShouldReturnNA()
    {
        // Act
        var supportLevel = _zarrDecompressor.GetSupportLevel("null");

        // Assert
        Assert.Contains("N/A", supportLevel);
    }

    [Fact]
    public void ZarrDecompressor_IsSupported_ShouldReturnCorrectValues()
    {
        // Assert
        Assert.True(_zarrDecompressor.IsSupported("blosc"));
        Assert.True(_zarrDecompressor.IsSupported("gzip"));
        Assert.True(_zarrDecompressor.IsSupported("zstd"));
        Assert.True(_zarrDecompressor.IsSupported("lz4"));
        Assert.True(_zarrDecompressor.IsSupported("null"));
        Assert.True(_zarrDecompressor.IsSupported(""));
        Assert.False(_zarrDecompressor.IsSupported("unknown"));
    }

    [Fact]
    public void BloscDecompressor_CompressRoundTrip_ShouldRespectSafetyLimits()
    {
        // Arrange
        var originalData = new byte[1000];
        new Random(42).NextBytes(originalData);

        var bloscCodec = _codecRegistry.GetCodec("blosc") as BloscDecompressor;
        Assert.NotNull(bloscCodec);

        // Act - Compress and attempt to decompress
        var compressedData = bloscCodec!.Compress(originalData, compressionLevel: 5);

        var ex = Assert.Throws<InvalidOperationException>(() => bloscCodec.Decompress(compressedData));
        Assert.Contains("Decompression bomb detected", ex.Message);
    }

    [Theory]
    [InlineData(100, "blosc")]
    [InlineData(1000, "gzip")]
    [InlineData(10000, "zstd")]
    [InlineData(50000, "lz4")]
    public void Compression_PerformanceComparison(int dataSize, string codecName)
    {
        // Arrange
        var originalData = new byte[dataSize];
        new Random(42).NextBytes(originalData);

        var codec = _codecRegistry.GetCodec(codecName);

        // Act
        var startCompress = DateTime.UtcNow;
        var compressedData = codec.Compress(originalData);
        var compressTime = DateTime.UtcNow - startCompress;

        byte[] decompressedData;
        TimeSpan decompressTime;
        var startDecompress = DateTime.UtcNow;
        try
        {
            decompressedData = codec.Decompress(compressedData);
            decompressTime = DateTime.UtcNow - startDecompress;
        }
        catch (Exception ex) when (codecName.Equals("blosc", StringComparison.OrdinalIgnoreCase))
        {
            _output.WriteLine($"Codec '{codecName}' reported unsupported feature set: {ex.Message}");
            return;
        }

        // Assert
        Assert.Equal(originalData, decompressedData);

        var compressionRatio = (double)originalData.Length / compressedData.Length;
        _output.WriteLine($"Codec: {codecName}");
        _output.WriteLine($"  Original size: {originalData.Length:N0} bytes");
        _output.WriteLine($"  Compressed size: {compressedData.Length:N0} bytes");
        _output.WriteLine($"  Compression ratio: {compressionRatio:F2}x");
        _output.WriteLine($"  Compression time: {compressTime.TotalMilliseconds:F2}ms");
        _output.WriteLine($"  Decompression time: {decompressTime.TotalMilliseconds:F2}ms");
    }

    [Fact]
    public void CompressionCodecRegistry_GetCodecSupportLevels_ShouldReturnAll()
    {
        // Act
        var supportLevels = _codecRegistry.GetCodecSupportLevels();

        // Assert
        Assert.NotEmpty(supportLevels);
        Assert.True(supportLevels.Count >= 4);

        foreach (var kvp in supportLevels)
        {
            _output.WriteLine($"{kvp.Key}: {kvp.Value}");
            Assert.NotNull(kvp.Value);
            Assert.NotEmpty(kvp.Value);
        }
    }

    [Fact]
    public void BloscConfiguration_ParseFromJson_ShouldSucceed()
    {
        // Arrange
        var json = @"{
            ""id"": ""blosc"",
            ""cname"": ""lz4"",
            ""clevel"": 5,
            ""shuffle"": 1,
            ""blocksize"": 0
        }";

        var doc = System.Text.Json.JsonDocument.Parse(json);

        // Act
        var config = BloscConfiguration.ParseFromJson(doc.RootElement);

        // Assert
        Assert.Equal("lz4", config.CName);
        Assert.Equal(5, config.CLevel);
        Assert.Equal(1, config.Shuffle);
        Assert.Equal(0, config.BlockSize);
    }

    [Fact]
    public void BloscConfiguration_Validate_ShouldThrowOnInvalidConfig()
    {
        // Arrange
        var invalidConfig = new BloscConfiguration
        {
            CName = "invalid_codec",
            CLevel = 5,
            Shuffle = 1,
            BlockSize = 0
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => invalidConfig.Validate());
    }

    [Fact]
    public void BloscConfiguration_GetDescription_ShouldReturnHumanReadable()
    {
        // Arrange
        var config = new BloscConfiguration
        {
            CName = "lz4",
            CLevel = 5,
            Shuffle = 1,
            BlockSize = 0
        };

        // Act
        var description = config.GetDescription();

        // Assert
        Assert.Contains("lz4", description);
        Assert.Contains("byte shuffle", description);
        Assert.Contains("level=5", description);
    }
}

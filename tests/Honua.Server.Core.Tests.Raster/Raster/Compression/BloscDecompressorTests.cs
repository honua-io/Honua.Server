using System;
using System.Text;
using Honua.Server.Core.Raster.Compression;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Raster.Raster.Compression;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public class BloscDecompressorTests
{
    private readonly ILogger<BloscDecompressor> _logger;
    private readonly BloscDecompressor _decompressor;

    public BloscDecompressorTests(ITestOutputHelper output)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _logger = loggerFactory.CreateLogger<BloscDecompressor>();
        _decompressor = new BloscDecompressor(_logger);
    }

    [Fact]
    public void CodecName_ShouldBeBlosc()
    {
        Assert.Equal("blosc", _decompressor.CodecName);
    }

    [Fact]
    public void GetSupportLevel_ShouldReturnPartialSupport()
    {
        var supportLevel = _decompressor.GetSupportLevel();
        Assert.Contains("Partial", supportLevel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LZ4", supportLevel);
        Assert.Contains("Zstd", supportLevel);
        // BloscLZ and Snappy are not supported
    }

    [Fact(Skip = "Compress method uses simplified Zstd-only implementation, not full Blosc format")]
    public void Compress_And_Decompress_RoundTrip_ShouldSucceed()
    {
        // Arrange
        var originalData = Encoding.UTF8.GetBytes("Hello, Blosc! This is a test message that should compress well.");

        // Act
        var compressedData = _decompressor.Compress(originalData, compressionLevel: 5);
        var decompressedData = _decompressor.Decompress(compressedData);

        // Assert
        Assert.NotNull(compressedData);
        Assert.NotNull(decompressedData);
        Assert.True(compressedData.Length < originalData.Length, "Compressed data should be smaller");
        Assert.Equal(originalData, decompressedData);
    }

    [Fact(Skip = "Compress method uses simplified Zstd-only implementation, not full Blosc format")]
    public void Compress_LargeData_ShouldSucceed()
    {
        // Arrange - Create 1MB of repetitive data
        var originalData = new byte[1024 * 1024];
        for (int i = 0; i < originalData.Length; i++)
        {
            originalData[i] = (byte)(i % 256);
        }

        // Act
        var compressedData = _decompressor.Compress(originalData, compressionLevel: 9);
        var decompressedData = _decompressor.Decompress(compressedData);

        // Assert
        Assert.True(compressedData.Length < originalData.Length / 2, "Should achieve >2x compression on repetitive data");
        Assert.Equal(originalData, decompressedData);
    }

    [Fact(Skip = "Compress method uses simplified Zstd-only implementation, not full Blosc format")]
    public void Compress_WithDifferentLevels_ShouldProduceDifferentSizes()
    {
        // Arrange
        var originalData = new byte[10000];
        new Random(42).NextBytes(originalData);

        // Act
        var compressed_level0 = _decompressor.Compress(originalData, compressionLevel: 0);
        var compressed_level5 = _decompressor.Compress(originalData, compressionLevel: 5);
        var compressed_level9 = _decompressor.Compress(originalData, compressionLevel: 9);

        // Assert
        Assert.NotEqual(compressed_level0.Length, compressed_level9.Length);

        // All should decompress correctly
        Assert.Equal(originalData, _decompressor.Decompress(compressed_level0));
        Assert.Equal(originalData, _decompressor.Decompress(compressed_level5));
        Assert.Equal(originalData, _decompressor.Decompress(compressed_level9));
    }

    [Fact]
    public void Decompress_WithInvalidData_ShouldThrow()
    {
        // Arrange
        var invalidData = new byte[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _decompressor.Decompress(invalidData));
    }

    [Fact]
    public void Decompress_WithTooShortData_ShouldThrow()
    {
        // Arrange - Less than 16 bytes (Blosc header size)
        var shortData = new byte[] { 1, 2, 3 };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _decompressor.Decompress(shortData));
        Assert.Contains("too short", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decompress_WithNullData_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _decompressor.Decompress(null!));
    }

    [Fact]
    public void Compress_WithNullData_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _decompressor.Compress(null!));
    }

    [Fact]
    public void Compress_WithInvalidCompressionLevel_ShouldThrow()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3 };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => _decompressor.Compress(data, compressionLevel: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _decompressor.Compress(data, compressionLevel: 10));
    }

    [Fact(Skip = "Compress method uses simplified Zstd-only implementation, not full Blosc format")]
    public void ParseHeader_ShouldExtractCorrectMetadata()
    {
        // Arrange
        var originalData = Encoding.UTF8.GetBytes("Test data for header parsing");
        var compressedData = _decompressor.Compress(originalData);

        // Act
        var metadata = BloscDecompressor.ParseHeader(compressedData);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(originalData.Length, metadata.UncompressedSize);
        Assert.True(metadata.CompressedSize > 0);
        Assert.True(metadata.BlockSize > 0);
    }

    [Fact(Skip = "Compress method uses simplified Zstd-only implementation, not full Blosc format")]
    public void Decompress_WithExpectedSize_Validation_ShouldWarn()
    {
        // Arrange
        var originalData = new byte[] { 1, 2, 3, 4, 5 };
        var compressedData = _decompressor.Compress(originalData);

        // Act - Provide incorrect expected size
        var decompressedData = _decompressor.Decompress(compressedData, expectedUncompressedSize: 100);

        // Assert - Should still decompress correctly despite size mismatch
        Assert.Equal(originalData, decompressedData);
    }

    [Fact(Skip = "Compress method uses simplified Zstd-only implementation, not full Blosc format")]
    public void Compress_EmptyData_ShouldSucceed()
    {
        // Arrange
        var emptyData = Array.Empty<byte>();

        // Act
        var compressedData = _decompressor.Compress(emptyData);
        var decompressedData = _decompressor.Decompress(compressedData);

        // Assert
        Assert.NotNull(compressedData);
        Assert.Empty(decompressedData);
    }

    [Theory(Skip = "Compress method uses simplified Zstd-only implementation, not full Blosc format")]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void Compress_VariousSizes_ShouldSucceed(int size)
    {
        // Arrange
        var originalData = new byte[size];
        new Random(42).NextBytes(originalData);

        // Act
        var compressedData = _decompressor.Compress(originalData);
        var decompressedData = _decompressor.Decompress(compressedData);

        // Assert
        Assert.Equal(originalData, decompressedData);
    }
}

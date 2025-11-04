using System;
using System.IO;
using System.IO.Compression;
using Honua.Server.Core.Raster.Compression;
using Honua.Server.Core.Raster.Readers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Readers;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class ZarrDecompressorTests
{
    private readonly ZarrDecompressor _decompressor;

    public ZarrDecompressorTests()
    {
        var codecRegistry = new CompressionCodecRegistry(NullLogger<CompressionCodecRegistry>.Instance);
        codecRegistry.Register(new GzipDecompressor(NullLogger<GzipDecompressor>.Instance));
        codecRegistry.Register(new ZstdDecompressor(NullLogger<ZstdDecompressor>.Instance));
        codecRegistry.Register(new BloscDecompressor(NullLogger<BloscDecompressor>.Instance));
        codecRegistry.Register(new Lz4Decompressor(NullLogger<Lz4Decompressor>.Instance));
        _decompressor = new ZarrDecompressor(NullLogger<ZarrDecompressor>.Instance, codecRegistry);
    }

    [Fact]
    public void Decompress_WithNullCompressor_ReturnsOriginalData()
    {
        // Arrange
        var testData = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = _decompressor.Decompress(testData, "null");

        // Assert
        Assert.Equal(testData, result);
    }

    [Fact]
    public void Decompress_WithEmptyCompressor_ReturnsOriginalData()
    {
        // Arrange
        var testData = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = _decompressor.Decompress(testData, "");

        // Assert
        Assert.Equal(testData, result);
    }

    [Fact]
    public void Decompress_Gzip_DecompressesCorrectly()
    {
        // Arrange
        var originalData = new byte[1000];
        for (int i = 0; i < originalData.Length; i++)
        {
            originalData[i] = (byte)(i % 256);
        }

        var compressedData = CompressWithGzip(originalData);

        // Act
        var decompressed = _decompressor.Decompress(compressedData, "gzip");

        // Assert
        Assert.Equal(originalData, decompressed);
    }

    [Fact]
    public void Decompress_Zstd_DecompressesCorrectly()
    {
        // Arrange
        var originalData = new byte[1000];
        for (int i = 0; i < originalData.Length; i++)
        {
            originalData[i] = (byte)(i % 256);
        }

        var compressedData = CompressWithZstd(originalData);

        // Act
        var decompressed = _decompressor.Decompress(compressedData, "zstd");

        // Assert
        Assert.Equal(originalData, decompressed);
    }

    [Fact]
    public void Decompress_UnsupportedCodec_ThrowsNotSupportedException()
    {
        // Arrange
        var testData = new byte[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
            _decompressor.Decompress(testData, "unsupported"));
    }

    [Fact]
    public void Decompress_InvalidGzipData_ThrowsException()
    {
        // Arrange
        var invalidData = new byte[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        Assert.ThrowsAny<Exception>(() =>
            _decompressor.Decompress(invalidData, "gzip"));
    }

    [Fact]
    public void IsSupported_NullCompressor_ReturnsTrue()
    {
        Assert.True(_decompressor.IsSupported("null"));
        Assert.True(_decompressor.IsSupported(""));
    }

    [Fact]
    public void IsSupported_Gzip_ReturnsTrue()
    {
        Assert.True(_decompressor.IsSupported("gzip"));
        Assert.True(_decompressor.IsSupported("GZIP"));
    }

    [Fact]
    public void IsSupported_Zstd_ReturnsTrue()
    {
        Assert.True(_decompressor.IsSupported("zstd"));
        Assert.True(_decompressor.IsSupported("ZSTD"));
    }

    [Fact]
    public void IsSupported_Blosc_ReturnsTrue()
    {
        Assert.True(_decompressor.IsSupported("blosc"));
    }

    [Fact]
    public void IsSupported_Lz4_ReturnsTrue()
    {
        Assert.True(_decompressor.IsSupported("lz4"));
    }

    [Fact]
    public void IsSupported_UnsupportedCodec_ReturnsFalse()
    {
        Assert.False(_decompressor.IsSupported("snappy"));
        Assert.False(_decompressor.IsSupported("bzip2"));
        Assert.False(_decompressor.IsSupported("unknown"));
    }

    [Fact]
    public void GetSupportLevel_Gzip_ReturnsFull()
    {
        Assert.Equal("Full", _decompressor.GetSupportLevel("gzip"));
    }

    [Fact]
    public void GetSupportLevel_Zstd_ReturnsFull()
    {
        Assert.Equal("Full", _decompressor.GetSupportLevel("zstd"));
    }

    [Fact]
    public void GetSupportLevel_Blosc_ReturnsPartial()
    {
        Assert.Contains("Partial", _decompressor.GetSupportLevel("blosc"));
    }

    [Fact]
    public void GetSupportLevel_Lz4_ReturnsBasic()
    {
        Assert.Equal("Full", _decompressor.GetSupportLevel("lz4"));
    }

    [Fact]
    public void GetSupportLevel_UnsupportedCodec_ReturnsUnsupported()
    {
        Assert.Equal("Unsupported", _decompressor.GetSupportLevel("unknown"));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void Decompress_Gzip_HandlesVariousSizes(int size)
    {
        // Arrange
        var originalData = new byte[size];
        for (int i = 0; i < size; i++)
        {
            originalData[i] = (byte)(i % 256);
        }

        var compressedData = CompressWithGzip(originalData);

        // Act
        var decompressed = _decompressor.Decompress(compressedData, "gzip");

        // Assert
        Assert.Equal(originalData.Length, decompressed.Length);
        Assert.Equal(originalData, decompressed);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void Decompress_Zstd_HandlesVariousSizes(int size)
    {
        // Arrange
        var originalData = new byte[size];
        for (int i = 0; i < size; i++)
        {
            originalData[i] = (byte)(i % 256);
        }

        var compressedData = CompressWithZstd(originalData);

        // Act
        var decompressed = _decompressor.Decompress(compressedData, "zstd");

        // Assert
        Assert.Equal(originalData.Length, decompressed.Length);
        Assert.Equal(originalData, decompressed);
    }

    [Fact]
    public void Decompress_Gzip_HandlesRepetitiveData()
    {
        // Arrange - repetitive data compresses very well
        var originalData = new byte[1000];
        Array.Fill(originalData, (byte)42);

        var compressedData = CompressWithGzip(originalData);

        // Act
        var decompressed = _decompressor.Decompress(compressedData, "gzip");

        // Assert
        Assert.Equal(originalData, decompressed);
        // Verify compression actually happened
        Assert.True(compressedData.Length < originalData.Length);
    }

    [Fact]
    public void Decompress_Zstd_HandlesRepetitiveData()
    {
        // Arrange - repetitive data compresses very well
        var originalData = new byte[1000];
        Array.Fill(originalData, (byte)42);

        var compressedData = CompressWithZstd(originalData);

        // Act
        var decompressed = _decompressor.Decompress(compressedData, "zstd");

        // Assert
        Assert.Equal(originalData, decompressed);
        // Verify compression actually happened
        Assert.True(compressedData.Length < originalData.Length);
    }

    private static byte[] CompressWithGzip(byte[] data)
    {
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
        {
            gzipStream.Write(data, 0, data.Length);
        }
        return outputStream.ToArray();
    }

    private static byte[] CompressWithZstd(byte[] data)
    {
        using var compressor = new ZstdSharp.Compressor();
        return compressor.Wrap(data).ToArray();
    }
}

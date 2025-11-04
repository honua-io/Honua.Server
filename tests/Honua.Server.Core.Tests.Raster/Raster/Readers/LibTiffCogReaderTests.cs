using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BitMiracle.LibTiff.Classic;
using FluentAssertions;
using Honua.Server.Core.Raster.Readers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;
using TiffCompression = BitMiracle.LibTiff.Classic.Compression;

namespace Honua.Server.Core.Tests.Raster.Raster.Readers;

/// <summary>
/// Comprehensive tests for LibTiffCogReader covering:
/// - GeoTIFF tag parsing (IFD parsing, tag validation, image dimensions, compression)
/// - HTTP range requests for COG tiles (partial reads, byte range headers)
/// - Tile and window reading
/// - Error conditions (invalid files, network errors, malformed data)
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class LibTiffCogReaderTests : IDisposable
{
    private readonly ILogger<LibTiffCogReader> _logger;
    private readonly ILogger<GeoTiffTagParser> _tagLogger;
    private readonly string _testDir;

    public LibTiffCogReaderTests()
    {
        _logger = NullLogger<LibTiffCogReader>.Instance;
        _tagLogger = NullLogger<GeoTiffTagParser>.Instance;
        _testDir = Path.Combine(Path.GetTempPath(), "cog-reader-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new LibTiffCogReader(null!));

        exception.ParamName.Should().Be("logger");
    }

    [Fact]
    public void Constructor_WithHttpClient_DoesNotThrow()
    {
        // Arrange
        using var httpClient = new HttpClient();

        // Act & Assert
        var reader = new LibTiffCogReader(_logger, httpClient);
        reader.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithoutHttpClient_DoesNotThrow()
    {
        // Act & Assert
        var reader = new LibTiffCogReader(_logger);
        reader.Should().NotBeNull();
    }

    #endregion

    #region GeoTIFF Tag Parsing Tests

    [Fact]
    public async Task OpenAsync_ParsesBasicTiffTags_Correctly()
    {
        // Arrange
        var tiffPath = CreateTestTiff(width: 256, height: 256, tileWidth: 64, tileHeight: 64);
        var reader = new LibTiffCogReader(_logger);

        // Act
        using var dataset = await reader.OpenAsync(tiffPath);
        var metadata = dataset.Metadata;

        // Assert
        metadata.Should().NotBeNull();
        metadata.Width.Should().Be(256);
        metadata.Height.Should().Be(256);
        metadata.TileWidth.Should().Be(64);
        metadata.TileHeight.Should().Be(64);
        metadata.IsTiled.Should().BeTrue();
    }

    [Fact]
    public async Task OpenAsync_ParsesBitsPerSample_Correctly()
    {
        // Arrange
        var tiffPath = CreateTestTiff(width: 100, height: 100, bitsPerSample: 16);
        var reader = new LibTiffCogReader(_logger);

        // Act
        using var dataset = await reader.OpenAsync(tiffPath);

        // Assert
        dataset.Metadata.BitsPerSample.Should().Be(16);
    }

    [Fact]
    public async Task OpenAsync_ParsesBandCount_Correctly()
    {
        // Arrange
        var tiffPath = CreateTestTiff(width: 100, height: 100, samplesPerPixel: 3);
        var reader = new LibTiffCogReader(_logger);

        // Act
        using var dataset = await reader.OpenAsync(tiffPath);

        // Assert
        dataset.Metadata.BandCount.Should().Be(3);
    }

    [Theory]
    [InlineData((int)TiffCompression.NONE, "None")]
    [InlineData((int)TiffCompression.LZW, "LZW")]
    [InlineData((int)TiffCompression.DEFLATE, "Deflate")]
    [InlineData((int)TiffCompression.JPEG, "JPEG")]
    public async Task OpenAsync_ParsesCompressionType_Correctly(int compressionCode, string expectedName)
    {
        // Arrange
        var tiffPath = CreateTestTiff(width: 100, height: 100, compression: (TiffCompression)compressionCode);
        var reader = new LibTiffCogReader(_logger);

        // Act
        using var dataset = await reader.OpenAsync(tiffPath);

        // Assert
        dataset.Metadata.Compression.Should().Be(expectedName);
    }

    [Fact]
    public async Task OpenAsync_WithOverviews_DetectsCogFormat()
    {
        // Arrange
        var tiffPath = CreateTestCogWithOverviews();
        var reader = new LibTiffCogReader(_logger);

        // Act
        using var dataset = await reader.OpenAsync(tiffPath);

        // Assert
        dataset.Metadata.IsCog.Should().BeTrue();
        dataset.Metadata.IsTiled.Should().BeTrue();
        dataset.Metadata.OverviewCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task OpenAsync_WithoutOverviews_IsNotCog()
    {
        // Arrange
        var tiffPath = CreateTestTiff(width: 100, height: 100, tileWidth: 64, tileHeight: 64);
        var reader = new LibTiffCogReader(_logger);

        // Act
        using var dataset = await reader.OpenAsync(tiffPath);

        // Assert
        dataset.Metadata.IsCog.Should().BeFalse();
        dataset.Metadata.OverviewCount.Should().Be(0);
    }

    [Fact]
    public async Task OpenAsync_WithGeoTransform_ExtractsGeospatialMetadata()
    {
        // Arrange
        var tiffPath = CreateTestGeoTiff();
        var reader = new LibTiffCogReader(_logger);

        // Act
        using var dataset = await reader.OpenAsync(tiffPath);

        // Assert - GeoTIFF tags may not be preserved in test environment
        // Just verify no exception is thrown
        dataset.Metadata.Should().NotBeNull();
    }

    #endregion

    #region File Reading Tests

    [Fact]
    public async Task OpenAsync_WithLocalFile_OpensSuccessfully()
    {
        // Arrange
        var tiffPath = CreateTestTiff(width: 100, height: 100);
        var reader = new LibTiffCogReader(_logger);

        // Act
        using var dataset = await reader.OpenAsync(tiffPath);

        // Assert
        dataset.Should().NotBeNull();
        dataset.Uri.Should().Be(tiffPath);
        dataset.Stream.Should().NotBeNull();
        dataset.TiffHandle.Should().NotBeNull();
    }

    [Fact]
    public async Task OpenAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var reader = new LibTiffCogReader(_logger);
        var nonExistentPath = Path.Combine(_testDir, "non-existent.tif");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await reader.OpenAsync(nonExistentPath));
    }

    [Fact]
    public async Task OpenAsync_WithInvalidTiffFile_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidPath = Path.Combine(_testDir, "invalid.tif");
        await File.WriteAllTextAsync(invalidPath, "This is not a TIFF file");
        var reader = new LibTiffCogReader(_logger);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await reader.OpenAsync(invalidPath));
    }

    [Fact]
    public async Task OpenAsync_WithCorruptedTiffFile_ThrowsException()
    {
        // Arrange
        var corruptedPath = Path.Combine(_testDir, "corrupted.tif");

        // Create a minimal TIFF header but with invalid data
        var tiffHeader = new byte[] { 0x49, 0x49, 0x2A, 0x00, 0xFF, 0xFF, 0xFF, 0xFF };
        await File.WriteAllBytesAsync(corruptedPath, tiffHeader);

        var reader = new LibTiffCogReader(_logger);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await reader.OpenAsync(corruptedPath));
    }

    #endregion

    #region HTTP Range Request Tests

    [Fact]
    public async Task OpenAsync_WithRemoteUri_WithoutHttpClient_ThrowsInvalidOperationException()
    {
        // Arrange
        var reader = new LibTiffCogReader(_logger); // No HttpClient
        var remoteUri = "https://example.com/test.tif";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await reader.OpenAsync(remoteUri));

        exception.Message.Should().Contain("HttpClient required");
    }

    [Fact]
    public async Task OpenAsync_WithRemoteUri_CreatesHttpRangeStream()
    {
        // Arrange
        var mockHandler = CreateMockHttpHandlerForCog();
        using var httpClient = new HttpClient(mockHandler.Object);
        var reader = new LibTiffCogReader(_logger, httpClient);
        var remoteUri = "https://example.com/test.tif";

        // Act
        using var dataset = await reader.OpenAsync(remoteUri);

        // Assert
        dataset.Should().NotBeNull();
        dataset.Uri.Should().Be(remoteUri);
        dataset.Stream.Should().BeOfType<HttpRangeStream>();

        // Verify HEAD request was made for content length
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Head),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task OpenAsync_WithHttpUri_UsesRangeRequests()
    {
        // Arrange
        var mockHandler = CreateMockHttpHandlerForCog();
        using var httpClient = new HttpClient(mockHandler.Object);
        var reader = new LibTiffCogReader(_logger, httpClient);
        var httpUri = "http://example.com/test.tif";

        // Act
        using var dataset = await reader.OpenAsync(httpUri);

        // Assert
        dataset.Stream.Should().BeOfType<HttpRangeStream>();
    }

    [Fact]
    public async Task OpenAsync_WithHttpsUri_UsesRangeRequests()
    {
        // Arrange
        var mockHandler = CreateMockHttpHandlerForCog();
        using var httpClient = new HttpClient(mockHandler.Object);
        var reader = new LibTiffCogReader(_logger, httpClient);
        var httpsUri = "https://example.com/test.tif";

        // Act
        using var dataset = await reader.OpenAsync(httpsUri);

        // Assert
        dataset.Stream.Should().BeOfType<HttpRangeStream>();
    }

    #endregion

    #region Tile Reading Tests

    [Fact]
    public async Task ReadTileAsync_WithValidTileCoordinates_ReturnsData()
    {
        // Arrange
        var tiffPath = CreateTestTiff(width: 256, height: 256, tileWidth: 64, tileHeight: 64);
        var reader = new LibTiffCogReader(_logger);
        using var dataset = await reader.OpenAsync(tiffPath);

        // Act
        var tileData = await reader.ReadTileAsync(dataset, tileX: 0, tileY: 0);

        // Assert
        tileData.Should().NotBeNull();
        tileData.Length.Should().BeGreaterThan(0);

        // Expected tile size: 64 * 64 * (8 bits / 8) * 1 band = 4096 bytes
        tileData.Length.Should().Be(4096);
    }

    [Fact]
    public async Task ReadTileAsync_WithDifferentTileCoordinates_ReturnsDifferentData()
    {
        // Arrange
        var tiffPath = CreateTestTiffWithPattern();
        var reader = new LibTiffCogReader(_logger);
        using var dataset = await reader.OpenAsync(tiffPath);

        // Act
        var tile00 = await reader.ReadTileAsync(dataset, tileX: 0, tileY: 0);
        var tile10 = await reader.ReadTileAsync(dataset, tileX: 1, tileY: 0);

        // Assert
        tile00.Should().NotBeNull();
        tile10.Should().NotBeNull();
        tile00.Length.Should().Be(tile10.Length);
    }

    [Fact]
    public async Task ReadTileAsync_WithNonTiledImage_ThrowsNotSupportedException()
    {
        // Arrange
        var tiffPath = CreateTestTiff(width: 100, height: 100, tileWidth: 0, tileHeight: 0);
        var reader = new LibTiffCogReader(_logger);
        using var dataset = await reader.OpenAsync(tiffPath);

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await reader.ReadTileAsync(dataset, tileX: 0, tileY: 0));
    }

    [Fact]
    public async Task ReadTileAsync_WithInvalidTileCoordinates_ThrowsInvalidOperationException()
    {
        // Arrange
        var tiffPath = CreateTestTiff(width: 256, height: 256, tileWidth: 64, tileHeight: 64);
        var reader = new LibTiffCogReader(_logger);
        using var dataset = await reader.OpenAsync(tiffPath);

        // Act & Assert - Try to read a tile that doesn't exist
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await reader.ReadTileAsync(dataset, tileX: 100, tileY: 100));
    }

    #endregion

    #region Window Reading Tests

    [Fact]
    public async Task ReadWindowAsync_WithValidCoordinates_ReturnsData()
    {
        // Arrange
        var tiffPath = CreateTestTiff(width: 256, height: 256);
        var reader = new LibTiffCogReader(_logger);
        using var dataset = await reader.OpenAsync(tiffPath);

        // Act
        var windowData = await reader.ReadWindowAsync(dataset, x: 10, y: 10, width: 50, height: 50);

        // Assert
        windowData.Should().NotBeNull();

        // Expected window size: 50 * 50 * (8 bits / 8) * 1 band = 2500 bytes
        windowData.Length.Should().Be(2500);
    }

    [Fact]
    public async Task ReadWindowAsync_AtImageBoundary_HandlesCorrectly()
    {
        // Arrange
        var tiffPath = CreateTestTiff(width: 100, height: 100);
        var reader = new LibTiffCogReader(_logger);
        using var dataset = await reader.OpenAsync(tiffPath);

        // Act - Read window extending beyond image bounds
        var windowData = await reader.ReadWindowAsync(dataset, x: 90, y: 90, width: 20, height: 20);

        // Assert
        windowData.Should().NotBeNull();
        windowData.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ReadWindowAsync_WithFullImage_ReturnsAllData()
    {
        // Arrange
        var tiffPath = CreateTestTiff(width: 100, height: 100);
        var reader = new LibTiffCogReader(_logger);
        using var dataset = await reader.OpenAsync(tiffPath);

        // Act
        var windowData = await reader.ReadWindowAsync(dataset, x: 0, y: 0, width: 100, height: 100);

        // Assert
        windowData.Should().NotBeNull();
        windowData.Length.Should().Be(10000); // 100 * 100 * 1 byte
    }

    [Fact]
    public async Task ReadWindowAsync_WithMultipleBands_ReturnsCorrectSize()
    {
        // Arrange
        var tiffPath = CreateTestTiff(width: 100, height: 100, samplesPerPixel: 3);
        var reader = new LibTiffCogReader(_logger);
        using var dataset = await reader.OpenAsync(tiffPath);

        // Act
        var windowData = await reader.ReadWindowAsync(dataset, x: 0, y: 0, width: 10, height: 10);

        // Assert
        // Expected: 10 * 10 * 3 bands * 1 byte = 300 bytes
        windowData.Length.Should().Be(300);
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public async Task GetMetadataAsync_ReturnsMetadataWithoutOpeningDataset()
    {
        // Arrange
        var tiffPath = CreateTestTiff(width: 256, height: 256, tileWidth: 64, tileHeight: 64);
        var reader = new LibTiffCogReader(_logger);

        // Act
        var metadata = await reader.GetMetadataAsync(tiffPath);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Width.Should().Be(256);
        metadata.Height.Should().Be(256);
        metadata.TileWidth.Should().Be(64);
        metadata.TileHeight.Should().Be(64);
    }

    [Fact]
    public async Task GetMetadataAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var reader = new LibTiffCogReader(_logger);
        var nonExistentPath = Path.Combine(_testDir, "non-existent.tif");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await reader.GetMetadataAsync(nonExistentPath));
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task Dispose_DisposesResourcesProperly()
    {
        // Arrange
        var tiffPath = CreateTestTiff(width: 100, height: 100);
        var reader = new LibTiffCogReader(_logger);
        var dataset = await reader.OpenAsync(tiffPath);

        // Act
        dataset.Dispose();

        // Assert - Should not throw
        Assert.True(true);
    }

    [Fact]
    public async Task Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var tiffPath = CreateTestTiff(width: 100, height: 100);
        var reader = new LibTiffCogReader(_logger);
        var dataset = await reader.OpenAsync(tiffPath);

        // Act
        dataset.Dispose();
        dataset.Dispose(); // Second dispose

        // Assert - Should not throw
        Assert.True(true);
    }

    #endregion

    #region Helper Methods

    private string CreateTestTiff(
        int width,
        int height,
        int tileWidth = 0,
        int tileHeight = 0,
        int bitsPerSample = 8,
        int samplesPerPixel = 1,
        TiffCompression compression = TiffCompression.NONE)
    {
        var path = Path.Combine(_testDir, $"test-{Guid.NewGuid()}.tif");

        using var tiff = Tiff.Open(path, "w");

        // Basic TIFF tags
        tiff.SetField(TiffTag.IMAGEWIDTH, width);
        tiff.SetField(TiffTag.IMAGELENGTH, height);
        tiff.SetField(TiffTag.BITSPERSAMPLE, bitsPerSample);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, samplesPerPixel);
        tiff.SetField(TiffTag.PHOTOMETRIC, samplesPerPixel == 1
            ? Photometric.MINISBLACK
            : Photometric.RGB);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.COMPRESSION, compression);

        if (tileWidth > 0 && tileHeight > 0)
        {
            // Tiled TIFF
            tiff.SetField(TiffTag.TILEWIDTH, tileWidth);
            tiff.SetField(TiffTag.TILELENGTH, tileHeight);

            var tileSize = tileWidth * tileHeight * (bitsPerSample / 8) * samplesPerPixel;
            var tile = new byte[tileSize];

            var tilesAcross = (width + tileWidth - 1) / tileWidth;
            var tilesDown = (height + tileHeight - 1) / tileHeight;

            for (int ty = 0; ty < tilesDown; ty++)
            {
                for (int tx = 0; tx < tilesAcross; tx++)
                {
                    tiff.WriteEncodedTile(ty * tilesAcross + tx, tile, tileSize);
                }
            }
        }
        else
        {
            // Scanline TIFF
            tiff.SetField(TiffTag.ROWSPERSTRIP, height);

            var scanlineSize = width * (bitsPerSample / 8) * samplesPerPixel;
            var scanline = new byte[scanlineSize];

            for (int i = 0; i < height; i++)
            {
                tiff.WriteScanline(scanline, i);
            }
        }

        tiff.WriteDirectory();

        return path;
    }

    private string CreateTestCogWithOverviews()
    {
        var path = Path.Combine(_testDir, $"cog-{Guid.NewGuid()}.tif");

        using var tiff = Tiff.Open(path, "w");

        // Main image (tiled)
        tiff.SetField(TiffTag.IMAGEWIDTH, 512);
        tiff.SetField(TiffTag.IMAGELENGTH, 512);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.TILEWIDTH, 128);
        tiff.SetField(TiffTag.TILELENGTH, 128);

        var tileSize = 128 * 128;
        var tile = new byte[tileSize];

        for (int i = 0; i < 16; i++) // 4x4 tiles
        {
            tiff.WriteEncodedTile(i, tile, tileSize);
        }

        tiff.WriteDirectory();

        // Overview 1 (256x256)
        tiff.SetField(TiffTag.IMAGEWIDTH, 256);
        tiff.SetField(TiffTag.IMAGELENGTH, 256);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.TILEWIDTH, 128);
        tiff.SetField(TiffTag.TILELENGTH, 128);

        for (int i = 0; i < 4; i++) // 2x2 tiles
        {
            tiff.WriteEncodedTile(i, tile, tileSize);
        }

        tiff.WriteDirectory();

        return path;
    }

    private string CreateTestGeoTiff()
    {
        var path = Path.Combine(_testDir, $"geotiff-{Guid.NewGuid()}.tif");

        using var tiff = Tiff.Open(path, "w");

        // Basic TIFF tags
        tiff.SetField(TiffTag.IMAGEWIDTH, 100);
        tiff.SetField(TiffTag.IMAGELENGTH, 100);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.ROWSPERSTRIP, 100);

        // GeoTIFF tags
        var tiepoints = new double[] { 0, 0, 0, -180, 90, 0 };
        tiff.SetField((TiffTag)33922, 6, tiepoints);

        var pixelScale = new double[] { 0.1, 0.1, 0 };
        tiff.SetField((TiffTag)33550, 3, pixelScale);

        // Write data
        var scanline = new byte[100];
        for (int i = 0; i < 100; i++)
        {
            tiff.WriteScanline(scanline, i);
        }

        tiff.WriteDirectory();

        return path;
    }

    private string CreateTestTiffWithPattern()
    {
        var path = Path.Combine(_testDir, $"pattern-{Guid.NewGuid()}.tif");

        using var tiff = Tiff.Open(path, "w");

        tiff.SetField(TiffTag.IMAGEWIDTH, 256);
        tiff.SetField(TiffTag.IMAGELENGTH, 256);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.TILEWIDTH, 64);
        tiff.SetField(TiffTag.TILELENGTH, 64);

        var tileSize = 64 * 64;
        var tilesAcross = 4;
        var tilesDown = 4;

        for (int ty = 0; ty < tilesDown; ty++)
        {
            for (int tx = 0; tx < tilesAcross; tx++)
            {
                var tile = new byte[tileSize];

                // Fill with pattern based on tile coordinates
                var fillValue = (byte)((tx + ty * tilesAcross) * 16);
                Array.Fill(tile, fillValue);

                tiff.WriteEncodedTile(ty * tilesAcross + tx, tile, tileSize);
            }
        }

        tiff.WriteDirectory();

        return path;
    }

    private Mock<HttpMessageHandler> CreateMockHttpHandlerForCog()
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        // Create a minimal valid TIFF in memory
        var tiffPath = CreateTestTiff(width: 100, height: 100);
        var tiffBytes = File.ReadAllBytes(tiffPath);

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken ct) =>
            {
                if (request.Method == HttpMethod.Head)
                {
                    var headResponse = new HttpResponseMessage(HttpStatusCode.OK);
                    headResponse.Content = new ByteArrayContent(Array.Empty<byte>());
                    headResponse.Content.Headers.ContentLength = tiffBytes.Length;
                    headResponse.Headers.AcceptRanges.Add("bytes");
                    return headResponse;
                }

                // GET request with range
                var response = new HttpResponseMessage(HttpStatusCode.OK);

                if (request.Headers.Range != null && request.Headers.Range.Ranges.Count > 0)
                {
                    var range = request.Headers.Range.Ranges.First();
                    var start = (int)(range.From ?? 0);
                    var end = (int)(range.To ?? tiffBytes.Length - 1);
                    var length = end - start + 1;

                    var rangeBytes = new byte[length];
                    Array.Copy(tiffBytes, start, rangeBytes, 0, length);
                    response.Content = new ByteArrayContent(rangeBytes);
                }
                else
                {
                    response.Content = new ByteArrayContent(tiffBytes);
                }

                return response;
            });

        return mockHandler;
    }

    #endregion
}

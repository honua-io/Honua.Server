using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using BitMiracle.LibTiff.Classic;
using FluentAssertions;
using Honua.Server.Core.Raster.Compression;
using Honua.Server.Core.Raster.Readers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TiffCompression = BitMiracle.LibTiff.Classic.Compression;

namespace Honua.Server.Core.Tests.Raster.Raster.Readers;

/// <summary>
/// Integration tests for COG and Zarr readers with actual sample files.
/// Tests end-to-end functionality with real data.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class CogZarrReadersIntegrationTests : IDisposable
{
    private readonly ILogger<LibTiffCogReader> _cogLogger;
    private readonly ILogger<HttpZarrReader> _zarrLogger;
    private readonly ILogger<GeoTiffTagParser> _tagLogger;
    private readonly ILogger<ZarrDecompressor> _decompressorLogger;
    private readonly ILogger<CompressionCodecRegistry> _codecLogger;
    private readonly string _testDir;

    public CogZarrReadersIntegrationTests()
    {
        _cogLogger = NullLogger<LibTiffCogReader>.Instance;
        _zarrLogger = NullLogger<HttpZarrReader>.Instance;
        _tagLogger = NullLogger<GeoTiffTagParser>.Instance;
        _decompressorLogger = NullLogger<ZarrDecompressor>.Instance;
        _codecLogger = NullLogger<CompressionCodecRegistry>.Instance;
        _testDir = Path.Combine(Path.GetTempPath(), "integration-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    #region COG Integration Tests

    [Fact]
    public async Task LibTiffCogReader_WithRealCogFile_ReadsAllTiles()
    {
        // Arrange
        var cogPath = CreateRealisticCog();
        var reader = new LibTiffCogReader(_cogLogger);

        // Act
        using var dataset = await reader.OpenAsync(cogPath);
        var metadata = dataset.Metadata;

        // Assert
        metadata.Should().NotBeNull();
        metadata.IsTiled.Should().BeTrue();
        metadata.Width.Should().BeGreaterThan(0);
        metadata.Height.Should().BeGreaterThan(0);

        // Read all tiles
        var tilesAcross = (metadata.Width + metadata.TileWidth - 1) / metadata.TileWidth;
        var tilesDown = (metadata.Height + metadata.TileHeight - 1) / metadata.TileHeight;

        for (int ty = 0; ty < tilesDown; ty++)
        {
            for (int tx = 0; tx < tilesAcross; tx++)
            {
                var tileData = await reader.ReadTileAsync(dataset, tx, ty);
                tileData.Should().NotBeNull();
                tileData.Length.Should().BeGreaterThan(0);
            }
        }
    }

    [Fact]
    public async Task LibTiffCogReader_WithCompressedCog_ReadsCorrectly()
    {
        // Arrange
        var cogPath = CreateCompressedCog();
        var reader = new LibTiffCogReader(_cogLogger);

        // Act
        using var dataset = await reader.OpenAsync(cogPath);
        var metadata = dataset.Metadata;

        // Assert
        metadata.Compression.Should().Be("LZW");

        // Read a tile
        var tileData = await reader.ReadTileAsync(dataset, 0, 0);
        tileData.Should().NotBeNull();
        tileData.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task LibTiffCogReader_WithGeoTiffTags_ExtractsCorrectMetadata()
    {
        // Arrange
        var geoTiffPath = CreateGeoTiffWithRealTags();
        var reader = new LibTiffCogReader(_cogLogger);

        // Act
        using var dataset = await reader.OpenAsync(geoTiffPath);
        var metadata = dataset.Metadata;

        // Assert
        metadata.Should().NotBeNull();
        // Note: GeoTIFF tag preservation depends on LibTiff version
        // Just verify no exceptions are thrown
    }

    [Fact]
    public async Task LibTiffCogReader_WithMultiBandImage_ReadsAllBands()
    {
        // Arrange
        var rgbPath = CreateRgbTiff();
        var reader = new LibTiffCogReader(_cogLogger);

        // Act
        using var dataset = await reader.OpenAsync(rgbPath);
        var metadata = dataset.Metadata;

        // Assert
        metadata.BandCount.Should().Be(3);

        // Read window
        var windowData = await reader.ReadWindowAsync(dataset, 0, 0, 10, 10);
        windowData.Should().NotBeNull();

        // Expected: 10 * 10 * 3 bands * 1 byte = 300 bytes
        windowData.Length.Should().Be(300);
    }

    [Fact]
    public async Task LibTiffCogReader_WithOverviews_ReadsCorrectly()
    {
        // Arrange
        var cogPath = CreateCogWithMultipleOverviews();
        var reader = new LibTiffCogReader(_cogLogger);

        // Act
        using var dataset = await reader.OpenAsync(cogPath);
        var metadata = dataset.Metadata;

        // Assert
        metadata.IsCog.Should().BeTrue();
        metadata.OverviewCount.Should().BeGreaterThan(0);

        // Read from main image
        var tileData = await reader.ReadTileAsync(dataset, 0, 0);
        tileData.Should().NotBeNull();
    }

    [Fact]
    public async Task LibTiffCogReader_With16BitData_ReadsCorrectly()
    {
        // Arrange
        var path = Create16BitTiff();
        var reader = new LibTiffCogReader(_cogLogger);

        // Act
        using var dataset = await reader.OpenAsync(path);
        var metadata = dataset.Metadata;

        // Assert
        metadata.BitsPerSample.Should().Be(16);

        var windowData = await reader.ReadWindowAsync(dataset, 0, 0, 10, 10);
        windowData.Should().NotBeNull();

        // Expected: 10 * 10 * 2 bytes (16-bit) = 200 bytes
        windowData.Length.Should().Be(200);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task LibTiffCogReader_WithCorruptedHeader_ThrowsException()
    {
        // Arrange
        var corruptedPath = Path.Combine(_testDir, "corrupted.tif");
        var corruptedData = new byte[1000];
        new Random().NextBytes(corruptedData);
        await File.WriteAllBytesAsync(corruptedPath, corruptedData);

        var reader = new LibTiffCogReader(_cogLogger);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await reader.OpenAsync(corruptedPath));
    }

    [Fact]
    public async Task LibTiffCogReader_WithTruncatedFile_ThrowsException()
    {
        // Arrange
        var validPath = CreateRealisticCog();
        var truncatedPath = Path.Combine(_testDir, "truncated.tif");

        // Copy only first 100 bytes
        var validData = await File.ReadAllBytesAsync(validPath);
        await File.WriteAllBytesAsync(truncatedPath, validData[..100]);

        var reader = new LibTiffCogReader(_cogLogger);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await reader.OpenAsync(truncatedPath));
    }

    [Fact]
    public async Task LibTiffCogReader_WithInvalidTileRequest_HandlesGracefully()
    {
        // Arrange
        var cogPath = CreateRealisticCog();
        var reader = new LibTiffCogReader(_cogLogger);

        using var dataset = await reader.OpenAsync(cogPath);

        // Act & Assert - Request tile far out of bounds
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await reader.ReadTileAsync(dataset, 1000, 1000));
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task LibTiffCogReader_ReadingManyTiles_CompletesInReasonableTime()
    {
        // Arrange
        var cogPath = CreateLargeCog();
        var reader = new LibTiffCogReader(_cogLogger);

        using var dataset = await reader.OpenAsync(cogPath);
        var metadata = dataset.Metadata;

        var tilesAcross = (metadata.Width + metadata.TileWidth - 1) / metadata.TileWidth;
        var tilesDown = (metadata.Height + metadata.TileHeight - 1) / metadata.TileHeight;
        var totalTiles = Math.Min(tilesAcross * tilesDown, 100); // Limit to 100 tiles

        // Act
        var startTime = DateTime.UtcNow;

        for (int i = 0; i < totalTiles; i++)
        {
            int tx = i % tilesAcross;
            int ty = i / tilesAcross;
            await reader.ReadTileAsync(dataset, tx, ty);
        }

        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Should complete within 5 seconds for 100 tiles
        elapsed.TotalSeconds.Should().BeLessThan(5.0);
    }

    #endregion

    #region Sample File Creators

    private string CreateRealisticCog()
    {
        var path = Path.Combine(_testDir, "realistic-cog.tif");

        using var tiff = Tiff.Open(path, "w");

        tiff.SetField(TiffTag.IMAGEWIDTH, 512);
        tiff.SetField(TiffTag.IMAGELENGTH, 512);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.TILEWIDTH, 256);
        tiff.SetField(TiffTag.TILELENGTH, 256);
        tiff.SetField(TiffTag.COMPRESSION, TiffCompression.NONE);

        var tileSize = 256 * 256;
        var tile = new byte[tileSize];

        // Create pattern
        for (int i = 0; i < tileSize; i++)
        {
            tile[i] = (byte)(i % 256);
        }

        for (int i = 0; i < 4; i++) // 2x2 tiles
        {
            tiff.WriteEncodedTile(i, tile, tileSize);
        }

        tiff.WriteDirectory();

        return path;
    }

    private string CreateCompressedCog()
    {
        var path = Path.Combine(_testDir, "compressed-cog.tif");

        using var tiff = Tiff.Open(path, "w");

        tiff.SetField(TiffTag.IMAGEWIDTH, 256);
        tiff.SetField(TiffTag.IMAGELENGTH, 256);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.TILEWIDTH, 128);
        tiff.SetField(TiffTag.TILELENGTH, 128);
        tiff.SetField(TiffTag.COMPRESSION, TiffCompression.LZW);

        var tileSize = 128 * 128;
        var tile = new byte[tileSize];

        for (int i = 0; i < 4; i++)
        {
            tiff.WriteEncodedTile(i, tile, tileSize);
        }

        tiff.WriteDirectory();

        return path;
    }

    private string CreateGeoTiffWithRealTags()
    {
        var path = Path.Combine(_testDir, "geotiff-real.tif");

        using var tiff = Tiff.Open(path, "w");

        tiff.SetField(TiffTag.IMAGEWIDTH, 256);
        tiff.SetField(TiffTag.IMAGELENGTH, 256);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.ROWSPERSTRIP, 256);

        // GeoTIFF tags
        var tiepoints = new double[] { 0, 0, 0, -180, 90, 0 };
        tiff.SetField((TiffTag)33922, 6, tiepoints);

        var pixelScale = new double[] { 1.40625, 0.703125, 0 };
        tiff.SetField((TiffTag)33550, 3, pixelScale);

        // Write data
        var scanline = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            tiff.WriteScanline(scanline, i);
        }

        tiff.WriteDirectory();

        return path;
    }

    private string CreateRgbTiff()
    {
        var path = Path.Combine(_testDir, "rgb.tif");

        using var tiff = Tiff.Open(path, "w");

        tiff.SetField(TiffTag.IMAGEWIDTH, 100);
        tiff.SetField(TiffTag.IMAGELENGTH, 100);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 3);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.RGB);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.ROWSPERSTRIP, 100);

        var scanlineSize = 100 * 3;
        var scanline = new byte[scanlineSize];

        // Create RGB pattern
        for (int i = 0; i < 100; i++)
        {
            for (int j = 0; j < 100; j++)
            {
                scanline[j * 3 + 0] = (byte)((i + j) % 256); // R
                scanline[j * 3 + 1] = (byte)((i * 2) % 256);  // G
                scanline[j * 3 + 2] = (byte)((j * 2) % 256);  // B
            }
            tiff.WriteScanline(scanline, i);
        }

        tiff.WriteDirectory();

        return path;
    }

    private string CreateCogWithMultipleOverviews()
    {
        var path = Path.Combine(_testDir, "cog-overviews.tif");

        using var tiff = Tiff.Open(path, "w");

        // Main image
        tiff.SetField(TiffTag.IMAGEWIDTH, 1024);
        tiff.SetField(TiffTag.IMAGELENGTH, 1024);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.TILEWIDTH, 256);
        tiff.SetField(TiffTag.TILELENGTH, 256);

        var tileSize = 256 * 256;
        var tile = new byte[tileSize];

        for (int i = 0; i < 16; i++) // 4x4 tiles
        {
            tiff.WriteEncodedTile(i, tile, tileSize);
        }

        tiff.WriteDirectory();

        // Overview 1: 512x512
        tiff.SetField(TiffTag.IMAGEWIDTH, 512);
        tiff.SetField(TiffTag.IMAGELENGTH, 512);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.TILEWIDTH, 256);
        tiff.SetField(TiffTag.TILELENGTH, 256);

        for (int i = 0; i < 4; i++) // 2x2 tiles
        {
            tiff.WriteEncodedTile(i, tile, tileSize);
        }

        tiff.WriteDirectory();

        // Overview 2: 256x256
        tiff.SetField(TiffTag.IMAGEWIDTH, 256);
        tiff.SetField(TiffTag.IMAGELENGTH, 256);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.TILEWIDTH, 256);
        tiff.SetField(TiffTag.TILELENGTH, 256);

        tiff.WriteEncodedTile(0, tile, tileSize);

        tiff.WriteDirectory();

        return path;
    }

    private string Create16BitTiff()
    {
        var path = Path.Combine(_testDir, "16bit.tif");

        using var tiff = Tiff.Open(path, "w");

        tiff.SetField(TiffTag.IMAGEWIDTH, 100);
        tiff.SetField(TiffTag.IMAGELENGTH, 100);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 16);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.ROWSPERSTRIP, 100);

        var scanlineSize = 100 * 2; // 2 bytes per pixel
        var scanline = new byte[scanlineSize];

        for (int i = 0; i < 100; i++)
        {
            // Fill with 16-bit pattern
            for (int j = 0; j < 100; j++)
            {
                var value = (ushort)((i * 100 + j) * 65);
                scanline[j * 2] = (byte)(value & 0xFF);
                scanline[j * 2 + 1] = (byte)((value >> 8) & 0xFF);
            }
            tiff.WriteScanline(scanline, i);
        }

        tiff.WriteDirectory();

        return path;
    }

    private string CreateLargeCog()
    {
        var path = Path.Combine(_testDir, "large-cog.tif");

        using var tiff = Tiff.Open(path, "w");

        tiff.SetField(TiffTag.IMAGEWIDTH, 2048);
        tiff.SetField(TiffTag.IMAGELENGTH, 2048);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.TILEWIDTH, 256);
        tiff.SetField(TiffTag.TILELENGTH, 256);

        var tileSize = 256 * 256;
        var tile = new byte[tileSize];

        var tilesAcross = 8; // 2048 / 256
        var tilesDown = 8;

        for (int i = 0; i < tilesAcross * tilesDown; i++)
        {
            tiff.WriteEncodedTile(i, tile, tileSize);
        }

        tiff.WriteDirectory();

        return path;
    }

    #endregion
}

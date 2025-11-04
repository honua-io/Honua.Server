using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster.Analytics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Analytics;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class RasterAnalyticsServiceTests
{
    private readonly RasterAnalyticsService _service;

    public RasterAnalyticsServiceTests()
    {
        _service = new RasterAnalyticsService(NullLogger<RasterAnalyticsService>.Instance);
    }

    [Fact]
    public void GetCapabilities_ShouldReturnSupportedOperations()
    {
        var capabilities = _service.GetCapabilities();

        capabilities.SupportedAlgebraOperators.Should().Contain(new[] { "+", "-", "*", "/", "min", "max", "mean", "stddev", "sqrt", "square", "log" });
        capabilities.SupportedAlgebraFunctions.Should().Contain(new[] { "ndvi", "evi", "savi", "ndwi", "ndmi", "normalize" });
        capabilities.SupportedTerrainAnalyses.Should().Contain(new[] { "hillshade", "slope", "aspect", "curvature", "roughness" });
        // Note: These values come from RasterMemoryLimits which may have different defaults
        capabilities.MaxAlgebraDatasets.Should().BeGreaterThan(0);
        capabilities.MaxExtractionPoints.Should().BeGreaterThan(0);
        capabilities.MaxHistogramBins.Should().BeGreaterThan(0);
        capabilities.MaxZonalPolygons.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CalculateStatisticsAsync_ShouldThrowWhenDatasetNotLoadable()
    {
        var dataset = CreateTestDataset("dataset-1", "/nonexistent/file.tif");
        var request = new RasterStatisticsRequest(dataset, null, null);

        var act = async () => await _service.CalculateStatisticsAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Could not load dataset dataset-1");
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    public async Task CalculateStatisticsAsync_ShouldRejectNonGeoTiffFormats(string extension)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-image{extension}");
        File.WriteAllBytes(tempPath, new byte[] { 0, 1, 2, 3 }); // Dummy file

        try
        {
            var dataset = CreateTestDataset("dataset-1", tempPath);
            var request = new RasterStatisticsRequest(dataset, null, null);

            var act = async () => await _service.CalculateStatisticsAsync(request);

            await act.Should().ThrowAsync<NotSupportedException>()
                .WithMessage("*Only GeoTIFF*");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public async Task CalculateStatisticsAsync_ShouldReturnBandStatistics()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("dataset-1", testImagePath);
        var request = new RasterStatisticsRequest(dataset, null, null);

        var result = await _service.CalculateStatisticsAsync(request);

        result.Should().NotBeNull();
        result.DatasetId.Should().Be("dataset-1");
        result.BandCount.Should().BeGreaterThan(0);
        result.Bands.Should().NotBeEmpty();

        foreach (var bandStat in result.Bands)
        {
            bandStat.Min.Should().BeGreaterThanOrEqualTo(0);
            bandStat.Max.Should().BeLessThanOrEqualTo(255);
            bandStat.Mean.Should().BeInRange(bandStat.Min, bandStat.Max);
            bandStat.ValidPixelCount.Should().BeGreaterThan(0);
        }

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateStatisticsAsync_ShouldFilterByBandIndex()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("dataset-1", testImagePath);
        var request = new RasterStatisticsRequest(dataset, null, 0);

        var result = await _service.CalculateStatisticsAsync(request);

        result.Bands.Should().HaveCount(1);
        result.Bands[0].BandIndex.Should().Be(0);

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateStatisticsAsync_ShouldIncludeMedianValue()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("dataset-1", testImagePath);
        var request = new RasterStatisticsRequest(dataset, null, null);

        var result = await _service.CalculateStatisticsAsync(request);

        foreach (var bandStat in result.Bands)
        {
            bandStat.Median.Should().BeInRange(bandStat.Min, bandStat.Max);
        }

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateAlgebraAsync_ShouldThrowWhenTooManyDatasets()
    {
        var datasets = Enumerable.Range(0, 11)
            .Select(i => CreateTestDataset($"dataset-{i}"))
            .ToList();

        var request = new RasterAlgebraRequest(
            datasets, "A + B",
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "png");

        var act = async () => await _service.CalculateAlgebraAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*exceeds maximum allowed*");
    }

    [Fact]
    public async Task CalculateAlgebraAsync_ShouldThrowWhenNoDatasetFilesExist()
    {
        var datasets = new[] { CreateTestDataset("dataset-1", "/nonexistent/file.tif") };

        var request = new RasterAlgebraRequest(
            datasets, "A",
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "png");

        var act = async () => await _service.CalculateAlgebraAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No datasets could be loaded");
    }

    [Fact]
    public async Task CalculateAlgebraAsync_ShouldSupportAdditionOperation()
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] { CreateTestDataset("dataset-1", testImagePath), CreateTestDataset("dataset-2", testImagePath) };

        var request = new RasterAlgebraRequest(
            datasets, "A + B",
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "png");

        var result = await _service.CalculateAlgebraAsync(request);

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();
        result.ContentType.Should().Be("image/png");
        result.Width.Should().Be(256);
        result.Height.Should().Be(256);
        result.Statistics.Should().NotBeNull();

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateAlgebraAsync_ShouldSupportSubtractionOperation()
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] { CreateTestDataset("dataset-1", testImagePath), CreateTestDataset("dataset-2", testImagePath) };

        var request = new RasterAlgebraRequest(
            datasets, "A - B",
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "png");

        var result = await _service.CalculateAlgebraAsync(request);

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateAlgebraAsync_ShouldSupportMultiplicationOperation()
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] { CreateTestDataset("dataset-1", testImagePath), CreateTestDataset("dataset-2", testImagePath) };

        var request = new RasterAlgebraRequest(
            datasets, "A * B",
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "png");

        var result = await _service.CalculateAlgebraAsync(request);

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateAlgebraAsync_ShouldSupportNdviFunction()
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] { CreateTestDataset("dataset-1", testImagePath), CreateTestDataset("dataset-2", testImagePath) };

        var request = new RasterAlgebraRequest(
            datasets, "ndvi",
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "png");

        var result = await _service.CalculateAlgebraAsync(request);

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();
        result.Statistics.DatasetId.Should().Be("algebra_result");

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateAlgebraAsync_ShouldSupportMeanFunction()
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] { CreateTestDataset("dataset-1", testImagePath), CreateTestDataset("dataset-2", testImagePath) };

        var request = new RasterAlgebraRequest(
            datasets, "mean",
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "png");

        var result = await _service.CalculateAlgebraAsync(request);

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();

        CleanupTestImage(testImagePath);
    }

    [Theory]
    [InlineData("png", "image/png")]
    [InlineData("jpeg", "image/jpeg")]
    [InlineData("jpg", "image/jpeg")]
    public async Task CalculateAlgebraAsync_ShouldSupportMultipleFormats(string format, string expectedContentType)
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] { CreateTestDataset("dataset-1", testImagePath) };

        var request = new RasterAlgebraRequest(
            datasets, "A",
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, format);

        var result = await _service.CalculateAlgebraAsync(request);

        result.ContentType.Should().Be(expectedContentType);
        result.Data.Should().NotBeEmpty();

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task ExtractValuesAsync_ShouldThrowWhenTooManyPoints()
    {
        var dataset = CreateTestDataset("dataset-1");
        var points = Enumerable.Range(0, 10001)
            .Select(i => new Point(0.0, 0.0))
            .ToList();

        var request = new RasterValueExtractionRequest(dataset, points, null);

        var act = async () => await _service.ExtractValuesAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*exceeds maximum allowed*");
    }

    [Fact]
    public async Task ExtractValuesAsync_ShouldThrowWhenDatasetNotLoadable()
    {
        var dataset = CreateTestDataset("dataset-1", "/nonexistent/file.tif");
        var points = new[] { new Point(0.0, 0.0) };
        var request = new RasterValueExtractionRequest(dataset, points, null);

        var act = async () => await _service.ExtractValuesAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Could not load dataset dataset-1");
    }

    [Fact]
    public async Task ExtractValuesAsync_ShouldExtractValuesAtPoints()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("dataset-1", testImagePath);
        var points = new[] { new Point(0.0, 0.0), new Point(0.5, 0.5) };
        var request = new RasterValueExtractionRequest(dataset, points, null);

        var result = await _service.ExtractValuesAsync(request);

        result.Should().NotBeNull();
        result.DatasetId.Should().Be("dataset-1");
        result.Values.Should().HaveCount(2);

        foreach (var value in result.Values)
        {
            value.X.Should().BeOneOf(0.0, 0.5);
            value.Y.Should().BeOneOf(0.0, 0.5);
            value.BandIndex.Should().Be(0);
        }

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task ExtractValuesAsync_ShouldReturnNullForPointsOutsideExtent()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("dataset-1", testImagePath);
        var points = new[] { new Point(10.0, 10.0) }; // Outside extent [-1, -1, 1, 1]
        var request = new RasterValueExtractionRequest(dataset, points, null);

        var result = await _service.ExtractValuesAsync(request);

        result.Values.Should().HaveCount(1);
        result.Values[0].Value.Should().BeNull();

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task ExtractValuesAsync_ShouldFilterByBandIndex()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("dataset-1", testImagePath);
        var points = new[] { new Point(0.0, 0.0) };
        var request = new RasterValueExtractionRequest(dataset, points, 1);

        var result = await _service.ExtractValuesAsync(request);

        result.Values.Should().HaveCount(1);
        result.Values[0].BandIndex.Should().Be(1);

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateHistogramAsync_ShouldThrowWhenTooManyBins()
    {
        var dataset = CreateTestDataset("dataset-1");
        var request = new RasterHistogramRequest(dataset, 1001, null, null);

        var act = async () => await _service.CalculateHistogramAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*exceeds maximum allowed*");
    }

    [Fact]
    public async Task CalculateHistogramAsync_ShouldThrowWhenDatasetNotLoadable()
    {
        var dataset = CreateTestDataset("dataset-1", "/nonexistent/file.tif");
        var request = new RasterHistogramRequest(dataset, 256, null, null);

        var act = async () => await _service.CalculateHistogramAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Could not load dataset dataset-1");
    }

    [Fact]
    public async Task CalculateHistogramAsync_ShouldReturnHistogramBins()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("dataset-1", testImagePath);
        var request = new RasterHistogramRequest(dataset, 256, null, null);

        var result = await _service.CalculateHistogramAsync(request);

        result.Should().NotBeNull();
        result.DatasetId.Should().Be("dataset-1");
        result.BandIndex.Should().Be(0);
        result.Bins.Should().HaveCount(256);
        result.Min.Should().BeGreaterThanOrEqualTo(0);
        result.Max.Should().BeLessThanOrEqualTo(255);

        foreach (var bin in result.Bins)
        {
            bin.Count.Should().BeGreaterThanOrEqualTo(0);
            bin.RangeStart.Should().BeLessThanOrEqualTo(bin.RangeEnd);
        }

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateHistogramAsync_ShouldFilterByBandIndex()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("dataset-1", testImagePath);
        var request = new RasterHistogramRequest(dataset, 256, null, 1);

        var result = await _service.CalculateHistogramAsync(request);

        result.BandIndex.Should().Be(1);

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateHistogramAsync_ShouldUseSpecifiedBinCount()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("dataset-1", testImagePath);
        var request = new RasterHistogramRequest(dataset, 128, null, null);

        var result = await _service.CalculateHistogramAsync(request);

        result.Bins.Should().HaveCount(128);

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateHistogramAsync_ShouldHandleDatasetWithZeroValues()
    {
        var testImagePath = CreateEmptyImage();
        var dataset = CreateTestDataset("dataset-1", testImagePath);
        var request = new RasterHistogramRequest(dataset, 256, null, null);

        var result = await _service.CalculateHistogramAsync(request);

        result.Should().NotBeNull();
        result.DatasetId.Should().Be("dataset-1");
        // Empty/zero-value GeoTIFF will have bins with zero counts
        result.Bins.Should().NotBeNull();

        CleanupTestImage(testImagePath);
    }

    private static RasterDatasetDefinition CreateTestDataset(string id, string? filePath = null)
    {
        return new RasterDatasetDefinition
        {
            Id = id,
            Title = $"Test Dataset {id}",
            ServiceId = "test-service",
            Source = new RasterSourceDefinition
            {
                Type = "geotiff",
                Uri = filePath ?? "/test/data.tif"
            },
            Extent = new LayerExtentDefinition
            {
                Bbox = new[] { new[] { -1.0, -1.0, 1.0, 1.0 } }
            }
        };
    }

    private static string CreateTestImage()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-raster-{Guid.NewGuid()}.tif");

        // Create a simple GeoTIFF with test data
        using var tiff = BitMiracle.LibTiff.Classic.Tiff.Open(tempPath, "w");

        const int width = 100;
        const int height = 100;

        tiff.SetField(BitMiracle.LibTiff.Classic.TiffTag.IMAGEWIDTH, width);
        tiff.SetField(BitMiracle.LibTiff.Classic.TiffTag.IMAGELENGTH, height);
        tiff.SetField(BitMiracle.LibTiff.Classic.TiffTag.BITSPERSAMPLE, 8);
        tiff.SetField(BitMiracle.LibTiff.Classic.TiffTag.SAMPLESPERPIXEL, 3);
        tiff.SetField(BitMiracle.LibTiff.Classic.TiffTag.PHOTOMETRIC, BitMiracle.LibTiff.Classic.Photometric.RGB);
        tiff.SetField(BitMiracle.LibTiff.Classic.TiffTag.PLANARCONFIG, BitMiracle.LibTiff.Classic.PlanarConfig.CONTIG);

        // Write blue pixels
        var scanline = new byte[width * 3];
        for (int i = 0; i < width; i++)
        {
            scanline[i * 3] = 0;     // R
            scanline[i * 3 + 1] = 0; // G
            scanline[i * 3 + 2] = 255; // B (blue)
        }

        for (int row = 0; row < height; row++)
        {
            tiff.WriteScanline(scanline, row);
        }

        return tempPath;
    }

    private static string CreateEmptyImage()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-raster-empty-{Guid.NewGuid()}.tif");

        // Create a GeoTIFF with transparent/zero pixels
        using var tiff = BitMiracle.LibTiff.Classic.Tiff.Open(tempPath, "w");

        const int width = 100;
        const int height = 100;

        tiff.SetField(BitMiracle.LibTiff.Classic.TiffTag.IMAGEWIDTH, width);
        tiff.SetField(BitMiracle.LibTiff.Classic.TiffTag.IMAGELENGTH, height);
        tiff.SetField(BitMiracle.LibTiff.Classic.TiffTag.BITSPERSAMPLE, 8);
        tiff.SetField(BitMiracle.LibTiff.Classic.TiffTag.SAMPLESPERPIXEL, 3);
        tiff.SetField(BitMiracle.LibTiff.Classic.TiffTag.PHOTOMETRIC, BitMiracle.LibTiff.Classic.Photometric.RGB);
        tiff.SetField(BitMiracle.LibTiff.Classic.TiffTag.PLANARCONFIG, BitMiracle.LibTiff.Classic.PlanarConfig.CONTIG);

        // Write zero pixels
        var scanline = new byte[width * 3];
        for (int row = 0; row < height; row++)
        {
            tiff.WriteScanline(scanline, row);
        }

        return tempPath;
    }

    private static void CleanupTestImage(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CalculateAlgebraAsync_ShouldSupportEviFunction()
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] {
            CreateTestDataset("nir", testImagePath),
            CreateTestDataset("red", testImagePath),
            CreateTestDataset("blue", testImagePath)
        };

        var request = new RasterAlgebraRequest(
            datasets, "evi",
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "png");

        var result = await _service.CalculateAlgebraAsync(request);

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();
        result.Statistics.DatasetId.Should().Be("algebra_result");

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateAlgebraAsync_ShouldSupportSaviFunction()
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] {
            CreateTestDataset("nir", testImagePath),
            CreateTestDataset("red", testImagePath)
        };

        var request = new RasterAlgebraRequest(
            datasets, "savi",
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "png");

        var result = await _service.CalculateAlgebraAsync(request);

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateAlgebraAsync_ShouldSupportNdwi()
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] {
            CreateTestDataset("green", testImagePath),
            CreateTestDataset("nir", testImagePath)
        };

        var request = new RasterAlgebraRequest(
            datasets, "ndwi",
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "png");

        var result = await _service.CalculateAlgebraAsync(request);

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateAlgebraAsync_ShouldSupportNdmi()
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] {
            CreateTestDataset("nir", testImagePath),
            CreateTestDataset("swir", testImagePath)
        };

        var request = new RasterAlgebraRequest(
            datasets, "ndmi",
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "png");

        var result = await _service.CalculateAlgebraAsync(request);

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateAlgebraAsync_ShouldSupportMinFunction()
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] {
            CreateTestDataset("a", testImagePath),
            CreateTestDataset("b", testImagePath)
        };

        var request = new RasterAlgebraRequest(
            datasets, "min",
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "png");

        var result = await _service.CalculateAlgebraAsync(request);

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateAlgebraAsync_ShouldSupportMaxFunction()
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] {
            CreateTestDataset("a", testImagePath),
            CreateTestDataset("b", testImagePath)
        };

        var request = new RasterAlgebraRequest(
            datasets, "max",
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "png");

        var result = await _service.CalculateAlgebraAsync(request);

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateAlgebraAsync_ShouldSupportStddevFunction()
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] {
            CreateTestDataset("a", testImagePath),
            CreateTestDataset("b", testImagePath),
            CreateTestDataset("c", testImagePath)
        };

        var request = new RasterAlgebraRequest(
            datasets, "stddev",
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "png");

        var result = await _service.CalculateAlgebraAsync(request);

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateZonalStatisticsAsync_ShouldThrowWhenTooManyZones()
    {
        var dataset = CreateTestDataset("dataset-1");
        var zones = Enumerable.Range(0, 1001)
            .Select(i => new Polygon(
                new[] { new Point(0.0, 0.0), new Point(1.0, 0.0), new Point(1.0, 1.0), new Point(0.0, 0.0) },
                $"zone-{i}",
                null))
            .ToList();

        var request = new ZonalStatisticsRequest(dataset, zones, null, null);

        var act = async () => await _service.CalculateZonalStatisticsAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*exceeds maximum allowed*");
    }

    [Fact]
    public async Task CalculateZonalStatisticsAsync_ShouldThrowWhenDatasetNotLoadable()
    {
        var dataset = CreateTestDataset("dataset-1", "/nonexistent/file.tif");
        var zones = new[] {
            new Polygon(
                new[] { new Point(0.0, 0.0), new Point(1.0, 0.0), new Point(1.0, 1.0), new Point(0.0, 0.0) },
                "zone-1",
                null)
        };
        var request = new ZonalStatisticsRequest(dataset, zones, null, null);

        var act = async () => await _service.CalculateZonalStatisticsAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Could not load dataset dataset-1");
    }

    [Fact]
    public async Task CalculateZonalStatisticsAsync_ShouldReturnStatisticsForZones()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("dataset-1", testImagePath);
        var zones = new[] {
            new Polygon(
                new[] { new Point(-0.5, -0.5), new Point(0.5, -0.5), new Point(0.5, 0.5), new Point(-0.5, 0.5), new Point(-0.5, -0.5) },
                "zone-1",
                new Dictionary<string, object> { ["name"] = "Test Zone" })
        };
        var request = new ZonalStatisticsRequest(dataset, zones, null, null);

        var result = await _service.CalculateZonalStatisticsAsync(request);

        result.Should().NotBeNull();
        result.DatasetId.Should().Be("dataset-1");
        result.Zones.Should().HaveCount(1);

        var zoneStats = result.Zones[0];
        zoneStats.ZoneId.Should().Be("zone-1");
        zoneStats.BandIndex.Should().Be(0);
        zoneStats.Mean.Should().BeInRange(zoneStats.Min, zoneStats.Max);
        zoneStats.StdDev.Should().BeGreaterThanOrEqualTo(0);
        zoneStats.PixelCount.Should().BeGreaterThan(0);
        zoneStats.Properties.Should().ContainKey("name");

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateZonalStatisticsAsync_ShouldHandleEmptyZone()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("dataset-1", testImagePath);
        var zones = new[] {
            new Polygon(
                new[] { new Point(10.0, 10.0), new Point(11.0, 10.0), new Point(11.0, 11.0), new Point(10.0, 10.0) },
                "empty-zone",
                null)
        };
        var request = new ZonalStatisticsRequest(dataset, zones, null, null);

        var result = await _service.CalculateZonalStatisticsAsync(request);

        result.Zones.Should().HaveCount(1);
        var zoneStats = result.Zones[0];
        zoneStats.PixelCount.Should().Be(0);

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateZonalStatisticsAsync_ShouldFilterByBandIndex()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("dataset-1", testImagePath);
        var zones = new[] {
            new Polygon(
                new[] { new Point(-0.5, -0.5), new Point(0.5, -0.5), new Point(0.5, 0.5), new Point(-0.5, -0.5) },
                "zone-1",
                null)
        };
        var request = new ZonalStatisticsRequest(dataset, zones, 1, null);

        var result = await _service.CalculateZonalStatisticsAsync(request);

        result.Zones[0].BandIndex.Should().Be(1);

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateZonalStatisticsAsync_ShouldCalculateMedianWhenRequested()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("dataset-1", testImagePath);
        var zones = new[] {
            new Polygon(
                new[] { new Point(-0.5, -0.5), new Point(0.5, -0.5), new Point(0.5, 0.5), new Point(-0.5, -0.5) },
                "zone-1",
                null)
        };
        var request = new ZonalStatisticsRequest(dataset, zones, null, new[] { "mean", "median" });

        var result = await _service.CalculateZonalStatisticsAsync(request);

        result.Zones[0].Median.Should().NotBeNull();
        result.Zones[0].Median.Should().BeInRange(result.Zones[0].Min, result.Zones[0].Max);

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateTerrainAsync_ShouldThrowWhenDatasetNotLoadable()
    {
        var dataset = CreateTestDataset("elevation", "/nonexistent/dem.tif");
        var request = new TerrainAnalysisRequest(dataset, TerrainAnalysisType.Hillshade);

        var act = async () => await _service.CalculateTerrainAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Could not load elevation dataset elevation");
    }

    [Fact]
    public async Task CalculateTerrainAsync_ShouldGenerateHillshade()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("elevation", testImagePath);
        var request = new TerrainAnalysisRequest(dataset, TerrainAnalysisType.Hillshade);

        var result = await _service.CalculateTerrainAsync(request);

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();
        result.ContentType.Should().Be("image/png");
        result.AnalysisType.Should().Be(TerrainAnalysisType.Hillshade);
        result.Statistics.Should().NotBeNull();
        result.Statistics.Unit.Should().Be("0-255");

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateTerrainAsync_ShouldGenerateSlope()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("elevation", testImagePath);
        var request = new TerrainAnalysisRequest(dataset, TerrainAnalysisType.Slope);

        var result = await _service.CalculateTerrainAsync(request);

        result.Should().NotBeNull();
        result.AnalysisType.Should().Be(TerrainAnalysisType.Slope);
        result.Statistics.Unit.Should().Be("degrees");
        result.Statistics.MaxValue.Should().BeLessThanOrEqualTo(255);

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateTerrainAsync_ShouldGenerateAspect()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("elevation", testImagePath);
        var request = new TerrainAnalysisRequest(dataset, TerrainAnalysisType.Aspect);

        var result = await _service.CalculateTerrainAsync(request);

        result.Should().NotBeNull();
        result.AnalysisType.Should().Be(TerrainAnalysisType.Aspect);
        result.Statistics.Unit.Should().Be("degrees");

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateTerrainAsync_ShouldGenerateCurvature()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("elevation", testImagePath);
        var request = new TerrainAnalysisRequest(dataset, TerrainAnalysisType.Curvature);

        var result = await _service.CalculateTerrainAsync(request);

        result.Should().NotBeNull();
        result.AnalysisType.Should().Be(TerrainAnalysisType.Curvature);
        result.Statistics.Unit.Should().Be("units");

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateTerrainAsync_ShouldGenerateRoughness()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("elevation", testImagePath);
        var request = new TerrainAnalysisRequest(dataset, TerrainAnalysisType.Roughness);

        var result = await _service.CalculateTerrainAsync(request);

        result.Should().NotBeNull();
        result.AnalysisType.Should().Be(TerrainAnalysisType.Roughness);
        result.Statistics.Unit.Should().Be("units");

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CalculateTerrainAsync_ShouldSupportDifferentOutputFormats()
    {
        var testImagePath = CreateTestImage();
        var dataset = CreateTestDataset("elevation", testImagePath);
        var requestPng = new TerrainAnalysisRequest(dataset, TerrainAnalysisType.Hillshade, Format: "png");
        var requestJpeg = new TerrainAnalysisRequest(dataset, TerrainAnalysisType.Hillshade, Format: "jpeg");

        var resultPng = await _service.CalculateTerrainAsync(requestPng);
        var resultJpeg = await _service.CalculateTerrainAsync(requestJpeg);

        resultPng.ContentType.Should().Be("image/png");
        resultJpeg.ContentType.Should().Be("image/jpeg");

        CleanupTestImage(testImagePath);
    }
}

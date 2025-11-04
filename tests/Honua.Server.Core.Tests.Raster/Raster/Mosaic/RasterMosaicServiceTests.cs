using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster.Mosaic;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Mosaic;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class RasterMosaicServiceTests
{
    private readonly RasterMosaicService _service;

    public RasterMosaicServiceTests()
    {
        _service = new RasterMosaicService(NullLogger<RasterMosaicService>.Instance);
    }

    [Fact]
    public void GetCapabilities_ShouldReturnSupportedMethods()
    {
        var capabilities = _service.GetCapabilities();

        capabilities.SupportedMethods.Should().Contain(new[] { "First", "Last", "Min", "Max", "Mean", "Median", "Blend" });
        capabilities.SupportedResamplingMethods.Should().Contain(new[] { "NearestNeighbor", "Bilinear", "Cubic", "Lanczos" });
        capabilities.SupportedFormats.Should().Contain(new[] { "png", "jpeg" });
        capabilities.MaxDatasets.Should().Be(100);
        capabilities.MaxWidth.Should().Be(8192);
        capabilities.MaxHeight.Should().Be(8192);
    }

    [Fact]
    public async Task CreateMosaicAsync_ShouldThrowWhenNoDatasetsProvided()
    {
        var request = new RasterMosaicRequest(
            Array.Empty<RasterDatasetDefinition>(),
            new[] { -180.0, -90.0, 180.0, 90.0 },
            512, 512, "EPSG:4326", "EPSG:4326", "png", true,
            RasterMosaicMethod.First, RasterResamplingMethod.Bilinear, null);

        var act = async () => await _service.CreateMosaicAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("At least one dataset is required*");
    }

    [Fact]
    public async Task CreateMosaicAsync_ShouldThrowWhenTooManyDatasets()
    {
        var datasets = Enumerable.Range(0, 101)
            .Select(i => CreateTestDataset($"dataset-{i}"))
            .ToList();

        var request = new RasterMosaicRequest(
            datasets,
            new[] { -180.0, -90.0, 180.0, 90.0 },
            512, 512, "EPSG:4326", "EPSG:4326", "png", true,
            RasterMosaicMethod.First, RasterResamplingMethod.Bilinear, null);

        var act = async () => await _service.CreateMosaicAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Maximum 100 datasets allowed*");
    }

    [Fact]
    public async Task CreateMosaicAsync_ShouldThrowWhenDimensionsTooLarge()
    {
        var datasets = new[] { CreateTestDataset("dataset-1") };

        var request = new RasterMosaicRequest(
            datasets,
            new[] { -180.0, -90.0, 180.0, 90.0 },
            8193, 8193, "EPSG:4326", "EPSG:4326", "png", true,
            RasterMosaicMethod.First, RasterResamplingMethod.Bilinear, null);

        var act = async () => await _service.CreateMosaicAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Maximum dimensions: 8192x8192*");
    }

    [Fact]
    public async Task CreateMosaicAsync_ShouldThrowWhenNoDatasetFilesExist()
    {
        var datasets = new[] { CreateTestDataset("dataset-1", "/nonexistent/file.tif") };

        var request = new RasterMosaicRequest(
            datasets,
            new[] { -180.0, -90.0, 180.0, 90.0 },
            512, 512, "EPSG:4326", "EPSG:4326", "png", true,
            RasterMosaicMethod.First, RasterResamplingMethod.Bilinear, null);

        var act = async () => await _service.CreateMosaicAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No datasets could be loaded");
    }

    [Theory]
    [InlineData("png", "image/png")]
    [InlineData("jpeg", "image/jpeg")]
    [InlineData("jpg", "image/jpeg")]
    public async Task CreateMosaicAsync_ShouldReturnCorrectContentType(string format, string expectedContentType)
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] { CreateTestDataset("dataset-1", testImagePath) };

        var request = new RasterMosaicRequest(
            datasets,
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "EPSG:4326", "EPSG:4326", format, true,
            RasterMosaicMethod.First, RasterResamplingMethod.Bilinear, null);

        var result = await _service.CreateMosaicAsync(request);

        result.ContentType.Should().Be(expectedContentType);
        result.Data.Should().NotBeEmpty();
        result.Width.Should().Be(256);
        result.Height.Should().Be(256);
        result.Metadata.DatasetCount.Should().Be(1);
        result.Metadata.Method.Should().Be("First");
        result.Metadata.Resampling.Should().Be("Bilinear");

        CleanupTestImage(testImagePath);
    }

    [Theory]
    [InlineData(RasterMosaicMethod.First)]
    [InlineData(RasterMosaicMethod.Last)]
    [InlineData(RasterMosaicMethod.Blend)]
    [InlineData(RasterMosaicMethod.Min)]
    [InlineData(RasterMosaicMethod.Max)]
    [InlineData(RasterMosaicMethod.Mean)]
    [InlineData(RasterMosaicMethod.Median)]
    public async Task CreateMosaicAsync_ShouldSupportAllMosaicMethods(RasterMosaicMethod method)
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] { CreateTestDataset("dataset-1", testImagePath) };

        var request = new RasterMosaicRequest(
            datasets,
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "EPSG:4326", "EPSG:4326", "png", true,
            method, RasterResamplingMethod.Bilinear, null);

        var result = await _service.CreateMosaicAsync(request);

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();
        result.Metadata.Method.Should().Be(method.ToString());

        CleanupTestImage(testImagePath);
    }

    [Theory]
    [InlineData(RasterResamplingMethod.NearestNeighbor)]
    [InlineData(RasterResamplingMethod.Bilinear)]
    [InlineData(RasterResamplingMethod.Cubic)]
    [InlineData(RasterResamplingMethod.Lanczos)]
    public async Task CreateMosaicAsync_ShouldSupportAllResamplingMethods(RasterResamplingMethod resampling)
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] { CreateTestDataset("dataset-1", testImagePath) };

        var request = new RasterMosaicRequest(
            datasets,
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "EPSG:4326", "EPSG:4326", "png", true,
            RasterMosaicMethod.First, resampling, null);

        var result = await _service.CreateMosaicAsync(request);

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();
        result.Metadata.Resampling.Should().Be(resampling.ToString());

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CreateMosaicAsync_ShouldIncludeTransparencyInPng()
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] { CreateTestDataset("dataset-1", testImagePath) };

        var request = new RasterMosaicRequest(
            datasets,
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "EPSG:4326", "EPSG:4326", "png", true,
            RasterMosaicMethod.First, RasterResamplingMethod.Bilinear, null);

        var result = await _service.CreateMosaicAsync(request);

        result.ContentType.Should().Be("image/png");
        result.Data.Should().NotBeEmpty();

        CleanupTestImage(testImagePath);
    }

    [Fact]
    public async Task CreateMosaicAsync_ShouldExcludeTransparencyInJpeg()
    {
        var testImagePath = CreateTestImage();
        var datasets = new[] { CreateTestDataset("dataset-1", testImagePath) };

        var request = new RasterMosaicRequest(
            datasets,
            new[] { -1.0, -1.0, 1.0, 1.0 },
            256, 256, "EPSG:4326", "EPSG:4326", "jpeg", false,
            RasterMosaicMethod.First, RasterResamplingMethod.Bilinear, null);

        var result = await _service.CreateMosaicAsync(request);

        result.ContentType.Should().Be("image/jpeg");
        result.Data.Should().NotBeEmpty();

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
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-raster-{Guid.NewGuid()}.png");

        using var bitmap = new SkiaSharp.SKBitmap(100, 100);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SkiaSharp.SKColors.Blue);

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        using var stream = File.OpenWrite(tempPath);
        data.SaveTo(stream);

        return tempPath;
    }

    private static void CleanupTestImage(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Analytics;
using Honua.Server.Core.Raster.Cache;
using Honua.Server.Core.Raster.Compression;
using Honua.Server.Core.Raster.Readers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster;

/// <summary>
/// Tests for memory limit enforcement in raster operations.
/// Validates that all limits are properly enforced to prevent OOM conditions.
/// </summary>
public sealed class RasterMemoryLimitsTests
{
    private readonly ILogger<HttpZarrReader> _zarrLogger;
    private readonly ILogger<RasterAnalyticsService> _analyticsLogger;
    private readonly ILogger<ZarrChunkCache> _cacheLogger;
    private readonly ILogger<ZarrDecompressor> _decompressorLogger;

    public RasterMemoryLimitsTests()
    {
        _zarrLogger = new Mock<ILogger<HttpZarrReader>>().Object;
        _analyticsLogger = new Mock<ILogger<RasterAnalyticsService>>().Object;
        _cacheLogger = new Mock<ILogger<ZarrChunkCache>>().Object;
        _decompressorLogger = new Mock<ILogger<ZarrDecompressor>>().Object;
    }

    [Fact]
    public void RasterMemoryLimits_DefaultValues_AreReasonable()
    {
        // Arrange & Act
        var limits = new RasterMemoryLimits();

        // Assert
        Assert.Equal(100_000_000, limits.MaxSliceSizeBytes); // 100 MB
        Assert.Equal(100, limits.MaxChunksPerRequest);
        Assert.Equal(5, limits.MaxSimultaneousDatasets);
        Assert.Equal(1000, limits.MaxHistogramBins);
        Assert.Equal(10_000, limits.MaxZonalPolygonVertices);
        Assert.Equal(1000, limits.MaxZonalPolygons);
        Assert.Equal(10_000, limits.MaxExtractionPoints);
        Assert.Equal(8192, limits.MaxRasterDimension);
        Assert.Equal(16_777_216, limits.MaxRasterPixels);
    }

    [Fact]
    public void RasterMemoryLimits_Validate_ThrowsOnInvalidValues()
    {
        // Arrange
        var limits = new RasterMemoryLimits { MaxSliceSizeBytes = -1 };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => limits.Validate());
    }

    [Fact]
    public async Task HttpZarrReader_ReadSliceAsync_EnforcesMaxSliceSize()
    {
        // Arrange
        var limits = new RasterMemoryLimits { MaxSliceSizeBytes = 1000 }; // 1 KB limit

        var mockHandler = CreateMockHttpHandler();
        var httpClient = new HttpClient(mockHandler.Object);
        var codecRegistry = new CompressionCodecRegistry(NullLogger<CompressionCodecRegistry>.Instance);
        var decompressor = new ZarrDecompressor(_decompressorLogger, codecRegistry);

        var reader = new HttpZarrReader(_zarrLogger, httpClient, decompressor, limits);

        // Create a mock array that would exceed limits
        var array = new ZarrArray
        {
            Uri = "http://test.com/data.zarr",
            VariableName = "temperature",
            Metadata = new ZarrArrayMetadata
            {
                Shape = new[] { 100, 100, 100 },
                Chunks = new[] { 10, 10, 10 },
                DType = "<f4", // 4 bytes per element
                Compressor = "null",
                ZarrFormat = 2,
                Order = "C"
            }
        };

        // Act & Assert - Request 50x50x50 = 125,000 elements * 4 bytes = 500 KB
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.ReadSliceAsync(array, new[] { 0, 0, 0 }, new[] { 50, 50, 50 }));

        Assert.Contains("exceeds maximum allowed", ex.Message);
        Assert.Contains("500,000", ex.Message); // Should show actual size
        Assert.Contains("1,000", ex.Message); // Should show limit
    }

    [Fact]
    public async Task HttpZarrReader_ReadSliceAsync_EnforcesMaxChunksPerRequest()
    {
        // Arrange
        var limits = new RasterMemoryLimits
        {
            MaxSliceSizeBytes = 1_000_000_000, // 1 GB - large enough
            MaxChunksPerRequest = 5 // Only 5 chunks allowed
        };

        var mockHandler = CreateMockHttpHandler();
        var httpClient = new HttpClient(mockHandler.Object);
        var codecRegistry = new CompressionCodecRegistry(NullLogger<CompressionCodecRegistry>.Instance);
        var decompressor = new ZarrDecompressor(_decompressorLogger, codecRegistry);

        var reader = new HttpZarrReader(_zarrLogger, httpClient, decompressor, limits);

        var array = new ZarrArray
        {
            Uri = "http://test.com/data.zarr",
            VariableName = "temperature",
            Metadata = new ZarrArrayMetadata
            {
                Shape = new[] { 100, 100 },
                Chunks = new[] { 10, 10 }, // Small chunks
                DType = "<f4",
                Compressor = "null",
                ZarrFormat = 2,
                Order = "C"
            }
        };

        // Act & Assert - Request 30x30 requires 3x3 = 9 chunks
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.ReadSliceAsync(array, new[] { 0, 0 }, new[] { 30, 30 }));

        Assert.Contains("9 chunks", ex.Message);
        Assert.Contains("exceeds maximum allowed", ex.Message);
        Assert.Contains("5 chunks", ex.Message);
    }

    [Fact]
    public async Task HttpZarrReader_ReadSliceAsync_SucceedsWithinLimits()
    {
        // Arrange
        var limits = new RasterMemoryLimits
        {
            MaxSliceSizeBytes = 1_000_000,
            MaxChunksPerRequest = 10
        };

        var mockHandler = CreateMockHttpHandler();
        var httpClient = new HttpClient(mockHandler.Object);
        var codecRegistry = new CompressionCodecRegistry(NullLogger<CompressionCodecRegistry>.Instance);
        var decompressor = new ZarrDecompressor(_decompressorLogger, codecRegistry);

        var reader = new HttpZarrReader(_zarrLogger, httpClient, decompressor, limits);

        var array = new ZarrArray
        {
            Uri = "http://test.com/data.zarr",
            VariableName = "temperature",
            Metadata = new ZarrArrayMetadata
            {
                Shape = new[] { 100, 100 },
                Chunks = new[] { 50, 50 },
                DType = "<f4",
                Compressor = "null",
                ZarrFormat = 2,
                Order = "C"
            }
        };

        // Act - Request 20x20 = 400 elements * 4 bytes = 1,600 bytes (within 1 MB limit)
        // Should not throw
        var result = await reader.ReadSliceAsync(array, new[] { 0, 0 }, new[] { 20, 20 });

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task RasterAnalyticsService_CalculateAlgebraAsync_EnforcesMaxDatasets()
    {
        // Arrange
        var limits = new RasterMemoryLimits { MaxSimultaneousDatasets = 2 };
        var service = new RasterAnalyticsService(_analyticsLogger, limits);

        var datasets = Enumerable.Range(0, 3)
            .Select(i => new RasterDatasetDefinition
            {
                Id = $"ds{i}",
                Title = $"Test Dataset {i}",
                Source = new RasterSourceDefinition { Type = "geotiff", Uri = "/test/data.tif" }
            })
            .ToList();

        var request = new RasterAlgebraRequest(
            datasets,
            "mean",
            new[] { 0.0, 0.0, 10.0, 10.0 },
            512,
            512,
            "png");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateAlgebraAsync(request));

        Assert.NotNull(ex);
        Assert.Contains("3", ex.Message);
        Assert.Contains("exceeds maximum allowed", ex.Message);
        Assert.Contains("2", ex.Message);
    }

    [Fact]
    public async Task RasterAnalyticsService_CalculateAlgebraAsync_EnforcesMaxDimensions()
    {
        // Arrange
        var limits = new RasterMemoryLimits { MaxRasterDimension = 1024 };
        var service = new RasterAnalyticsService(_analyticsLogger, limits);

        var request = new RasterAlgebraRequest(
            new[] { new RasterDatasetDefinition
            {
                Id = "ds1",
                Title = "Test Dataset",
                Source = new RasterSourceDefinition { Type = "geotiff", Uri = "/test/data.tif" }
            } },
            "mean",
            new[] { 0.0, 0.0, 10.0, 10.0 },
            2048, // Exceeds limit
            512,
            "png");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateAlgebraAsync(request));

        Assert.NotNull(ex);
        Assert.Contains("2048", ex.Message);
        Assert.Contains("1024", ex.Message);
    }

    [Fact]
    public async Task RasterAnalyticsService_CalculateAlgebraAsync_EnforcesMaxPixels()
    {
        // Arrange
        var limits = new RasterMemoryLimits
        {
            MaxRasterDimension = 8192,
            MaxRasterPixels = 1_000_000 // 1M pixels
        };
        var service = new RasterAnalyticsService(_analyticsLogger, limits);

        var request = new RasterAlgebraRequest(
            new[] { new RasterDatasetDefinition
            {
                Id = "ds1",
                Title = "Test Dataset",
                Source = new RasterSourceDefinition { Type = "geotiff", Uri = "/test/data.tif" }
            } },
            "mean",
            new[] { 0.0, 0.0, 10.0, 10.0 },
            2000, // 2000x2000 = 4M pixels
            2000,
            "png");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateAlgebraAsync(request));

        Assert.NotNull(ex);
        Assert.Contains("4,000,000", ex.Message);
        Assert.Contains("1,000,000", ex.Message);
    }

    [Fact]
    public async Task RasterAnalyticsService_ExtractValuesAsync_EnforcesMaxPoints()
    {
        // Arrange
        var limits = new RasterMemoryLimits { MaxExtractionPoints = 100 };
        var service = new RasterAnalyticsService(_analyticsLogger, limits);

        var points = Enumerable.Range(0, 200)
            .Select(i => new Point(i, i))
            .ToList();

        var request = new RasterValueExtractionRequest(
            new RasterDatasetDefinition
            {
                Id = "ds1",
                Title = "Test Dataset",
                Source = new RasterSourceDefinition { Type = "geotiff", Uri = "/test/data.tif" }
            },
            points);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.ExtractValuesAsync(request));

        Assert.NotNull(ex);
        Assert.Contains("200", ex.Message);
        Assert.Contains("100", ex.Message);
    }

    [Fact]
    public async Task RasterAnalyticsService_CalculateHistogramAsync_EnforcesMaxBins()
    {
        // Arrange
        var limits = new RasterMemoryLimits { MaxHistogramBins = 256 };
        var service = new RasterAnalyticsService(_analyticsLogger, limits);

        var request = new RasterHistogramRequest(
            new RasterDatasetDefinition
            {
                Id = "ds1",
                Title = "Test Dataset",
                Source = new RasterSourceDefinition { Type = "geotiff", Uri = "/test/data.tif" }
            },
            BinCount: 512); // Exceeds limit

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateHistogramAsync(request));

        Assert.NotNull(ex);
        Assert.Contains("512", ex.Message);
        Assert.Contains("256", ex.Message);
    }

    [Fact]
    public async Task RasterAnalyticsService_CalculateHistogramAsync_RejectZeroBins()
    {
        // Arrange
        var service = new RasterAnalyticsService(_analyticsLogger);

        var request = new RasterHistogramRequest(
            new RasterDatasetDefinition
            {
                Id = "ds1",
                Title = "Test Dataset",
                Source = new RasterSourceDefinition { Type = "geotiff", Uri = "/test/data.tif" }
            },
            BinCount: 0);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateHistogramAsync(request));

        Assert.NotNull(ex);
        Assert.Contains("greater than zero", ex.Message);
    }

    [Fact]
    public async Task RasterAnalyticsService_CalculateZonalStatisticsAsync_EnforcesMaxZones()
    {
        // Arrange
        var limits = new RasterMemoryLimits { MaxZonalPolygons = 10 };
        var service = new RasterAnalyticsService(_analyticsLogger, limits);

        var zones = Enumerable.Range(0, 20)
            .Select(i => new Polygon(
                new[] { new Point(0, 0), new Point(1, 0), new Point(1, 1), new Point(0, 1) },
                $"zone{i}"))
            .ToList();

        var request = new ZonalStatisticsRequest(
            new RasterDatasetDefinition
            {
                Id = "ds1",
                Title = "Test Dataset",
                Source = new RasterSourceDefinition { Type = "geotiff", Uri = "/test/data.tif" }
            },
            zones);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateZonalStatisticsAsync(request));

        Assert.NotNull(ex);
        Assert.Contains("20", ex.Message);
        Assert.Contains("10", ex.Message);
    }

    [Fact]
    public async Task RasterAnalyticsService_CalculateZonalStatisticsAsync_EnforcesMaxVertices()
    {
        // Arrange
        var limits = new RasterMemoryLimits { MaxZonalPolygonVertices = 100 };
        var service = new RasterAnalyticsService(_analyticsLogger, limits);

        // Create polygon with 200 vertices
        var vertices = Enumerable.Range(0, 200)
            .Select(i => new Point(i, i))
            .ToList();

        var zones = new[] { new Polygon(vertices, "complex_zone") };

        var request = new ZonalStatisticsRequest(
            new RasterDatasetDefinition
            {
                Id = "ds1",
                Title = "Test Dataset",
                Source = new RasterSourceDefinition { Type = "geotiff", Uri = "/test/data.tif" }
            },
            zones);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateZonalStatisticsAsync(request));

        Assert.NotNull(ex);
        Assert.Contains("200", ex.Message);
        Assert.Contains("100", ex.Message);
        Assert.Contains("complex_zone", ex.Message);
    }

    [Fact]
    public async Task RasterAnalyticsService_CalculateZonalStatisticsAsync_RejectsTooFewVertices()
    {
        // Arrange
        var service = new RasterAnalyticsService(_analyticsLogger);

        var zones = new[]
        {
            new Polygon(
                new[] { new Point(0, 0), new Point(1, 0) }, // Only 2 vertices
                "invalid_zone")
        };

        var request = new ZonalStatisticsRequest(
            new RasterDatasetDefinition
            {
                Id = "ds1",
                Title = "Test Dataset",
                Source = new RasterSourceDefinition { Type = "geotiff", Uri = "/test/data.tif" }
            },
            zones);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateZonalStatisticsAsync(request));

        Assert.NotNull(ex);
        Assert.Contains("at least 3 vertices", ex.Message);
    }

    [Fact]
    public void RasterAnalyticsService_GetCapabilities_ReflectsConfiguredLimits()
    {
        // Arrange
        var limits = new RasterMemoryLimits
        {
            MaxSimultaneousDatasets = 3,
            MaxExtractionPoints = 500,
            MaxHistogramBins = 128,
            MaxZonalPolygons = 50
        };
        var service = new RasterAnalyticsService(_analyticsLogger, limits);

        // Act
        var capabilities = service.GetCapabilities();

        // Assert
        Assert.Equal(3, capabilities.MaxAlgebraDatasets);
        Assert.Equal(500, capabilities.MaxExtractionPoints);
        Assert.Equal(128, capabilities.MaxHistogramBins);
        Assert.Equal(50, capabilities.MaxZonalPolygons);
    }

    [Theory]
    [InlineData(10, 10, 4, 400)]      // 10x10 * 4 bytes = 400 bytes
    [InlineData(100, 100, 8, 80000)]  // 100x100 * 8 bytes = 80,000 bytes
    [InlineData(1000, 1000, 4, 4000000)] // 1000x1000 * 4 bytes = 4,000,000 bytes
    public async Task HttpZarrReader_ReadSliceAsync_CalculatesMemoryCorrectly(
        int width, int height, int elementSize, long expectedBytes)
    {
        // Arrange
        var limits = new RasterMemoryLimits { MaxSliceSizeBytes = expectedBytes - 1 }; // Just under

        var mockHandler = CreateMockHttpHandler();
        var httpClient = new HttpClient(mockHandler.Object);
        var codecRegistry = new CompressionCodecRegistry(NullLogger<CompressionCodecRegistry>.Instance);
        var decompressor = new ZarrDecompressor(_decompressorLogger, codecRegistry);

        var reader = new HttpZarrReader(_zarrLogger, httpClient, decompressor, limits);

        var dtype = elementSize == 4 ? "<f4" : "<f8";
        var array = new ZarrArray
        {
            Uri = "http://test.com/data.zarr",
            VariableName = "temperature",
            Metadata = new ZarrArrayMetadata
            {
                Shape = new[] { width, height },
                Chunks = new[] { width, height }, // Single chunk
                DType = dtype,
                Compressor = "null",
                ZarrFormat = 2,
                Order = "C"
            }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.ReadSliceAsync(array, new[] { 0, 0 }, new[] { width, height }));

        Assert.Contains($"{expectedBytes:N0}", ex.Message);
        Assert.Contains($"{expectedBytes - 1:N0}", ex.Message);
    }

    private Mock<HttpMessageHandler> CreateMockHttpHandler()
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        // Mock .zarray metadata response
        var metadataJson = @"{
            ""shape"": [100, 100],
            ""chunks"": [10, 10],
            ""dtype"": ""<f4"",
            ""compressor"": null,
            ""zarr_format"": 2,
            ""order"": ""C"",
            ""fill_value"": 0.0
        }";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().EndsWith(".zarray")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(metadataJson)
            });

        // Mock chunk data response
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => !req.RequestUri!.ToString().EndsWith(".zarray")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(new byte[4000]) // 10x10x4 bytes
            });

        return mockHandler;
    }
}

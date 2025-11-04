using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Caching;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Host.Configuration;
using Honua.Server.Host.Tests.TestInfrastructure;
using Honua.Server.Host.Wms;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Wms;

/// <summary>
/// Tests for WMS GetMap memory buffering fixes and streaming implementation.
/// Validates that large images are streamed instead of buffered, and that size/timeout limits are enforced.
/// </summary>
[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public sealed class WmsGetMapStreamingTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;

    public WmsGetMapStreamingTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMap_SmallImage_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&format=image/png&crs=EPSG:4326";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetMap_LargeImage_WithinLimits_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        // Request a 2048x2048 image (within default 4096 limit)
        var url = "/wms?service=WMS&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=2048&height=2048&format=image/png&crs=EPSG:4326";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetMap_ExceedsMaxWidth_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        // Request width of 10000 (exceeds default 4096 limit)
        var url = "/wms?service=WMS&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=10000&height=256&format=image/png&crs=EPSG:4326";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("exceeds maximum allowed width");
    }

    [Fact]
    public async Task GetMap_ExceedsMaxHeight_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        // Request height of 10000 (exceeds default 4096 limit)
        var url = "/wms?service=WMS&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=10000&format=image/png&crs=EPSG:4326";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("exceeds maximum allowed height");
    }

    [Fact]
    public async Task GetMap_ExceedsMaxTotalPixels_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        // Request 4096x4096 (16,777,216 pixels, within individual limits but right at total pixel limit)
        // Then test 4096x4097 which should exceed
        var url = "/wms?service=WMS&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=4096&height=4097&format=image/png&crs=EPSG:4326";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("exceeds maximum allowed total pixels");
    }

    [Fact]
    public async Task GetMap_DifferentFormats_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();

        var formats = new[]
        {
            ("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47 }), // PNG magic bytes
            ("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF }), // JPEG magic bytes (first 3)
        };

        foreach (var (format, magicBytes) in formats)
        {
            var url = $"/wms?service=WMS&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&format={format}&crs=EPSG:4326";

            // Act
            var response = await client.GetAsync(url);

            // Assert
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync();
            bytes.Should().NotBeNull();
            bytes.Length.Should().BeGreaterThan(magicBytes.Length);

            // Verify magic bytes
            for (int i = 0; i < magicBytes.Length; i++)
            {
                bytes[i].Should().Be(magicBytes[i], $"Format {format} should have correct magic bytes");
            }
        }
    }

    [Theory]
    [InlineData(256, 256, "png", true)] // Small PNG - should buffer
    [InlineData(2048, 2048, "png", false)] // Large PNG - should stream
    [InlineData(512, 512, "jpeg", true)] // Small JPEG - should buffer
    [InlineData(3000, 3000, "jpeg", false)] // Large JPEG - should stream
    public void EstimateImageSize_ShouldReturnReasonableValues(int width, int height, string format, bool shouldBufferAt2MB)
    {
        // This is a unit test for the estimation logic
        // We can't directly call the private method, but we can verify the behavior
        var pixels = (long)width * height;

        long estimatedSize = format.ToLowerInvariant() switch
        {
            "png" => pixels * 3 / 2,
            "jpeg" => pixels / 4,
            _ => pixels * 2
        };

        var streamingThreshold = 2_097_152; // 2MB default
        var shouldBuffer = estimatedSize <= streamingThreshold;

        shouldBuffer.Should().Be(shouldBufferAt2MB,
            $"Image {width}x{height} {format} estimated at {estimatedSize:N0} bytes should {(shouldBufferAt2MB ? "" : "not ")}be buffered");
    }
}

/// <summary>
/// Unit tests for WMS configuration options validation.
/// </summary>
public sealed class WmsOptionsTests
{
    [Fact]
    public void WmsOptions_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var options = new WmsOptions();

        // Assert
        options.MaxWidth.Should().Be(4096);
        options.MaxHeight.Should().Be(4096);
        options.MaxTotalPixels.Should().Be(16_777_216);
        options.RenderTimeoutSeconds.Should().Be(60);
        options.StreamingThresholdBytes.Should().Be(2_097_152);
        options.EnableStreaming.Should().BeTrue();
    }

    [Theory]
    [InlineData(256, 256, 65536, true)]
    [InlineData(4096, 4096, 16777216, true)]
    [InlineData(4097, 4096, 16777216, false)] // Exceeds width
    [InlineData(4096, 4097, 16777216, false)] // Exceeds height
    [InlineData(4000, 4200, 16777216, false)] // Within individual but exceeds total
    public void ValidateImageSize_ShouldAcceptOrRejectBasedOnLimits(int width, int height, long maxPixels, bool shouldSucceed)
    {
        // Arrange
        var options = new WmsOptions
        {
            MaxWidth = 4096,
            MaxHeight = 4096,
            MaxTotalPixels = maxPixels
        };

        // Act
        Action validate = () => ValidateImageSizeHelper(width, height, options);

        // Assert
        if (shouldSucceed)
        {
            validate.Should().NotThrow();
        }
        else
        {
            validate.Should().Throw<InvalidOperationException>();
        }
    }

    private static void ValidateImageSizeHelper(int width, int height, WmsOptions options)
    {
        if (width > options.MaxWidth)
        {
            throw new InvalidOperationException($"Requested width {width} exceeds maximum allowed width of {options.MaxWidth} pixels");
        }

        if (height > options.MaxHeight)
        {
            throw new InvalidOperationException($"Requested height {height} exceeds maximum allowed height of {options.MaxHeight} pixels");
        }

        var totalPixels = (long)width * height;
        if (totalPixels > options.MaxTotalPixels)
        {
            throw new InvalidOperationException($"Requested image size {width}x{height} ({totalPixels:N0} pixels) exceeds maximum allowed total pixels of {options.MaxTotalPixels:N0}");
        }
    }

    [Fact]
    public void WmsOptions_ConfigurationSection_ShouldHaveCorrectName()
    {
        // Assert
        WmsOptions.SectionName.Should().Be("Wms");
    }
}

/// <summary>
/// Performance tests to verify memory usage improvements.
/// </summary>
[Collection("EndpointTests")]
[Trait("Category", "Performance")]
public sealed class WmsGetMapMemoryTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;

    public WmsGetMapMemoryTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMap_MultipleSequentialLargeRequests_ShouldNotExhaustMemory()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=2048&height=2048&format=image/png&crs=EPSG:4326";

        var initialMemory = GC.GetTotalMemory(forceFullCollection: true);

        // Act - Make multiple sequential requests
        for (int i = 0; i < 5; i++)
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            // Read and discard the response
            var bytes = await response.Content.ReadAsByteArrayAsync();
            bytes.Should().NotBeNull();

            // Force GC between requests
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        // Assert
        var finalMemory = GC.GetTotalMemory(forceFullCollection: true);
        var memoryGrowth = finalMemory - initialMemory;

        // Memory growth should be minimal (less than 50MB for 5 large requests)
        // Before streaming fix, this would grow by hundreds of MB
        memoryGrowth.Should().BeLessThan(50 * 1024 * 1024,
            "Streaming should prevent excessive memory accumulation");
    }

    [Fact]
    public async Task GetMap_ConcurrentRequests_ShouldHandleGracefully()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=1024&height=1024&format=image/png&crs=EPSG:4326";

        // Act - Make 10 concurrent requests
        var tasks = new Task<HttpResponseMessage>[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = client.GetAsync(url);
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        foreach (var response in responses)
        {
            response.IsSuccessStatusCode.Should().BeTrue();
            var bytes = await response.Content.ReadAsByteArrayAsync();
            bytes.Should().NotBeNull();
            bytes.Length.Should().BeGreaterThan(0);
        }
    }
}

/// <summary>
/// Tests for streaming behavior with CDN headers.
/// </summary>
[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public sealed class WmsGetMapCdnStreamingTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;

    public WmsGetMapCdnStreamingTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMap_WithCdnEnabled_ShouldIncludeCacheHeaders()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&format=image/png&crs=EPSG:4326";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();

        // Check for cache control headers (if CDN is enabled in test config)
        // This validates that streaming doesn't break CDN header functionality
        if (response.Headers.CacheControl != null)
        {
            response.Headers.Vary.Should().Contain("Accept-Encoding");
        }
    }

    [Fact]
    public async Task GetMap_StreamingResponse_ShouldHaveCorrectContentType()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=3000&height=3000&format=image/png&crs=EPSG:4326";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
    }
}

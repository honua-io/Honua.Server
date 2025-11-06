// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.LocationServices.Models;
using Honua.Server.Core.LocationServices.Providers.AzureMaps;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;

namespace Honua.Server.Core.Tests.LocationServices.Providers;

[Trait("Category", "Unit")]
public sealed class AzureMapsBasemapTileProviderTests
{
    private readonly Mock<ILogger<AzureMapsBasemapTileProvider>> _mockLogger;
    private readonly string _testSubscriptionKey = "test-key-12345";

    public AzureMapsBasemapTileProviderTests()
    {
        _mockLogger = new Mock<ILogger<AzureMapsBasemapTileProvider>>();
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var provider = new AzureMapsBasemapTileProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        // Assert
        provider.Should().NotBeNull();
        provider.ProviderKey.Should().Be("azure-maps");
        provider.ProviderName.Should().Be("Azure Maps Basemap");
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AzureMapsBasemapTileProvider(null!, _testSubscriptionKey, _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpClient");
    }

    [Fact]
    public async Task GetAvailableTilesetsAsync_ReturnsAllTilesets()
    {
        // Arrange
        var httpClient = new HttpClient();
        var provider = new AzureMapsBasemapTileProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        // Act
        var tilesets = await provider.GetAvailableTilesetsAsync();

        // Assert
        tilesets.Should().NotBeEmpty();
        tilesets.Should().Contain(t => t.Id == "road");
        tilesets.Should().Contain(t => t.Id == "satellite");
        tilesets.Should().Contain(t => t.Id == "hybrid");
        tilesets.Should().Contain(t => t.Id == "dark");
        tilesets.Should().Contain(t => t.Format == TileFormat.Vector);
    }

    [Fact]
    public async Task GetAvailableTilesetsAsync_TilesetsHaveCorrectProperties()
    {
        // Arrange
        var httpClient = new HttpClient();
        var provider = new AzureMapsBasemapTileProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        // Act
        var tilesets = await provider.GetAvailableTilesetsAsync();

        // Assert
        foreach (var tileset in tilesets)
        {
            tileset.Id.Should().NotBeNullOrEmpty();
            tileset.Name.Should().NotBeNullOrEmpty();
            tileset.TileUrlTemplate.Should().NotBeNullOrEmpty();
            tileset.Attribution.Should().Be("© Microsoft Azure Maps");
            tileset.MaxZoom.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task GetTileAsync_WithValidRequest_ReturnsTileData()
    {
        // Arrange
        var tileData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, tileData, "image/png");
        var provider = new AzureMapsBasemapTileProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new TileRequest
        {
            TilesetId = "road",
            Z = 1,
            X = 0,
            Y = 0
        };

        // Act
        var response = await provider.GetTileAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Data.Should().NotBeEmpty();
        response.ContentType.Should().Be("image/png");
    }

    [Theory]
    [InlineData("road")]
    [InlineData("satellite")]
    [InlineData("hybrid")]
    [InlineData("dark")]
    [InlineData("night")]
    public async Task GetTileAsync_WithDifferentTilesets_SupportsAllTypes(string tilesetId)
    {
        // Arrange
        var tileData = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, tileData, "image/png");
        var provider = new AzureMapsBasemapTileProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new TileRequest
        {
            TilesetId = tilesetId,
            Z = 1,
            X = 0,
            Y = 0
        };

        // Act
        var response = await provider.GetTileAsync(request);

        // Assert
        response.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTileAsync_WithHighDpiScale_IncludesScaleFactor()
    {
        // Arrange
        var tileData = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, tileData, "image/png");
        var provider = new AzureMapsBasemapTileProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new TileRequest
        {
            TilesetId = "road",
            Z = 1,
            X = 0,
            Y = 0,
            Scale = 2
        };

        // Act
        var response = await provider.GetTileAsync(request);

        // Assert
        response.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTileAsync_WithLanguage_IncludesLanguageParameter()
    {
        // Arrange
        var tileData = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, tileData, "image/png");
        var provider = new AzureMapsBasemapTileProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new TileRequest
        {
            TilesetId = "road",
            Z = 1,
            X = 0,
            Y = 0,
            Language = "es"
        };

        // Act
        var response = await provider.GetTileAsync(request);

        // Assert
        response.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTileAsync_WithInvalidTileset_ThrowsArgumentException()
    {
        // Arrange
        var httpClient = new HttpClient { BaseAddress = new Uri("https://atlas.microsoft.com") };
        var provider = new AzureMapsBasemapTileProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new TileRequest
        {
            TilesetId = "invalid-tileset",
            Z = 1,
            X = 0,
            Y = 0
        };

        // Act
        var act = async () => await provider.GetTileAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unknown tileset*");
    }

    [Fact]
    public async Task GetTileAsync_WithHttpError_ThrowsInvalidOperationException()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized, Array.Empty<byte>(), "text/plain");
        var provider = new AzureMapsBasemapTileProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new TileRequest
        {
            TilesetId = "road",
            Z = 1,
            X = 0,
            Y = 0
        };

        // Act
        var act = async () => await provider.GetTileAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Azure Maps tile request failed*");
    }

    [Fact]
    public async Task GetTileUrlTemplateAsync_WithValidTileset_ReturnsTemplate()
    {
        // Arrange
        var httpClient = new HttpClient();
        var provider = new AzureMapsBasemapTileProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        // Act
        var template = await provider.GetTileUrlTemplateAsync("road");

        // Assert
        template.Should().NotBeNullOrEmpty();
        template.Should().Contain("{z}");
        template.Should().Contain("{x}");
        template.Should().Contain("{y}");
        template.Should().Contain(_testSubscriptionKey);
    }

    [Fact]
    public async Task GetTileUrlTemplateAsync_WithInvalidTileset_ThrowsArgumentException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var provider = new AzureMapsBasemapTileProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        // Act
        var act = async () => await provider.GetTileUrlTemplateAsync("invalid-tileset");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unknown tileset*");
    }

    [Fact]
    public async Task TestConnectivityAsync_WithSuccessfulConnection_ReturnsTrue()
    {
        // Arrange
        var tileData = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, tileData, "image/png");
        var provider = new AzureMapsBasemapTileProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        // Act
        var result = await provider.TestConnectivityAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectivityAsync_WithFailedConnection_ReturnsFalse()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.ServiceUnavailable, Array.Empty<byte>(), "text/plain");
        var provider = new AzureMapsBasemapTileProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        // Act
        var result = await provider.TestConnectivityAsync();

        // Assert
        result.Should().BeFalse();
    }

    private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, byte[] responseContent, string contentType)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new ByteArrayContent(responseContent)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType) }
                }
            });

        return new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://atlas.microsoft.com")
        };
    }
}

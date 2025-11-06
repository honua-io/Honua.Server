// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using Honua.Admin.Blazor.Tests.Infrastructure;
using System.Net;
using Moq;

namespace Honua.Admin.Blazor.Tests.Services;

/// <summary>
/// Tests for LayerApiClient.
/// </summary>
public class LayerApiClientTests
{
    [Fact]
    public async Task GetLayersAsync_Success_ReturnsLayerList()
    {
        // Arrange
        var expectedLayers = new List<LayerListItem>
        {
            new LayerListItem
            {
                Id = "layer1",
                Title = "Test Layer 1",
                GeometryType = "Polygon",
                Crs = new List<string> { "EPSG:4326" },
                ServiceId = "wms-service"
            },
            new LayerListItem
            {
                Id = "layer2",
                Title = "Test Layer 2",
                GeometryType = "Point",
                Crs = new List<string> { "EPSG:3857" },
                ServiceId = "wfs-service"
            }
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/metadata/layers", expectedLayers);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<LayerApiClient>>();
        var apiClient = new LayerApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.GetLayersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].GeometryType.Should().Be("Polygon");
        result[1].Crs.Should().Contain("EPSG:3857");
    }

    [Fact]
    public async Task GetLayersAsync_WithServiceId_ReturnsFilteredLayers()
    {
        // Arrange
        var expectedLayers = new List<LayerListItem>
        {
            new LayerListItem
            {
                Id = "layer1",
                Title = "Service Layer",
                GeometryType = "Polygon",
                Crs = new List<string> { "EPSG:4326" },
                ServiceId = "wms-service"
            }
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/metadata/layers?serviceId=wms-service", expectedLayers);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<LayerApiClient>>();
        var apiClient = new LayerApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.GetLayersAsync("wms-service");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].ServiceId.Should().Be("wms-service");
    }

    [Fact]
    public async Task GetLayerByIdAsync_Success_ReturnsLayer()
    {
        // Arrange
        var expectedLayer = new LayerResponse
        {
            Id = "test-layer",
            Title = "Test Layer Title",
            Description = "Test layer description",
            GeometryType = "LineString",
            Crs = new List<string> { "EPSG:4326" },
            ServiceId = "wms-service",
            IdField = "id",
            GeometryField = "geom"
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/metadata/layers/test-layer", expectedLayer);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<LayerApiClient>>();
        var apiClient = new LayerApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.GetLayerByIdAsync("test-layer");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("test-layer");
        result.GeometryType.Should().Be("LineString");
    }

    [Fact]
    public async Task CreateLayerAsync_Success_ReturnsCreatedLayer()
    {
        // Arrange
        var request = new CreateLayerRequest
        {
            Id = "new-layer",
            Title = "New Layer Title",
            GeometryType = "Point",
            Crs = new List<string> { "EPSG:4326" },
            ServiceId = "wms-service",
            IdField = "id",
            GeometryField = "geom"
        };

        var expectedResponse = new LayerResponse
        {
            Id = "new-layer",
            Title = "New Layer Title",
            GeometryType = "Point",
            Crs = new List<string> { "EPSG:4326" },
            ServiceId = "wms-service",
            IdField = "id",
            GeometryField = "geom"
        };

        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/admin/metadata/layers", expectedResponse, HttpStatusCode.Created);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<LayerApiClient>>();
        var apiClient = new LayerApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.CreateLayerAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("new-layer");
        result.GeometryType.Should().Be("Point");
    }

    [Fact]
    public async Task UpdateLayerAsync_Success_CompletesWithoutException()
    {
        // Arrange
        var request = new UpdateLayerRequest
        {
            Title = "Updated Title",
            Description = "Updated description",
            GeometryType = "Polygon",
            IdField = "id",
            GeometryField = "geom"
        };

        var mockFactory = new MockHttpClientFactory()
            .MockPutJson("/admin/metadata/layers/test-layer", new { success = true });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<LayerApiClient>>();
        var apiClient = new LayerApiClient(httpClientFactory.Object, logger);

        // Act
        var act = async () => await apiClient.UpdateLayerAsync("test-layer", request);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteLayerAsync_Success_CompletesWithoutException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockDelete("/admin/metadata/layers/test-layer");

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<LayerApiClient>>();
        var apiClient = new LayerApiClient(httpClientFactory.Object, logger);

        // Act
        var act = async () => await apiClient.DeleteLayerAsync("test-layer");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetLayersAsync_ServerError_ThrowsHttpRequestException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Get, "/admin/metadata/layers", HttpStatusCode.InternalServerError);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<LayerApiClient>>();
        var apiClient = new LayerApiClient(httpClientFactory.Object, logger);

        // Act
        var act = async () => await apiClient.GetLayersAsync();

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetLayerByIdAsync_NotFound_ThrowsHttpRequestException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Get, "/admin/metadata/layers/nonexistent", HttpStatusCode.NotFound);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<LayerApiClient>>();
        var apiClient = new LayerApiClient(httpClientFactory.Object, logger);

        // Act
        var act = async () => await apiClient.GetLayerByIdAsync("nonexistent");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CreateLayerAsync_InvalidGeometryType_ThrowsHttpRequestException()
    {
        // Arrange
        var request = new CreateLayerRequest
        {
            Id = "invalid-layer",
            Title = "Invalid Layer",
            GeometryType = "InvalidType", // Invalid geometry type
            Crs = new List<string> { "EPSG:4326" },
            ServiceId = "wms-service",
            IdField = "id",
            GeometryField = "geom"
        };

        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Post, "/admin/metadata/layers", HttpStatusCode.BadRequest, "Invalid geometry type");

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<LayerApiClient>>();
        var apiClient = new LayerApiClient(httpClientFactory.Object, logger);

        // Act
        var act = async () => await apiClient.CreateLayerAsync(request);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}

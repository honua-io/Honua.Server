// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using Honua.Admin.Blazor.Tests.Infrastructure;
using System.Net;

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
                Name = "Test Layer 1",
                GeometryType = "Polygon",
                Crs = "EPSG:4326",
                ServiceId = "wms-service"
            },
            new LayerListItem
            {
                Id = "layer2",
                Name = "Test Layer 2",
                GeometryType = "Point",
                Crs = "EPSG:3857",
                ServiceId = "wfs-service"
            }
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/metadata/layers", expectedLayers);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new LayerApiClient(httpClient);

        // Act
        var result = await apiClient.GetLayersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].GeometryType.Should().Be("Polygon");
        result[1].Crs.Should().Be("EPSG:3857");
    }

    [Fact]
    public async Task GetLayersByServiceAsync_Success_ReturnsFilteredLayers()
    {
        // Arrange
        var expectedLayers = new List<LayerListItem>
        {
            new LayerListItem
            {
                Id = "layer1",
                Name = "Service Layer",
                GeometryType = "Polygon",
                Crs = "EPSG:4326",
                ServiceId = "wms-service"
            }
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/metadata/layers?serviceId=wms-service", expectedLayers);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new LayerApiClient(httpClient);

        // Act
        var result = await apiClient.GetLayersByServiceAsync("wms-service");

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
            Name = "Test Layer",
            Title = "Test Layer Title",
            Abstract = "Test layer description",
            GeometryType = "LineString",
            Crs = "EPSG:4326",
            ServiceId = "wms-service",
            BoundingBox = new BoundingBox
            {
                MinX = -180,
                MinY = -90,
                MaxX = 180,
                MaxY = 90
            }
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/metadata/layers/test-layer", expectedLayer);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new LayerApiClient(httpClient);

        // Act
        var result = await apiClient.GetLayerByIdAsync("test-layer");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("test-layer");
        result.GeometryType.Should().Be("LineString");
        result.BoundingBox.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateLayerAsync_Success_ReturnsCreatedLayer()
    {
        // Arrange
        var request = new CreateLayerRequest
        {
            Id = "new-layer",
            Name = "New Layer",
            Title = "New Layer Title",
            GeometryType = "Point",
            Crs = "EPSG:4326",
            ServiceId = "wms-service"
        };

        var expectedResponse = new LayerResponse
        {
            Id = "new-layer",
            Name = "New Layer",
            Title = "New Layer Title",
            GeometryType = "Point",
            Crs = "EPSG:4326",
            ServiceId = "wms-service"
        };

        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/admin/metadata/layers", expectedResponse, HttpStatusCode.Created);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new LayerApiClient(httpClient);

        // Act
        var result = await apiClient.CreateLayerAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("new-layer");
        result.GeometryType.Should().Be("Point");
    }

    [Fact]
    public async Task UpdateLayerAsync_Success_ReturnsUpdatedLayer()
    {
        // Arrange
        var request = new UpdateLayerRequest
        {
            Name = "Updated Layer",
            Title = "Updated Title",
            Abstract = "Updated description"
        };

        var expectedResponse = new LayerResponse
        {
            Id = "test-layer",
            Name = "Updated Layer",
            Title = "Updated Title",
            Abstract = "Updated description",
            GeometryType = "Polygon",
            Crs = "EPSG:4326",
            ServiceId = "wms-service"
        };

        var mockFactory = new MockHttpClientFactory()
            .MockPutJson("/admin/metadata/layers/test-layer", expectedResponse);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new LayerApiClient(httpClient);

        // Act
        var result = await apiClient.UpdateLayerAsync("test-layer", request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Layer");
        result.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task DeleteLayerAsync_Success_CompletesWithoutException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockDelete("/admin/metadata/layers/test-layer");

        var httpClient = mockFactory.CreateClient();
        var apiClient = new LayerApiClient(httpClient);

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

        var httpClient = mockFactory.CreateClient();
        var apiClient = new LayerApiClient(httpClient);

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

        var httpClient = mockFactory.CreateClient();
        var apiClient = new LayerApiClient(httpClient);

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
            Name = "Invalid Layer",
            GeometryType = "InvalidType", // Invalid geometry type
            Crs = "EPSG:4326",
            ServiceId = "wms-service"
        };

        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Post, "/admin/metadata/layers", HttpStatusCode.BadRequest, "Invalid geometry type");

        var httpClient = mockFactory.CreateClient();
        var apiClient = new LayerApiClient(httpClient);

        // Act
        var act = async () => await apiClient.CreateLayerAsync(request);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}

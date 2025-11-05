// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using Honua.Admin.Blazor.Tests.Infrastructure;
using System.Net;

namespace Honua.Admin.Blazor.Tests.Services;

/// <summary>
/// Tests for ServiceApiClient.
/// </summary>
public class ServiceApiClientTests
{
    [Fact]
    public async Task GetServicesAsync_Success_ReturnsServiceList()
    {
        // Arrange
        var expectedServices = new List<ServiceListItem>
        {
            new ServiceListItem
            {
                Id = "wms-service",
                Name = "WMS Service",
                ServiceType = "WMS",
                Enabled = true,
                LayerCount = 5,
                FolderId = null
            },
            new ServiceListItem
            {
                Id = "wfs-service",
                Name = "WFS Service",
                ServiceType = "WFS",
                Enabled = true,
                LayerCount = 3,
                FolderId = "folder1"
            }
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/metadata/services", expectedServices);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new ServiceApiClient(httpClient);

        // Act
        var result = await apiClient.GetServicesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].Id.Should().Be("wms-service");
        result[0].ServiceType.Should().Be("WMS");
        result[1].LayerCount.Should().Be(3);
    }

    [Fact]
    public async Task GetServiceByIdAsync_Success_ReturnsService()
    {
        // Arrange
        var expectedService = new ServiceResponse
        {
            Id = "test-service",
            Name = "Test Service",
            ServiceType = "WMS",
            Title = "Test WMS Service",
            Abstract = "Service for testing",
            Keywords = new List<string> { "test", "wms" },
            Enabled = true,
            FolderId = null,
            Layers = new List<string> { "layer1", "layer2" }
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/metadata/services/test-service", expectedService);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new ServiceApiClient(httpClient);

        // Act
        var result = await apiClient.GetServiceByIdAsync("test-service");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("test-service");
        result.ServiceType.Should().Be("WMS");
        result.Layers.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateServiceAsync_Success_ReturnsCreatedService()
    {
        // Arrange
        var request = new CreateServiceRequest
        {
            Id = "new-service",
            Name = "New Service",
            ServiceType = "WMTS",
            Title = "New WMTS Service",
            Abstract = "New service for testing",
            Keywords = new List<string> { "test", "wmts" },
            Enabled = true
        };

        var expectedResponse = new ServiceResponse
        {
            Id = "new-service",
            Name = "New Service",
            ServiceType = "WMTS",
            Title = "New WMTS Service",
            Abstract = "New service for testing",
            Enabled = true,
            Layers = new List<string>()
        };

        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/admin/metadata/services", expectedResponse, HttpStatusCode.Created);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new ServiceApiClient(httpClient);

        // Act
        var result = await apiClient.CreateServiceAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("new-service");
        result.ServiceType.Should().Be("WMTS");
    }

    [Fact]
    public async Task UpdateServiceAsync_Success_ReturnsUpdatedService()
    {
        // Arrange
        var request = new UpdateServiceRequest
        {
            Name = "Updated Service",
            Title = "Updated Title",
            Abstract = "Updated description",
            Enabled = false
        };

        var expectedResponse = new ServiceResponse
        {
            Id = "test-service",
            Name = "Updated Service",
            ServiceType = "WMS",
            Title = "Updated Title",
            Abstract = "Updated description",
            Enabled = false,
            Layers = new List<string>()
        };

        var mockFactory = new MockHttpClientFactory()
            .MockPutJson("/admin/metadata/services/test-service", expectedResponse);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new ServiceApiClient(httpClient);

        // Act
        var result = await apiClient.UpdateServiceAsync("test-service", request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Service");
        result.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteServiceAsync_Success_CompletesWithoutException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockDelete("/admin/metadata/services/test-service");

        var httpClient = mockFactory.CreateClient();
        var apiClient = new ServiceApiClient(httpClient);

        // Act
        var act = async () => await apiClient.DeleteServiceAsync("test-service");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetDashboardStatsAsync_Success_ReturnsStatistics()
    {
        // Arrange
        var expectedStats = new DashboardStats
        {
            TotalServices = 10,
            TotalLayers = 50,
            TotalFolders = 5,
            EnabledServices = 8,
            DisabledServices = 2
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/metadata/stats", expectedStats);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new ServiceApiClient(httpClient);

        // Act
        var result = await apiClient.GetDashboardStatsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalServices.Should().Be(10);
        result.TotalLayers.Should().Be(50);
        result.EnabledServices.Should().Be(8);
    }

    [Fact]
    public async Task GetServicesAsync_ServerError_ThrowsHttpRequestException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Get, "/admin/metadata/services", HttpStatusCode.InternalServerError);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new ServiceApiClient(httpClient);

        // Act
        var act = async () => await apiClient.GetServicesAsync();

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetServiceByIdAsync_NotFound_ThrowsHttpRequestException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Get, "/admin/metadata/services/nonexistent", HttpStatusCode.NotFound);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new ServiceApiClient(httpClient);

        // Act
        var act = async () => await apiClient.GetServiceByIdAsync("nonexistent");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CreateServiceAsync_DuplicateId_ThrowsHttpRequestException()
    {
        // Arrange
        var request = new CreateServiceRequest
        {
            Id = "existing-service",
            Name = "Duplicate Service",
            ServiceType = "WMS"
        };

        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Post, "/admin/metadata/services", HttpStatusCode.Conflict, "Service ID already exists");

        var httpClient = mockFactory.CreateClient();
        var apiClient = new ServiceApiClient(httpClient);

        // Act
        var act = async () => await apiClient.CreateServiceAsync(request);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task DeleteServiceAsync_ServiceWithLayers_ThrowsHttpRequestException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Delete, "/admin/metadata/services/service-with-layers",
                HttpStatusCode.BadRequest, "Cannot delete service with attached layers");

        var httpClient = mockFactory.CreateClient();
        var apiClient = new ServiceApiClient(httpClient);

        // Act
        var act = async () => await apiClient.DeleteServiceAsync("service-with-layers");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using Honua.Admin.Blazor.Tests.Infrastructure;
using System.Net;
using Moq;

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
                Title = "WMS Service",
                ServiceType = "WMS",
                LayerCount = 5,
                FolderId = null
            },
            new ServiceListItem
            {
                Id = "wfs-service",
                Title = "WFS Service",
                ServiceType = "WFS",
                LayerCount = 3,
                FolderId = "folder1"
            }
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/metadata/services", expectedServices);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<ServiceApiClient>>();
        var apiClient = new ServiceApiClient(httpClientFactory.Object, logger);

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
            Title = "Test WMS Service",
            ServiceType = "WMS",
            Description = "Service for testing",
            FolderId = null,
            LayerCount = 2
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/metadata/services/test-service", expectedService);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<ServiceApiClient>>();
        var apiClient = new ServiceApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.GetServiceByIdAsync("test-service");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("test-service");
        result.ServiceType.Should().Be("WMS");
        result.LayerCount.Should().Be(2);
    }

    [Fact]
    public async Task CreateServiceAsync_Success_ReturnsCreatedService()
    {
        // Arrange
        var request = new CreateServiceRequest
        {
            Id = "new-service",
            Title = "New WMTS Service",
            ServiceType = "WMTS",
            Description = "New service for testing"
        };

        var expectedResponse = new ServiceResponse
        {
            Id = "new-service",
            Title = "New WMTS Service",
            ServiceType = "WMTS",
            Description = "New service for testing",
            LayerCount = 0
        };

        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/admin/metadata/services", expectedResponse, HttpStatusCode.Created);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<ServiceApiClient>>();
        var apiClient = new ServiceApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.CreateServiceAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("new-service");
        result.ServiceType.Should().Be("WMTS");
    }

    [Fact]
    public async Task UpdateServiceAsync_Success_CompletesWithoutException()
    {
        // Arrange
        var request = new UpdateServiceRequest
        {
            Title = "Updated Title",
            ServiceType = "WMS",
            Description = "Updated description"
        };

        var mockFactory = new MockHttpClientFactory()
            .MockPutJson("/admin/metadata/services/test-service", new { success = true });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<ServiceApiClient>>();
        var apiClient = new ServiceApiClient(httpClientFactory.Object, logger);

        // Act
        var act = async () => await apiClient.UpdateServiceAsync("test-service", request);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteServiceAsync_Success_CompletesWithoutException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockDelete("/admin/metadata/services/test-service");

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<ServiceApiClient>>();
        var apiClient = new ServiceApiClient(httpClientFactory.Object, logger);

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
            ServiceCount = 10,
            LayerCount = 50,
            FolderCount = 5,
            DataSourceCount = 8
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/metadata/stats", expectedStats);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<ServiceApiClient>>();
        var apiClient = new ServiceApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.GetDashboardStatsAsync();

        // Assert
        result.Should().NotBeNull();
        result.ServiceCount.Should().Be(10);
        result.LayerCount.Should().Be(50);
        result.DataSourceCount.Should().Be(8);
    }

    [Fact]
    public async Task GetServicesAsync_ServerError_ThrowsHttpRequestException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Get, "/admin/metadata/services", HttpStatusCode.InternalServerError);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<ServiceApiClient>>();
        var apiClient = new ServiceApiClient(httpClientFactory.Object, logger);

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

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<ServiceApiClient>>();
        var apiClient = new ServiceApiClient(httpClientFactory.Object, logger);

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
            Title = "Duplicate Service",
            ServiceType = "WMS"
        };

        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Post, "/admin/metadata/services", HttpStatusCode.Conflict, "Service ID already exists");

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<ServiceApiClient>>();
        var apiClient = new ServiceApiClient(httpClientFactory.Object, logger);

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

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<ServiceApiClient>>();
        var apiClient = new ServiceApiClient(httpClientFactory.Object, logger);

        // Act
        var act = async () => await apiClient.DeleteServiceAsync("service-with-layers");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using System.Net;
using System.Net.Http.Json;

namespace Honua.Admin.Blazor.Tests.Integration;

/// <summary>
/// Integration tests for Service CRUD operations.
/// These tests demonstrate end-to-end testing of service management workflows.
/// Note: These require the backend API to be running.
/// </summary>
public class ServiceCrudIntegrationTests
{
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task CreateService_ValidRequest_ReturnsCreatedService()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();

        var createRequest = new CreateServiceRequest
        {
            Id = "test-service",
            Title = "Test WMS Service",
            ServiceType = "WMS",
            FolderId = null,
            Description = "Test service for integration testing"
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/services", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var service = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        service.Should().NotBeNull();
        service!.Id.Should().Be("test-service");
        service.Title.Should().Be("Test WMS Service");
    }

    [Fact(Skip = "Integration test - requires backend API")]
    public async Task GetServices_ReturnsServiceList()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/admin/metadata/services");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var services = await response.Content.ReadFromJsonAsync<List<ServiceListItem>>();
        services.Should().NotBeNull();
    }

    [Fact(Skip = "Integration test - requires backend API")]
    public async Task UpdateService_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var serviceId = "test-service";

        var updateRequest = new UpdateServiceRequest
        {
            Title = "Updated Service Name",
            ServiceType = "WMS",
            Description = "Updated description"
        };

        // Act
        var response = await client.PutAsJsonAsync($"/admin/metadata/services/{serviceId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(Skip = "Integration test - requires backend API")]
    public async Task DeleteService_ExistingService_ReturnsNoContent()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var serviceId = "test-service";

        // Act
        var response = await client.DeleteAsync($"/admin/metadata/services/{serviceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(Skip = "Integration test - requires backend API")]
    public async Task DeleteService_NonExistentService_ReturnsNotFound()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var serviceId = "nonexistent-service";

        // Act
        var response = await client.DeleteAsync($"/admin/metadata/services/{serviceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(Skip = "Integration test - requires backend API")]
    public async Task CreateService_DuplicateId_ReturnsConflict()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();

        var createRequest = new CreateServiceRequest
        {
            Id = "existing-service",
            Title = "Duplicate Service",
            ServiceType = "WMS"
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/services", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(Skip = "Integration test - requires backend API")]
    public async Task GetServiceById_ExistingService_ReturnsService()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var serviceId = "test-service";

        // Act
        var response = await client.GetAsync($"/admin/metadata/services/{serviceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var service = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        service.Should().NotBeNull();
        service!.Id.Should().Be(serviceId);
    }

    [Fact(Skip = "Integration test - requires backend API")]
    public async Task GetServiceById_NonExistentService_ReturnsNotFound()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var serviceId = "nonexistent-service";

        // Act
        var response = await client.GetAsync($"/admin/metadata/services/{serviceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<HttpClient> CreateAuthenticatedClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri("https://localhost:5001")
        };

        // Add authentication token
        // This would typically come from a test configuration or environment variable
        var token = Environment.GetEnvironmentVariable("TEST_AUTH_TOKEN");
        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return await Task.FromResult(client);
    }
}

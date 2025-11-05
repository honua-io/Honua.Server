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
            Name = "Test WMS Service",
            ServiceType = "WMS",
            FolderId = null,
            Abstract = "Test service for integration testing",
            Keywords = new List<string> { "test", "integration" },
            Enabled = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/services", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var service = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        service.Should().NotBeNull();
        service!.Id.Should().Be("test-service");
        service.Name.Should().Be("Test WMS Service");
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
    public async Task UpdateService_ValidRequest_ReturnsUpdatedService()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var serviceId = "test-service";

        var updateRequest = new UpdateServiceRequest
        {
            Name = "Updated Service Name",
            Abstract = "Updated description",
            Enabled = false
        };

        // Act
        var response = await client.PutAsJsonAsync($"/admin/metadata/services/{serviceId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var service = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        service.Should().NotBeNull();
        service!.Name.Should().Be("Updated Service Name");
        service.Enabled.Should().BeFalse();
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
        var serviceId = "non-existent-service";

        // Act
        var response = await client.DeleteAsync($"/admin/metadata/services/{serviceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Helper method to create an authenticated HttpClient.
    /// In real integration tests, this would authenticate and get a token.
    /// </summary>
    private async Task<HttpClient> CreateAuthenticatedClient()
    {
        var client = new HttpClient { BaseAddress = new Uri("https://localhost:5001") };

        // Login to get token
        var loginRequest = new { Username = "admin", Password = "password" };
        var loginResponse = await client.PostAsJsonAsync("/api/tokens/generate", loginRequest);
        var tokenResponse = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        // Add bearer token
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenResponse!.Token}");

        return client;
    }
}

// DTOs for integration tests
public class CreateServiceRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string? FolderId { get; set; }
    public string? Abstract { get; set; }
    public List<string>? Keywords { get; set; }
    public bool Enabled { get; set; }
}

public class UpdateServiceRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Abstract { get; set; }
    public bool Enabled { get; set; }
}

public class ServiceResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public class ServiceListItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
}

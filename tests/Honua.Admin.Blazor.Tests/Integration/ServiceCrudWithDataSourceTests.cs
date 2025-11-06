// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using System.Net;
using System.Net.Http.Json;

namespace Honua.Admin.Blazor.Tests.Integration;

/// <summary>
/// Enhanced integration tests for Service CRUD operations with data source scenarios.
/// These tests extend the existing ServiceCrudIntegrationTests with data source-specific scenarios.
/// Note: These require the backend API to be running.
/// </summary>
[Trait("Category", "Integration")]
public class ServiceCrudWithDataSourceTests
{
    /// <summary>
    /// Tests creating a service with a valid data source.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task CreateService_WithDataSource_Success()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var dataSourceId = $"test-ds-{Guid.NewGuid():N}";
        var serviceId = $"test-service-{Guid.NewGuid():N}";

        // Step 1: Create data source
        var dataSourceRequest = new CreateDataSourceRequest
        {
            Id = dataSourceId,
            Provider = "PostGIS",
            ConnectionString = "Host=localhost;Port=5432;Database=testdb;Username=test;Password=test"
        };
        var dsResponse = await client.PostAsJsonAsync("/admin/metadata/datasources", dataSourceRequest);
        dsResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 2: Create service with data source
        var createRequest = new CreateServiceRequest
        {
            Id = serviceId,
            Title = "Test Service with Data Source",
            ServiceType = "WMS",
            DataSourceId = dataSourceId,
            FolderId = null,
            Description = "Test service for data source integration",
            Keywords = new List<string> { "test", "integration" },
            Enabled = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/services", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var service = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        service.Should().NotBeNull();
        service!.Id.Should().Be(serviceId);
        service.DataSourceId.Should().Be(dataSourceId);
    }

    /// <summary>
    /// Tests that creating a service without a data source returns a validation error.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task CreateService_WithoutDataSource_ValidationError()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var serviceId = $"test-service-{Guid.NewGuid():N}";

        var createRequest = new CreateServiceRequest
        {
            Id = serviceId,
            Title = "Test Service Without Data Source",
            ServiceType = "WMS",
            DataSourceId = "", // Empty data source
            FolderId = null,
            Enabled = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/services", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests that creating a service with an invalid data source ID returns an error.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task CreateService_InvalidDataSourceId_ApiReturnsError()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var serviceId = $"test-service-{Guid.NewGuid():N}";

        var createRequest = new CreateServiceRequest
        {
            Id = serviceId,
            Title = "Test Service",
            ServiceType = "WMS",
            DataSourceId = "non-existent-data-source",
            FolderId = null,
            Enabled = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/services", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("Data source");
        error.Should().Contain("does not exist");
    }

    /// <summary>
    /// Tests that updating a service cannot change the data source.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task UpdateService_CannotChangeDataSource()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var dataSourceId1 = $"test-ds1-{Guid.NewGuid():N}";
        var dataSourceId2 = $"test-ds2-{Guid.NewGuid():N}";
        var serviceId = $"test-service-{Guid.NewGuid():N}";

        // Create two data sources
        await client.PostAsJsonAsync("/admin/metadata/datasources", new CreateDataSourceRequest
        {
            Id = dataSourceId1,
            Provider = "PostGIS",
            ConnectionString = "Host=localhost"
        });
        await client.PostAsJsonAsync("/admin/metadata/datasources", new CreateDataSourceRequest
        {
            Id = dataSourceId2,
            Provider = "SqlServer",
            ConnectionString = "Server=localhost"
        });

        // Create service with first data source
        await client.PostAsJsonAsync("/admin/metadata/services", new CreateServiceRequest
        {
            Id = serviceId,
            Title = "Test Service",
            ServiceType = "WMS",
            DataSourceId = dataSourceId1,
            FolderId = null
        });

        // Act - Attempt to update with different data source
        // Note: The UpdateServiceRequest doesn't include DataSourceId, so this validates
        // that the backend properly maintains the original data source
        var updateRequest = new UpdateServiceRequest
        {
            Title = "Updated Service Title",
            Description = "Updated description",
            FolderId = ""
        };

        var response = await client.PutAsJsonAsync($"/admin/metadata/services/{serviceId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify data source didn't change
        var getResponse = await client.GetAsync($"/admin/metadata/services/{serviceId}");
        var service = await getResponse.Content.ReadFromJsonAsync<ServiceResponse>();
        service!.DataSourceId.Should().Be(dataSourceId1);
    }

    /// <summary>
    /// Tests updating a service's folder and title while preserving data source.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task UpdateService_ChangeFolderAndTitle_Success()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var dataSourceId = "existing-ds";
        var serviceId = "existing-service";

        var updateRequest = new UpdateServiceRequest
        {
            Title = "Updated Service Title",
            Description = "Updated description",
            FolderId = "new-folder"
        };

        // Act
        var response = await client.PutAsJsonAsync($"/admin/metadata/services/{serviceId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify updates
        var getResponse = await client.GetAsync($"/admin/metadata/services/{serviceId}");
        var service = await getResponse.Content.ReadFromJsonAsync<ServiceResponse>();
        service!.Title.Should().Be("Updated Service Title");
        service.FolderId.Should().Be("new-folder");
        service.DataSourceId.Should().NotBeNullOrEmpty(); // Data source preserved
    }

    /// <summary>
    /// Tests that deleting a service removes it from data source usage count.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task DeleteService_RemovesFromDataSourceUsageCount()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var dataSourceId = $"test-ds-{Guid.NewGuid():N}";
        var serviceId = $"test-service-{Guid.NewGuid():N}";

        // Create data source and service
        await client.PostAsJsonAsync("/admin/metadata/datasources", new CreateDataSourceRequest
        {
            Id = dataSourceId,
            Provider = "PostGIS",
            ConnectionString = "Host=localhost"
        });
        await client.PostAsJsonAsync("/admin/metadata/services", new CreateServiceRequest
        {
            Id = serviceId,
            Title = "Test Service",
            ServiceType = "WMS",
            DataSourceId = dataSourceId,
            FolderId = null
        });

        // Act - Delete service
        var deleteResponse = await client.DeleteAsync($"/admin/metadata/services/{serviceId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - Data source should now be deletable (no services using it)
        var deleteDataSourceResponse = await client.DeleteAsync($"/admin/metadata/datasources/{dataSourceId}");
        deleteDataSourceResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Tests that getting services includes data source ID in the response.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task GetServices_IncludesDataSourceId()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/admin/metadata/services");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var services = await response.Content.ReadFromJsonAsync<List<ServiceListItem>>();
        services.Should().NotBeNull();

        // Verify services with data sources include the data source ID
        var servicesWithDs = services!.Where(s => !string.IsNullOrEmpty(s.DataSourceId)).ToList();
        servicesWithDs.Should().NotBeEmpty();
        servicesWithDs.All(s => !string.IsNullOrEmpty(s.DataSourceId)).Should().BeTrue();
    }

    /// <summary>
    /// Tests that getting a service by ID includes data source information.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task GetServiceById_IncludesDataSourceInfo()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var serviceId = "existing-service";

        // Act
        var response = await client.GetAsync($"/admin/metadata/services/{serviceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var service = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        service.Should().NotBeNull();
        service!.DataSourceId.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Helper method to create an authenticated HttpClient.
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
file record TokenResponse
{
    public required string Token { get; init; }
}

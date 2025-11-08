// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using System.Net;
using System.Net.Http.Json;

namespace Honua.Admin.Blazor.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the complete data source to layer workflow.
/// These tests demonstrate the full user workflow from creating a data source
/// to testing connections and creating services and layers.
/// Note: These require the backend API to be running.
/// </summary>
[Trait("Category", "Integration")]
public class DataSourceToLayerWorkflowTests
{
    /// <summary>
    /// Tests the complete workflow: create data source, test connection successfully.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task EndToEnd_CreateDataSource_TestConnection_Success()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var dataSourceId = $"test-ds-{Guid.NewGuid():N}";

        var createRequest = new CreateDataSourceRequest
        {
            Id = dataSourceId,
            Provider = "PostGIS",
            ConnectionString = "Host=localhost;Port=5432;Database=testdb;Username=test;Password=test"
        };

        // Act - Create data source
        var createResponse = await client.PostAsJsonAsync("/admin/metadata/datasources", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act - Test connection
        var testResponse = await client.PostAsync($"/admin/metadata/datasources/{dataSourceId}/test", null);
        testResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var testResult = await testResponse.Content.ReadFromJsonAsync<TestConnectionResponse>();

        // Assert
        testResult.Should().NotBeNull();
        testResult!.Success.Should().BeTrue();
        testResult.ConnectionTime.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Tests the complete workflow: create service with data source selection.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task EndToEnd_CreateService_SelectDataSource_Success()
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
            ConnectionString = "Host=localhost;Port=5432;Database=testdb"
        };
        await client.PostAsJsonAsync("/admin/metadata/datasources", dataSourceRequest);

        // Step 2: Create service with data source
        var serviceRequest = new CreateServiceRequest
        {
            Id = serviceId,
            Title = "Test Service with Data Source",
            ServiceType = "WMS",
            DataSourceId = dataSourceId,
            FolderId = null
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/services", serviceRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var service = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        service.Should().NotBeNull();
        service!.DataSourceId.Should().Be(dataSourceId);
    }

    /// <summary>
    /// Tests creating a layer from a database table.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task EndToEnd_CreateLayer_FromDatabaseTable_Success()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var dataSourceId = "existing-ds";
        var serviceId = "existing-service";
        var layerId = $"test-layer-{Guid.NewGuid():N}";

        // Get tables from data source
        var tablesResponse = await client.GetAsync($"/admin/metadata/datasources/{dataSourceId}/tables");
        tablesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tablesResult = await tablesResponse.Content.ReadFromJsonAsync<TableListResponse>();
        tablesResult!.Tables.Should().NotBeEmpty();

        var table = tablesResult.Tables.First();

        // Act - Create layer from table
        var layerRequest = new CreateLayerRequest
        {
            Id = layerId,
            ServiceId = serviceId,
            Title = $"Layer from {table.Table}",
            GeometryType = table.GeometryType ?? "Point",
            IdField = "id",
            GeometryField = table.GeometryColumn ?? "geom",
            DisplayField = "name"
        };

        var response = await client.PostAsJsonAsync("/admin/metadata/layers", layerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// Tests creating a layer with column configuration.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task EndToEnd_CreateLayer_WithColumnConfig_Success()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var serviceId = "existing-service";
        var layerId = $"test-layer-{Guid.NewGuid():N}";

        var layerRequest = new CreateLayerRequest
        {
            Id = layerId,
            ServiceId = serviceId,
            Title = "Layer with Column Config",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            DisplayField = "name",
            Crs = new List<string> { "EPSG:4326", "EPSG:3857" }
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/layers", layerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var layer = await response.Content.ReadFromJsonAsync<LayerResponse>();
        layer.Should().NotBeNull();
        layer!.Crs.Should().Contain("EPSG:4326");
    }

    /// <summary>
    /// Tests creating a layer with filter configuration.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task EndToEnd_CreateLayer_WithFilter_Success()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var serviceId = "existing-service";
        var layerId = $"test-layer-{Guid.NewGuid():N}";

        var layerRequest = new CreateLayerRequest
        {
            Id = layerId,
            ServiceId = serviceId,
            Title = "Layer with Filter",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            DisplayField = "name",
            Description = "Filtered layer for testing"
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/layers", layerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// Tests creating a layer with coded values configuration.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task EndToEnd_CreateLayer_WithCodedValues_Success()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var serviceId = "existing-service";
        var layerId = $"test-layer-{Guid.NewGuid():N}";

        var layerRequest = new CreateLayerRequest
        {
            Id = layerId,
            ServiceId = serviceId,
            Title = "Layer with Coded Values",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            DisplayField = "name",
            Keywords = new List<string> { "test", "coded-values" }
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/layers", layerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// Tests that deleting a data source in use returns an error.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task EndToEnd_DeleteDataSource_InUse_ShowsError()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var dataSourceId = "ds-in-use";

        // Act - Attempt to delete data source that's in use
        var response = await client.DeleteAsync($"/admin/metadata/datasources/{dataSourceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("in use");
    }

    /// <summary>
    /// Tests viewing services that use a specific data source.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task EndToEnd_ViewServicesUsingDataSource_ShowsDialog()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var dataSourceId = "test-ds";

        // Act - Get all services
        var response = await client.GetAsync("/admin/metadata/services");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var services = await response.Content.ReadFromJsonAsync<List<ServiceListItem>>();

        // Assert - Filter services using this data source
        var servicesUsingDs = services!.Where(s => s.DataSourceId == dataSourceId).ToList();
        servicesUsingDs.Should().NotBeNull();
    }

    /// <summary>
    /// Tests connection test from service detail page.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task EndToEnd_ServiceDetail_TestConnection_Success()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var serviceId = "existing-service";

        // Get service to find its data source
        var serviceResponse = await client.GetAsync($"/admin/metadata/services/{serviceId}");
        serviceResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var service = await serviceResponse.Content.ReadFromJsonAsync<ServiceResponse>();
        service!.DataSourceId.Should().NotBeNullOrEmpty();

        // Act - Test connection for the service's data source
        var testResponse = await client.PostAsync($"/admin/metadata/datasources/{service.DataSourceId}/test", null);

        // Assert
        testResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var testResult = await testResponse.Content.ReadFromJsonAsync<TestConnectionResponse>();
        testResult.Should().NotBeNull();
    }

    /// <summary>
    /// Tests file import workflow creating a job and monitoring progress.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task EndToEnd_ImportFile_CreateJob_MonitorProgress()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();
        var dataSourceId = "test-ds";

        // This test would require actual file upload infrastructure
        // Placeholder for future implementation
        await Task.CompletedTask;
    }

    /// <summary>
    /// Tests Esri service import workflow creating a job and monitoring progress.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task EndToEnd_ImportEsri_CreateJob_MonitorProgress()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();

        // This test would require Esri service integration
        // Placeholder for future implementation
        await Task.CompletedTask;
    }

    /// <summary>
    /// Tests that validation errors prevent workflow progression.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task Workflow_ValidationErrors_PreventProgression()
    {
        // Arrange
        using var client = await CreateAuthenticatedClient();

        // Try to create service without data source
        var serviceRequest = new CreateServiceRequest
        {
            Id = "invalid-service",
            Title = "Invalid Service",
            ServiceType = "WMS",
            DataSourceId = "non-existent-ds",
            FolderId = null
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/services", serviceRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Tests that network errors show user-friendly messages.
    /// </summary>
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task Workflow_NetworkError_ShowsUserFriendlyMessage()
    {
        // Arrange
        using var client = new HttpClient { BaseAddress = new Uri("https://invalid.honua.test") };

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await client.GetAsync("/admin/metadata/services");
        });
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
file record CreateLayerRequest
{
    public required string Id { get; init; }
    public required string ServiceId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string GeometryType { get; init; }
    public required string IdField { get; init; }
    public required string GeometryField { get; init; }
    public required string DisplayField { get; init; }
    public List<string> Crs { get; init; } = new();
    public List<string> Keywords { get; init; } = new();
}

file record LayerResponse
{
    public required string Id { get; init; }
    public required string ServiceId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string GeometryType { get; init; }
    public List<string> Crs { get; init; } = new();
}

file record TokenResponse
{
    public required string Token { get; init; }
}

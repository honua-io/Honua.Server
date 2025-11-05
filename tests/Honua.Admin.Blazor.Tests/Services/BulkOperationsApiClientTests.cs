// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using RichardSzalay.MockHttp;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Honua.Admin.Blazor.Tests.Services;

public class BulkOperationsApiClientTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly BulkOperationsApiClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public BulkOperationsApiClientTests()
    {
        _mockHandler = new MockHttpMessageHandler();
        _httpClient = _mockHandler.ToHttpClient();
        _httpClient.BaseAddress = new Uri("https://api.test/");
        _client = new BulkOperationsApiClient(_httpClient);
        _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    [Fact]
    public async Task BulkDeleteServicesAsync_ShouldReturnSuccessResponse()
    {
        // Arrange
        var serviceIds = new List<string> { "service1", "service2", "service3" };
        var expectedResponse = new BulkOperationResponse
        {
            OperationId = "op-123",
            TotalItems = 3,
            SuccessCount = 3,
            FailureCount = 0,
            Status = "completed",
            Results = new List<BulkOperationItemResult>
            {
                new BulkOperationItemResult { ItemId = "service1", ItemName = "Service 1", Success = true },
                new BulkOperationItemResult { ItemId = "service2", ItemName = "Service 2", Success = true },
                new BulkOperationItemResult { ItemId = "service3", ItemName = "Service 3", Success = true }
            }
        };

        _mockHandler.When(HttpMethod.Post, "https://api.test/admin/services/bulk-delete")
            .Respond("application/json", JsonSerializer.Serialize(expectedResponse, _jsonOptions));

        // Act
        var result = await _client.BulkDeleteServicesAsync(serviceIds, force: false);

        // Assert
        result.Should().NotBeNull();
        result.OperationId.Should().Be("op-123");
        result.TotalItems.Should().Be(3);
        result.SuccessCount.Should().Be(3);
        result.FailureCount.Should().Be(0);
        result.Results.Should().HaveCount(3);
        result.Results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
    }

    [Fact]
    public async Task BulkDeleteServicesAsync_WithForce_ShouldIncludeForceFlag()
    {
        // Arrange
        var serviceIds = new List<string> { "service1" };
        var expectedResponse = new BulkOperationResponse
        {
            OperationId = "op-124",
            TotalItems = 1,
            SuccessCount = 1,
            FailureCount = 0,
            Status = "completed"
        };

        var request = _mockHandler.When(HttpMethod.Post, "https://api.test/admin/services/bulk-delete")
            .WithContent("{\"ids\":[\"service1\"],\"force\":true}")
            .Respond("application/json", JsonSerializer.Serialize(expectedResponse, _jsonOptions));

        // Act
        var result = await _client.BulkDeleteServicesAsync(serviceIds, force: true);

        // Assert
        result.Should().NotBeNull();
        _mockHandler.GetMatchCount(request).Should().Be(1);
    }

    [Fact]
    public async Task BulkDeleteLayersAsync_ShouldReturnPartialSuccessResponse()
    {
        // Arrange
        var layerIds = new List<string> { "layer1", "layer2", "layer3" };
        var expectedResponse = new BulkOperationResponse
        {
            OperationId = "op-125",
            TotalItems = 3,
            SuccessCount = 2,
            FailureCount = 1,
            Status = "completed",
            Results = new List<BulkOperationItemResult>
            {
                new BulkOperationItemResult { ItemId = "layer1", ItemName = "Layer 1", Success = true },
                new BulkOperationItemResult { ItemId = "layer2", ItemName = "Layer 2", Success = false, ErrorMessage = "Layer is referenced by service" },
                new BulkOperationItemResult { ItemId = "layer3", ItemName = "Layer 3", Success = true }
            }
        };

        _mockHandler.When(HttpMethod.Post, "https://api.test/admin/layers/bulk-delete")
            .Respond("application/json", JsonSerializer.Serialize(expectedResponse, _jsonOptions));

        // Act
        var result = await _client.BulkDeleteLayersAsync(layerIds);

        // Assert
        result.SuccessCount.Should().Be(2);
        result.FailureCount.Should().Be(1);
        result.Results.Should().ContainSingle(r => !r.Success);
        result.Results.First(r => !r.Success).ErrorMessage.Should().Be("Layer is referenced by service");
    }

    [Fact]
    public async Task BulkMoveServicesToFolderAsync_ShouldMoveToTargetFolder()
    {
        // Arrange
        var serviceIds = new List<string> { "service1", "service2" };
        var targetFolderId = "folder-123";
        var expectedResponse = new BulkOperationResponse
        {
            OperationId = "op-126",
            TotalItems = 2,
            SuccessCount = 2,
            FailureCount = 0,
            Status = "completed"
        };

        _mockHandler.When(HttpMethod.Post, "https://api.test/admin/services/bulk-move")
            .Respond("application/json", JsonSerializer.Serialize(expectedResponse, _jsonOptions));

        // Act
        var result = await _client.BulkMoveServicesToFolderAsync(serviceIds, targetFolderId);

        // Assert
        result.Should().NotBeNull();
        result.SuccessCount.Should().Be(2);
    }

    [Fact]
    public async Task BulkMoveToFolderAsync_WithNullFolder_ShouldMoveToRoot()
    {
        // Arrange
        var serviceIds = new List<string> { "service1" };
        var expectedResponse = new BulkOperationResponse
        {
            OperationId = "op-127",
            TotalItems = 1,
            SuccessCount = 1,
            FailureCount = 0
        };

        var request = _mockHandler.When(HttpMethod.Post, "https://api.test/admin/services/bulk-move")
            .WithContent("{\"ids\":[\"service1\"],\"targetFolderId\":null}")
            .Respond("application/json", JsonSerializer.Serialize(expectedResponse, _jsonOptions));

        // Act
        var result = await _client.BulkMoveServicesToFolderAsync(serviceIds, null);

        // Assert
        result.Should().NotBeNull();
        _mockHandler.GetMatchCount(request).Should().Be(1);
    }

    [Fact]
    public async Task BulkUpdateServiceMetadataAsync_WithMergeMode_ShouldUpdateMetadata()
    {
        // Arrange
        var serviceIds = new List<string> { "service1", "service2" };
        var tags = new List<string> { "public", "gis" };
        var keywords = new List<string> { "map", "geodata" };
        var expectedResponse = new BulkOperationResponse
        {
            OperationId = "op-128",
            TotalItems = 2,
            SuccessCount = 2,
            FailureCount = 0
        };

        _mockHandler.When(HttpMethod.Post, "https://api.test/admin/services/bulk-update-metadata")
            .Respond("application/json", JsonSerializer.Serialize(expectedResponse, _jsonOptions));

        // Act
        var result = await _client.BulkUpdateServiceMetadataAsync(serviceIds, tags, keywords, "merge");

        // Assert
        result.Should().NotBeNull();
        result.SuccessCount.Should().Be(2);
    }

    [Fact]
    public async Task BulkApplyStyleAsync_ShouldApplyStyleToLayers()
    {
        // Arrange
        var layerIds = new List<string> { "layer1", "layer2", "layer3" };
        var styleId = "style-123";
        var expectedResponse = new BulkOperationResponse
        {
            OperationId = "op-129",
            TotalItems = 3,
            SuccessCount = 3,
            FailureCount = 0
        };

        _mockHandler.When(HttpMethod.Post, "https://api.test/admin/layers/bulk-apply-style")
            .Respond("application/json", JsonSerializer.Serialize(expectedResponse, _jsonOptions));

        // Act
        var result = await _client.BulkApplyStyleAsync(layerIds, styleId, setAsDefault: true);

        // Assert
        result.Should().NotBeNull();
        result.SuccessCount.Should().Be(3);
    }

    [Fact]
    public async Task BulkSetServiceEnabledAsync_ShouldEnableServices()
    {
        // Arrange
        var serviceIds = new List<string> { "service1", "service2" };
        var expectedResponse = new BulkOperationResponse
        {
            OperationId = "op-130",
            TotalItems = 2,
            SuccessCount = 2,
            FailureCount = 0
        };

        _mockHandler.When(HttpMethod.Post, "https://api.test/admin/services/bulk-set-enabled")
            .Respond("application/json", JsonSerializer.Serialize(expectedResponse, _jsonOptions));

        // Act
        var result = await _client.BulkSetServiceEnabledAsync(serviceIds, enabled: true);

        // Assert
        result.Should().NotBeNull();
        result.SuccessCount.Should().Be(2);
    }

    [Fact]
    public async Task GetOperationStatusAsync_ShouldReturnStatus()
    {
        // Arrange
        var operationId = "op-131";
        var expectedStatus = new BulkOperationStatus
        {
            OperationId = operationId,
            Status = "in_progress",
            Progress = 50,
            ProcessedItems = 5,
            TotalItems = 10,
            CurrentItem = "item-5",
            CanCancel = true
        };

        _mockHandler.When(HttpMethod.Get, $"https://api.test/admin/bulk-operations/{operationId}/status")
            .Respond("application/json", JsonSerializer.Serialize(expectedStatus, _jsonOptions));

        // Act
        var result = await _client.GetOperationStatusAsync(operationId);

        // Assert
        result.Should().NotBeNull();
        result.OperationId.Should().Be(operationId);
        result.Status.Should().Be("in_progress");
        result.Progress.Should().Be(50);
        result.ProcessedItems.Should().Be(5);
        result.TotalItems.Should().Be(10);
        result.CanCancel.Should().BeTrue();
    }

    [Fact]
    public async Task CancelOperationAsync_ShouldCancelOperation()
    {
        // Arrange
        var operationId = "op-132";

        _mockHandler.When(HttpMethod.Post, $"https://api.test/admin/bulk-operations/{operationId}/cancel")
            .Respond(HttpStatusCode.OK);

        // Act
        await _client.CancelOperationAsync(operationId);

        // Assert
        // Should complete without exception
    }

    [Fact]
    public async Task GetOperationResultsAsync_ShouldReturnResults()
    {
        // Arrange
        var operationId = "op-133";
        var expectedResults = new BulkOperationResponse
        {
            OperationId = operationId,
            TotalItems = 5,
            SuccessCount = 4,
            FailureCount = 1,
            Status = "completed",
            Results = new List<BulkOperationItemResult>
            {
                new BulkOperationItemResult { ItemId = "item1", Success = true },
                new BulkOperationItemResult { ItemId = "item2", Success = true },
                new BulkOperationItemResult { ItemId = "item3", Success = false, ErrorMessage = "Error" },
                new BulkOperationItemResult { ItemId = "item4", Success = true },
                new BulkOperationItemResult { ItemId = "item5", Success = true }
            }
        };

        _mockHandler.When(HttpMethod.Get, $"https://api.test/admin/bulk-operations/{operationId}/results")
            .Respond("application/json", JsonSerializer.Serialize(expectedResults, _jsonOptions));

        // Act
        var result = await _client.GetOperationResultsAsync(operationId);

        // Assert
        result.Should().NotBeNull();
        result.OperationId.Should().Be(operationId);
        result.TotalItems.Should().Be(5);
        result.SuccessCount.Should().Be(4);
        result.FailureCount.Should().Be(1);
        result.Results.Should().HaveCount(5);
    }

    [Fact]
    public async Task BulkOperationAsync_WhenServerReturnsError_ShouldThrowException()
    {
        // Arrange
        var serviceIds = new List<string> { "service1" };

        _mockHandler.When(HttpMethod.Post, "https://api.test/admin/services/bulk-delete")
            .Respond(HttpStatusCode.InternalServerError);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            _client.BulkDeleteServicesAsync(serviceIds));
    }

    public void Dispose()
    {
        _mockHandler?.Dispose();
        _httpClient?.Dispose();
    }
}

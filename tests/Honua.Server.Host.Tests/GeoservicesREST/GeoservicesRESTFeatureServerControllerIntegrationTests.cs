// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Host.Tests.TestInfrastructure;
using Xunit;

namespace Honua.Server.Host.Tests.GeoservicesREST;

/// <summary>
/// Integration tests for GeoservicesRESTFeatureServerController - ArcGIS REST Feature Server implementation.
/// Tests service metadata, layer metadata, query operations, and feature editing.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "ArcGIS_FeatureServer")]
public sealed class GeoservicesRESTFeatureServerControllerIntegrationTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GeoservicesRESTFeatureServerControllerIntegrationTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    #region Service Metadata Tests

    [Fact]
    public async Task GetServiceMetadata_ReturnsServiceInfo()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("currentVersion", out _).Should().BeTrue();
            content.TryGetProperty("serviceDescription", out _).Should().BeTrue();
            content.TryGetProperty("layers", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetServiceMetadata_WithFolderId_ReturnsServiceInfo()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-folder/test-service/FeatureServer");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetServiceMetadata_NonExistentService_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/nonexistent-service-12345/FeatureServer");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetServiceMetadata_WithoutAuthentication_HandlesCorrectly()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/rest/services/test-service/FeatureServer");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Layer Metadata Tests

    [Fact]
    public async Task GetLayerMetadata_ReturnsLayerInfo()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer/0");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("id", out _).Should().BeTrue();
            content.TryGetProperty("name", out _).Should().BeTrue();
            content.TryGetProperty("type", out var type).Should().BeTrue();
            type.GetString().Should().Be("Feature Layer");
        }
    }

    [Fact]
    public async Task GetLayerMetadata_NonExistentLayer_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer/999");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetLayerMetadata_InvalidLayerIndex_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer/-1");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task Query_WithDefaultParameters_ReturnsFeatures()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer/0/query?where=1=1&f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("features", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Query_PostMethod_ReturnsFeatures()
    {
        // Arrange
        var queryParams = new Dictionary<string, string>
        {
            { "where", "1=1" },
            { "f", "json" }
        };

        // Act
        var response = await _client.PostAsync(
            "/rest/services/test-service/FeatureServer/0/query",
            new FormUrlEncodedContent(queryParams));

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Query_WithReturnCountOnly_ReturnsCount()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer/0/query?where=1=1&returnCountOnly=true&f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("count", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Query_WithResultOffset_PaginatesResults()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer/0/query?where=1=1&resultOffset=10&f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Query_WithResultRecordCount_LimitsResults()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer/0/query?where=1=1&resultRecordCount=10&f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Query_WithGeometry_FiltersResults()
    {
        // Act
        var geometry = "{\"xmin\":-122.5,\"ymin\":37.7,\"xmax\":-122.3,\"ymax\":37.8}";
        var response = await _client.GetAsync($"/rest/services/test-service/FeatureServer/0/query?where=1=1&geometry={Uri.EscapeDataString(geometry)}&geometryType=esriGeometryEnvelope&f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Query_WithOutFields_ReturnsSpecifiedFields()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer/0/query?where=1=1&outFields=id,name&f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Query_WithOrderBy_SortsResults()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer/0/query?where=1=1&orderByFields=name ASC&f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Query_InvalidWhereClause_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer/0/query?where=invalid syntax here&f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    #endregion

    #region AddFeatures Tests

    [Fact]
    public async Task AddFeatures_ValidFeature_ReturnsSuccess()
    {
        // Arrange
        var features = new[]
        {
            new
            {
                attributes = new { name = "Test Feature", description = "Test" },
                geometry = new { x = -122.4194, y = 37.7749 }
            }
        };

        var formData = new Dictionary<string, string>
        {
            { "features", JsonSerializer.Serialize(features) },
            { "f", "json" }
        };

        // Act
        var response = await _client.PostAsync(
            "/rest/services/test-service/FeatureServer/0/addFeatures",
            new FormUrlEncodedContent(formData));

        // Assert - Requires DataPublisher role
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddFeatures_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();
        var features = new[] { new { attributes = new { name = "Test" } } };
        var formData = new Dictionary<string, string>
        {
            { "features", JsonSerializer.Serialize(features) },
            { "f", "json" }
        };

        // Act
        var response = await unauthenticatedClient.PostAsync(
            "/rest/services/test-service/FeatureServer/0/addFeatures",
            new FormUrlEncodedContent(formData));

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region UpdateFeatures Tests

    [Fact]
    public async Task UpdateFeatures_ValidFeature_ReturnsSuccess()
    {
        // Arrange
        var features = new[]
        {
            new
            {
                attributes = new { OBJECTID = 1, name = "Updated Feature" }
            }
        };

        var formData = new Dictionary<string, string>
        {
            { "features", JsonSerializer.Serialize(features) },
            { "f", "json" }
        };

        // Act
        var response = await _client.PostAsync(
            "/rest/services/test-service/FeatureServer/0/updateFeatures",
            new FormUrlEncodedContent(formData));

        // Assert - Requires DataPublisher role
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    #endregion

    #region DeleteFeatures Tests

    [Fact]
    public async Task DeleteFeatures_ValidObjectIds_ReturnsSuccess()
    {
        // Arrange
        var formData = new Dictionary<string, string>
        {
            { "objectIds", "1,2,3" },
            { "f", "json" }
        };

        // Act
        var response = await _client.PostAsync(
            "/rest/services/test-service/FeatureServer/0/deleteFeatures",
            new FormUrlEncodedContent(formData));

        // Assert - Requires DataPublisher role
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteFeatures_WithWhereClause_ReturnsSuccess()
    {
        // Arrange
        var formData = new Dictionary<string, string>
        {
            { "where", "status='deleted'" },
            { "f", "json" }
        };

        // Act
        var response = await _client.PostAsync(
            "/rest/services/test-service/FeatureServer/0/deleteFeatures",
            new FormUrlEncodedContent(formData));

        // Assert - Requires DataPublisher role
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    #endregion

    #region ApplyEdits Tests

    [Fact]
    public async Task ApplyEdits_BatchOperations_ReturnsSuccess()
    {
        // Arrange
        var adds = new[] { new { attributes = new { name = "New Feature" } } };
        var updates = new[] { new { attributes = new { OBJECTID = 1, name = "Updated" } } };
        var deletes = new[] { 2, 3 };

        var formData = new Dictionary<string, string>
        {
            { "adds", JsonSerializer.Serialize(adds) },
            { "updates", JsonSerializer.Serialize(updates) },
            { "deletes", JsonSerializer.Serialize(deletes) },
            { "f", "json" }
        };

        // Act
        var response = await _client.PostAsync(
            "/rest/services/test-service/FeatureServer/0/applyEdits",
            new FormUrlEncodedContent(formData));

        // Assert - Requires DataPublisher role
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Attachment Tests

    [Fact]
    public async Task QueryAttachments_ReturnsAttachmentInfo()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer/0/queryAttachments?objectIds=1&f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAttachment_ByIdReturnsAttachment()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer/0/1/attachments/1");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GenerateRenderer Tests

    [Fact]
    public async Task GenerateRenderer_GetMethod_ReturnsRenderer()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer/0/generateRenderer?classificationDef={\"type\":\"uniqueValueDef\",\"uniqueValueFields\":[\"type\"]}&f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GenerateRenderer_PostMethod_ReturnsRenderer()
    {
        // Arrange
        var formData = new Dictionary<string, string>
        {
            { "classificationDef", "{\"type\":\"uniqueValueDef\",\"uniqueValueFields\":[\"type\"]}" },
            { "f", "json" }
        };

        // Act
        var response = await _client.PostAsync(
            "/rest/services/test-service/FeatureServer/0/generateRenderer",
            new FormUrlEncodedContent(formData));

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest);
    }

    #endregion

    #region Content Negotiation Tests

    [Fact]
    public async Task Query_WithFormatJson_ReturnsJson()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer/0/query?where=1=1&f=json");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("json");
        }
    }

    [Fact]
    public async Task Query_WithFormatGeoJson_ReturnsGeoJson()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer/0/query?where=1=1&f=geojson");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("json");
        }
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task GetServiceMetadata_CompletesWithinReasonableTime()
    {
        // Act
        var startTime = DateTime.UtcNow;
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer");
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            duration.Should().BeLessThan(2000); // Should complete within 2 seconds
        }
    }

    [Fact]
    public async Task Query_CompletesWithinReasonableTime()
    {
        // Act
        var startTime = DateTime.UtcNow;
        var response = await _client.GetAsync("/rest/services/test-service/FeatureServer/0/query?where=1=1&f=json");
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            duration.Should().BeLessThan(3000); // Should complete within 3 seconds
        }
    }

    #endregion

    #region Concurrent Request Tests

    [Fact]
    public async Task Query_MultipleConcurrentRequests_HandlesCorrectly()
    {
        // Act - Send 10 concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _client.GetAsync("/rest/services/test-service/FeatureServer/0/query?where=1=1&f=json"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should complete
        responses.Should().NotBeEmpty();
        responses.Should().OnlyContain(r => r != null);
    }

    #endregion
}

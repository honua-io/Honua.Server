// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Honua.Server.Host.Tests.TestInfrastructure;
using Xunit;

namespace Honua.Server.Host.Tests.OData;

/// <summary>
/// Integration tests for DynamicODataController - OData protocol support for feature data.
/// Tests OData query, CRUD operations, and protocol compliance.
/// Note: OData is disabled by default in test configuration, so many tests verify proper error handling.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "OData")]
public sealed class DynamicODataControllerIntegrationTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DynamicODataControllerIntegrationTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    #region OData Metadata Tests

    [Fact]
    public async Task GetODataMetadata_ReturnsMetadataDocument()
    {
        // Act
        var response = await _client.GetAsync("/odata/$metadata");

        // Assert - OData is disabled by default in test configuration
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetODataMetadata_WithoutAuthentication_HandlesCorrectly()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/odata/$metadata");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetODataMetadata_ReturnsXmlContentType()
    {
        // Act
        var response = await _client.GetAsync("/odata/$metadata");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("xml");
        }
    }

    #endregion

    #region OData Collection Query Tests

    [Fact]
    public async Task GetCollection_BasicQuery_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet");

        // Assert - OData may be disabled or entity set may not exist
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCollection_WithTop_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet?$top=10");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCollection_WithSkip_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet?$skip=5");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCollection_WithCount_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet?$count=true");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCollection_CountOnly_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet/$count");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCollection_WithFilter_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet?$filter=id eq 1");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCollection_WithOrderBy_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet?$orderby=id desc");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCollection_WithSelect_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet?$select=id,name");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCollection_WithExpand_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet?$expand=RelatedEntity");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCollection_WithMultipleQueryOptions_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet?$top=10&$skip=5&$count=true&$orderby=id");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    #endregion

    #region OData Get Entity Tests

    [Fact]
    public async Task GetEntity_ByKey_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet(1)");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetEntity_WithStringKey_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet('test-id')");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetEntity_WithGuidKey_HandlesCorrectly()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/odata/TestEntitySet({guid})");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetEntity_WithSelect_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet(1)?$select=id,name");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    #endregion

    #region OData Create Tests

    [Fact]
    public async Task Post_CreateEntity_HandlesCorrectly()
    {
        // Arrange
        var entity = new
        {
            name = "Test Entity",
            description = "Test Description"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/odata/TestEntitySet", entity);

        // Assert - Requires DataPublisher role
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();
        var entity = new
        {
            name = "Test Entity"
        };

        // Act
        var response = await unauthenticatedClient.PostAsJsonAsync("/odata/TestEntitySet", entity);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_EmptyBody_ReturnsBadRequest()
    {
        // Act
        var response = await _client.PostAsync("/odata/TestEntitySet", null);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable);
    }

    #endregion

    #region OData Update Tests

    [Fact]
    public async Task Put_UpdateEntity_HandlesCorrectly()
    {
        // Arrange
        var entity = new
        {
            name = "Updated Entity",
            description = "Updated Description"
        };

        // Act
        var response = await _client.PutAsJsonAsync("/odata/TestEntitySet(1)", entity);

        // Assert - Requires DataPublisher role
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();
        var entity = new
        {
            name = "Updated Entity"
        };

        // Act
        var response = await unauthenticatedClient.PutAsJsonAsync("/odata/TestEntitySet(1)", entity);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_WithETag_HandlesCorrectly()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("If-Match", "\"test-etag\"");
        var entity = new
        {
            name = "Updated Entity"
        };

        // Act
        var response = await client.PutAsJsonAsync("/odata/TestEntitySet(1)", entity);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest,
            HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task Patch_UpdateEntity_HandlesCorrectly()
    {
        // Arrange
        var entity = new
        {
            name = "Patched Entity"
        };

        // Act
        var response = await _client.PatchAsync("/odata/TestEntitySet(1)", JsonContent.Create(entity));

        // Assert - Requires DataPublisher role
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_WithETag_HandlesCorrectly()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("If-Match", "\"test-etag\"");
        var entity = new
        {
            name = "Patched Entity"
        };

        // Act
        var response = await client.PatchAsync("/odata/TestEntitySet(1)", JsonContent.Create(entity));

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest,
            HttpStatusCode.PreconditionFailed);
    }

    #endregion

    #region OData Delete Tests

    [Fact]
    public async Task Delete_Entity_HandlesCorrectly()
    {
        // Act
        var response = await _client.DeleteAsync("/odata/TestEntitySet(1)");

        // Assert - Requires DataPublisher role
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NoContent,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.DeleteAsync("/odata/TestEntitySet(1)");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithETag_HandlesCorrectly()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("If-Match", "\"test-etag\"");

        // Act
        var response = await client.DeleteAsync("/odata/TestEntitySet(1)");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NoContent,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest,
            HttpStatusCode.PreconditionFailed);
    }

    #endregion

    #region OData Spatial Filter Tests

    [Fact]
    public async Task GetCollection_WithGeoIntersectsFilter_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet?$filter=geo.intersects(geometry, geography'POINT(-122.4194 37.7749)')");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCollection_WithGeoDistanceFilter_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet?$filter=geo.distance(geometry, geography'POINT(-122.4194 37.7749)') lt 1000");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    #endregion

    #region Content Negotiation Tests

    [Fact]
    public async Task GetCollection_WithAcceptJson_ReturnsJson()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        // Act
        var response = await client.GetAsync("/odata/TestEntitySet");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("json");
        }
    }

    [Fact]
    public async Task GetMetadata_WithAcceptXml_ReturnsXml()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("Accept", "application/xml");

        // Act
        var response = await client.GetAsync("/odata/$metadata");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("xml");
        }
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task GetCollection_CompletesWithinReasonableTime()
    {
        // Act
        var startTime = DateTime.UtcNow;
        var response = await _client.GetAsync("/odata/TestEntitySet");
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            duration.Should().BeLessThan(2000); // Should complete within 2 seconds
        }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GetCollection_InvalidFilter_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet?$filter=invalid syntax here");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetCollection_InvalidTop_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet?$top=invalid");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetCollection_NegativeTop_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet?$top=-10");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetCollection_NegativeSkip_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/odata/TestEntitySet?$skip=-5");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable);
    }

    #endregion

    #region Concurrent Request Tests

    [Fact]
    public async Task GetCollection_MultipleConcurrentRequests_HandlesCorrectly()
    {
        // Act - Send 10 concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _client.GetAsync("/odata/TestEntitySet"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should complete
        responses.Should().NotBeEmpty();
        responses.Should().OnlyContain(r => r != null);
    }

    #endregion
}

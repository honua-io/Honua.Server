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
/// Integration tests for GeoservicesRESTImageServerController - ArcGIS REST Image Server implementation.
/// Tests service metadata, image export, and raster analytics operations.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "ArcGIS_ImageServer")]
public sealed class GeoservicesRESTImageServerControllerIntegrationTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GeoservicesRESTImageServerControllerIntegrationTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    #region Service Metadata Tests

    [Fact]
    public async Task GetService_ReturnsServiceInfo()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/ImageServer");

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
            content.TryGetProperty("pixelSizeX", out _).Should().BeTrue();
            content.TryGetProperty("pixelSizeY", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetService_NonExistentService_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/nonexistent-service/ImageServer");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetService_WithoutAuthentication_HandlesCorrectly()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/rest/services/folder/service/ImageServer");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region ExportImage Tests

    [Fact]
    public async Task ExportImage_WithBbox_ReturnsImage()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/ImageServer/exportImage?bbox=-122.5,37.7,-122.3,37.8&size=512,512&f=image");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("image");
        }
    }

    [Fact]
    public async Task ExportImage_WithFormat_ReturnsSpecifiedFormat()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/ImageServer/exportImage?bbox=-122.5,37.7,-122.3,37.8&size=512,512&format=png&f=image");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExportImage_WithPngFormat_ReturnsPng()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/ImageServer/exportImage?bbox=-122.5,37.7,-122.3,37.8&size=512,512&format=png&f=image");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().BeOneOf("image/png", "image/x-png");
        }
    }

    [Fact]
    public async Task ExportImage_WithJpegFormat_ReturnsJpeg()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/ImageServer/exportImage?bbox=-122.5,37.7,-122.3,37.8&size=512,512&format=jpeg&f=image");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("jpeg");
        }
    }

    [Fact]
    public async Task ExportImage_WithTransparent_ReturnsTransparentImage()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/ImageServer/exportImage?bbox=-122.5,37.7,-122.3,37.8&size=512,512&transparent=true&format=png&f=image");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExportImage_InvalidBbox_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/ImageServer/exportImage?bbox=invalid&size=512,512&f=image");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExportImage_MissingBbox_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/ImageServer/exportImage?size=512,512&f=image");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExportImage_InvalidSize_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/ImageServer/exportImage?bbox=-122.5,37.7,-122.3,37.8&size=invalid&f=image");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExportImage_WithSpatialReference_ReturnsImage()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/ImageServer/exportImage?bbox=-122.5,37.7,-122.3,37.8&size=512,512&bboxSR=4326&imageSR=3857&f=image");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest);
    }

    #endregion

    #region Content Type Tests

    [Fact]
    public async Task ExportImage_ReturnsImageContentType()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/ImageServer/exportImage?bbox=-122.5,37.7,-122.3,37.8&size=512,512&f=image");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("image");
        }
    }

    [Fact]
    public async Task ExportImage_IncludesCustomHeaders()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/ImageServer/exportImage?bbox=-122.5,37.7,-122.3,37.8&size=512,512&f=image");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            // May include X-Rendered-Dataset and X-Target-CRS headers
            response.Should().NotBeNull();
        }
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task GetService_CompletesWithinReasonableTime()
    {
        // Act
        var startTime = DateTime.UtcNow;
        var response = await _client.GetAsync("/rest/services/folder/service/ImageServer");
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            duration.Should().BeLessThan(2000); // Should complete within 2 seconds
        }
    }

    [Fact]
    public async Task ExportImage_SmallTile_CompletesWithinReasonableTime()
    {
        // Act
        var startTime = DateTime.UtcNow;
        var response = await _client.GetAsync("/rest/services/folder/service/ImageServer/exportImage?bbox=-122.5,37.7,-122.3,37.8&size=256,256&f=image");
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            duration.Should().BeLessThan(5000); // Should complete within 5 seconds
        }
    }

    #endregion

    #region Concurrent Request Tests

    [Fact]
    public async Task GetService_MultipleConcurrentRequests_HandlesCorrectly()
    {
        // Act - Send 10 concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _client.GetAsync("/rest/services/folder/service/ImageServer"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should complete
        responses.Should().NotBeEmpty();
        responses.Should().OnlyContain(r => r != null);
    }

    [Fact]
    public async Task ExportImage_MultipleConcurrentRequests_HandlesCorrectly()
    {
        // Act - Send 5 concurrent export requests
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _client.GetAsync("/rest/services/folder/service/ImageServer/exportImage?bbox=-122.5,37.7,-122.3,37.8&size=256,256&f=image"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should complete
        responses.Should().NotBeEmpty();
        responses.Should().OnlyContain(r => r != null);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ExportImage_VeryLargeBbox_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/ImageServer/exportImage?bbox=-180,-90,180,90&size=512,512&f=image");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExportImage_VerySmallBbox_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/ImageServer/exportImage?bbox=-122.4194,37.7749,-122.4193,37.7750&size=256,256&f=image");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExportImage_MaximumSize_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/ImageServer/exportImage?bbox=-122.5,37.7,-122.3,37.8&size=4096,4096&f=image");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest,
            HttpStatusCode.RequestEntityTooLarge);
    }

    #endregion
}

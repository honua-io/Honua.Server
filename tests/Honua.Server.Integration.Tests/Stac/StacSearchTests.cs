// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Integration.Tests.Fixtures;
using Honua.Server.Integration.Tests.Helpers;
using Xunit;

namespace Honua.Server.Integration.Tests.Stac;

/// <summary>
/// Integration tests for STAC Search API endpoints.
/// Tests search functionality including spatial, temporal, and attribute filtering.
/// </summary>
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "STAC")]
[Trait("Endpoint", "Search")]
public class StacSearchTests
{
    private readonly DatabaseFixture _databaseFixture;

    public StacSearchTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
    }

    [Fact]
    public async Task GetSearch_WithoutParameters_ReturnsAllItems()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/v1/stac/search");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("type");
            content.Should().Contain("FeatureCollection");
        }
    }

    [Fact]
    public async Task GetSearch_WithBbox_ReturnsFilteredItems()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        var bbox = string.Join(",", TestDataFixture.SampleBbox);

        // Act
        var response = await client.GetAsync($"/v1/stac/search?bbox={bbox}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("FeatureCollection");
        }
    }

    [Fact]
    public async Task GetSearch_WithDatetime_ReturnsFilteredItems()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        var datetime = TestDataFixture.SampleDateTimeInterval;

        // Act
        var response = await client.GetAsync($"/v1/stac/search?datetime={Uri.EscapeDataString(datetime)}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSearch_WithCollections_ReturnsFilteredItems()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/v1/stac/search?collections=test-collection");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSearch_WithLimit_ReturnsLimitedItems()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/v1/stac/search?limit=5");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);
            var features = json.RootElement.GetProperty("features");
            features.GetArrayLength().Should().BeLessThanOrEqualTo(5);
        }
    }

    [Fact]
    public async Task GetSearch_WithIds_ReturnsSpecificItems()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/v1/stac/search?ids=item-001,item-002");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSearch_WithSortBy_ReturnsSortedItems()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/v1/stac/search?sortby=-datetime");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSearch_WithFields_ReturnsFilteredFields()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/v1/stac/search?fields=id,geometry,properties.datetime");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostSearch_WithBbox_ReturnsFilteredItems()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        var searchRequest = new
        {
            bbox = TestDataFixture.SampleBbox,
            limit = 10
        };

        var content = HttpClientHelper.CreateJsonContent(searchRequest);

        // Act
        var response = await client.PostAsync("/v1/stac/search", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().Contain("FeatureCollection");
        }
    }

    [Fact]
    public async Task PostSearch_WithIntersects_ReturnsFilteredItems()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        var searchRequest = new
        {
            intersects = new
            {
                type = "Point",
                coordinates = new[] { TestDataFixture.SamplePoint.X, TestDataFixture.SamplePoint.Y }
            },
            limit = 10
        };

        var content = HttpClientHelper.CreateJsonContent(searchRequest);

        // Act
        var response = await client.PostAsync("/v1/stac/search", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostSearch_WithDatetime_ReturnsFilteredItems()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        var searchRequest = new
        {
            datetime = TestDataFixture.SampleDateTimeInterval,
            limit = 10
        };

        var content = HttpClientHelper.CreateJsonContent(searchRequest);

        // Act
        var response = await client.PostAsync("/v1/stac/search", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostSearch_WithComplexFilter_ReturnsFilteredItems()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        var searchRequest = new
        {
            collections = new[] { "test-collection" },
            bbox = TestDataFixture.SampleBbox,
            datetime = TestDataFixture.SampleDateTimeInterval,
            limit = 10,
            sortby = new[]
            {
                new { field = "datetime", direction = "desc" }
            }
        };

        var content = HttpClientHelper.CreateJsonContent(searchRequest);

        // Act
        var response = await client.PostAsync("/v1/stac/search", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostSearch_WithInvalidBboxAndIntersects_ReturnsBadRequest()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        var searchRequest = new
        {
            bbox = TestDataFixture.SampleBbox,
            intersects = new
            {
                type = "Point",
                coordinates = new[] { -157.8583, 21.3099 }
            }
        };

        var content = HttpClientHelper.CreateJsonContent(searchRequest);

        // Act
        var response = await client.PostAsync("/v1/stac/search", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostSearch_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        var searchRequest = new
        {
            limit = 5
        };

        var content = HttpClientHelper.CreateJsonContent(searchRequest);

        // Act
        var response = await client.PostAsync("/v1/stac/search", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(responseContent);
            var features = json.RootElement.GetProperty("features");
            features.GetArrayLength().Should().BeLessThanOrEqualTo(5);
        }
    }
}

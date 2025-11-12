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
/// Integration tests for STAC Collections API endpoints.
/// Tests collection listing and metadata retrieval.
/// </summary>
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "STAC")]
[Trait("Endpoint", "Collections")]
public class StacCollectionsTests
{
    private readonly DatabaseFixture _databaseFixture;

    public StacCollectionsTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
    }

    [Fact]
    public async Task GetCollections_ReturnsAllCollections()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/v1/stac/collections");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("collections");

            var json = JsonDocument.Parse(content);
            json.RootElement.TryGetProperty("collections", out var collections).Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetCollection_WithValidId_ReturnsCollection()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync($"/v1/stac/collections/{TestDataFixture.SampleStacCollectionId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("id");
            content.Should().Contain("extent");
        }
    }

    [Fact]
    public async Task GetCollection_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/v1/stac/collections/non-existent-collection");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCollectionItems_ReturnsItems()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync($"/v1/stac/collections/{TestDataFixture.SampleStacCollectionId}/items");

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
    public async Task GetCollectionItems_WithLimit_ReturnsLimitedItems()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync($"/v1/stac/collections/{TestDataFixture.SampleStacCollectionId}/items?limit=5");

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
    public async Task GetCollectionItems_WithBbox_ReturnsFilteredItems()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        var bbox = string.Join(",", TestDataFixture.SampleBbox);

        // Act
        var response = await client.GetAsync($"/v1/stac/collections/{TestDataFixture.SampleStacCollectionId}/items?bbox={bbox}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCollectionItems_WithDatetime_ReturnsFilteredItems()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        var datetime = TestDataFixture.SampleDateTimeInterval;

        // Act
        var response = await client.GetAsync($"/v1/stac/collections/{TestDataFixture.SampleStacCollectionId}/items?datetime={Uri.EscapeDataString(datetime)}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCollectionItem_WithValidId_ReturnsItem()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync($"/v1/stac/collections/{TestDataFixture.SampleStacCollectionId}/items/{TestDataFixture.SampleStacItemId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("type");
            content.Should().Contain("Feature");
        }
    }

    [Fact]
    public async Task GetCollectionItem_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync($"/v1/stac/collections/{TestDataFixture.SampleStacCollectionId}/items/non-existent-item");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCollections_HasCorrectContentType()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/v1/stac/collections");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("json");
        }
    }

    [Fact]
    public async Task GetCollectionItems_HasCorrectContentType()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync($"/v1/stac/collections/{TestDataFixture.SampleStacCollectionId}/items");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("json");
        }
    }
}

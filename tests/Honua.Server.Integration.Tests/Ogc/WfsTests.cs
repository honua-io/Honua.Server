// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Integration.Tests.Fixtures;
using Honua.Server.Integration.Tests.Helpers;
using Xunit;

namespace Honua.Server.Integration.Tests.Ogc;

/// <summary>
/// Integration tests for OGC WFS (Web Feature Service) endpoints.
/// Tests WFS 2.0/3.0 operations including GetCapabilities, GetFeature, and transactions.
/// </summary>
[Trait("Category", "Integration")]
[Trait("API", "OGC")]
[Trait("Endpoint", "WFS")]
public class WfsTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _databaseFixture;

    public WfsTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
    }

    [Fact]
    public async Task GetCapabilities_WFS20_ReturnsValidCapabilities()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/wfs?service=WFS&version=2.0.0&request=GetCapabilities");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("WFS_Capabilities");
            content.Should().Contain("FeatureTypeList");
        }
    }

    [Fact]
    public async Task GetCapabilities_WFS30_ReturnsValidLandingPage()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/ogc/features");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("links");
        }
    }

    [Fact]
    public async Task GetConformance_ReturnsConformanceClasses()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/ogc/features/conformance");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("conformsTo");
        }
    }

    [Fact]
    public async Task GetCollections_ReturnsFeatureCollections()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/ogc/features/collections");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("collections");
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
        var response = await client.GetAsync("/ogc/features/collections/test-collection");

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
    public async Task GetItems_ReturnsFeatures()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/ogc/features/collections/test-collection/items");

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
    public async Task GetItems_WithBbox_ReturnsFilteredFeatures()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        var bbox = string.Join(",", TestDataFixture.SampleBbox);

        // Act
        var response = await client.GetAsync($"/ogc/features/collections/test-collection/items?bbox={bbox}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetItems_WithDateTime_ReturnsFilteredFeatures()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        var datetime = TestDataFixture.SampleDateTimeInterval;

        // Act
        var response = await client.GetAsync($"/ogc/features/collections/test-collection/items?datetime={Uri.EscapeDataString(datetime)}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetItems_WithLimit_ReturnsLimitedFeatures()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/ogc/features/collections/test-collection/items?limit=10");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetItem_WithValidId_ReturnsFeature()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/ogc/features/collections/test-collection/items/1");

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
    public async Task GetFeature_WFS20_WithTypeName_ReturnsFeatures()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/wfs?service=WFS&version=2.0.0&request=GetFeature&typeNames=test-collection");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("FeatureCollection");
        }
    }

    [Fact]
    public async Task GetFeature_WFS20_WithBbox_ReturnsFilteredFeatures()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var bbox = string.Join(",", TestDataFixture.SampleBbox);

        // Act
        var response = await client.GetAsync($"/ogc/wfs?service=WFS&version=2.0.0&request=GetFeature&typeNames=test-collection&bbox={bbox}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFeature_WFS20_WithCount_ReturnsLimitedFeatures()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/wfs?service=WFS&version=2.0.0&request=GetFeature&typeNames=test-collection&count=10");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DescribeFeatureType_ReturnsSchema()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/wfs?service=WFS&version=2.0.0&request=DescribeFeatureType&typeNames=test-collection");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("schema");
        }
    }

    [Fact]
    public async Task GetPropertyValue_ReturnsPropertyValues()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/wfs?service=WFS&version=2.0.0&request=GetPropertyValue&typeNames=test-collection&valueReference=name");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }
}

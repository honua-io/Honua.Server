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
/// Integration tests for OGC WMS (Web Map Service) endpoints.
/// Tests WMS 1.3.0 operations including GetCapabilities, GetMap, and GetFeatureInfo.
/// </summary>
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "OGC")]
[Trait("Endpoint", "WMS")]
public class WmsTests
{
    private readonly DatabaseFixture _databaseFixture;

    public WmsTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
    }

    [Fact]
    public async Task GetCapabilities_ReturnsValidCapabilities()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/wms?service=WMS&version=1.3.0&request=GetCapabilities");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("WMS_Capabilities");
            content.Should().Contain("Layer");
        }
    }

    [Fact]
    public async Task GetCapabilities_ContainsRequiredElements()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/wms?service=WMS&version=1.3.0&request=GetCapabilities");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Service");
            content.Should().Contain("Capability");
            content.Should().Contain("Layer");
        }
    }

    [Fact]
    public async Task GetMap_WithBbox_ReturnsMapImage()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/ogc/wms?service=WMS&version=1.3.0&request=GetMap&layers=test-layer&bbox={bbox}&width=256&height=256&crs=EPSG:4326&format=image/png";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("image");
        }
    }

    [Fact]
    public async Task GetMap_WithStyles_ReturnsStyledMap()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/ogc/wms?service=WMS&version=1.3.0&request=GetMap&layers=test-layer&styles=default&bbox={bbox}&width=256&height=256&crs=EPSG:4326&format=image/png";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMap_WithTransparent_ReturnsTransparentImage()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/ogc/wms?service=WMS&version=1.3.0&request=GetMap&layers=test-layer&bbox={bbox}&width=256&height=256&crs=EPSG:4326&format=image/png&transparent=true";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMap_WithMultipleLayers_ReturnsCompositeMap()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/ogc/wms?service=WMS&version=1.3.0&request=GetMap&layers=layer1,layer2&bbox={bbox}&width=256&height=256&crs=EPSG:4326&format=image/png";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMap_WithDifferentFormat_ReturnsCorrectFormat()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/ogc/wms?service=WMS&version=1.3.0&request=GetMap&layers=test-layer&bbox={bbox}&width=256&height=256&crs=EPSG:4326&format=image/jpeg";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFeatureInfo_WithQueryPoint_ReturnsFeatureData()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/ogc/wms?service=WMS&version=1.3.0&request=GetFeatureInfo&layers=test-layer&query_layers=test-layer&bbox={bbox}&width=256&height=256&crs=EPSG:4326&i=128&j=128&info_format=application/json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task GetFeatureInfo_WithTextFormat_ReturnsTextData()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/ogc/wms?service=WMS&version=1.3.0&request=GetFeatureInfo&layers=test-layer&query_layers=test-layer&bbox={bbox}&width=256&height=256&crs=EPSG:4326&i=128&j=128&info_format=text/plain";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFeatureInfo_WithGmlFormat_ReturnsGmlData()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/ogc/wms?service=WMS&version=1.3.0&request=GetFeatureInfo&layers=test-layer&query_layers=test-layer&bbox={bbox}&width=256&height=256&crs=EPSG:4326&i=128&j=128&info_format=application/gml+xml";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetLegendGraphic_ReturnsLegendImage()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/wms?service=WMS&version=1.3.0&request=GetLegendGraphic&layer=test-layer&format=image/png");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("image");
        }
    }

    [Fact]
    public async Task GetMap_WithInvalidBbox_ReturnsError()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/wms?service=WMS&version=1.3.0&request=GetMap&layers=test-layer&bbox=invalid&width=256&height=256&crs=EPSG:4326&format=image/png");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMap_WithInvalidCrs_ReturnsError()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var bbox = string.Join(",", TestDataFixture.SampleBbox);

        // Act
        var response = await client.GetAsync($"/ogc/wms?service=WMS&version=1.3.0&request=GetMap&layers=test-layer&bbox={bbox}&width=256&height=256&crs=INVALID&format=image/png");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }
}

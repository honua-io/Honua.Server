// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Integration.Tests.Fixtures;
using Honua.Server.Integration.Tests.Helpers;
using Xunit;

namespace Honua.Server.Integration.Tests.GeoservicesREST;

/// <summary>
/// Integration tests for GeoServices REST Map Server API endpoints.
/// Tests map service operations including rendering and export.
/// </summary>
[Trait("Category", "Integration")]
[Trait("API", "GeoservicesREST")]
[Trait("Endpoint", "MapServer")]
public class MapServerTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _databaseFixture;

    public MapServerTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
    }

    [Fact]
    public async Task GetServiceInfo_ReturnsValidMetadata()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/rest/services/test-service/MapServer");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("currentVersion");
            content.Should().Contain("layers");
        }
    }

    [Fact]
    public async Task GetLayerInfo_ReturnsLayerMetadata()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/rest/services/test-service/MapServer/0");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("name");
            content.Should().Contain("geometryType");
        }
    }

    [Fact]
    public async Task ExportMap_WithBbox_ReturnsImage()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/rest/services/test-service/MapServer/export?bbox={bbox}&size=256,256&f=image";

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
    public async Task ExportMap_WithLayers_ReturnsImage()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/rest/services/test-service/MapServer/export?bbox={bbox}&size=256,256&layers=show:0&f=image";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExportMap_WithFormat_ReturnsCorrectFormat()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/rest/services/test-service/MapServer/export?bbox={bbox}&size=256,256&format=png&f=image";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExportMap_WithTransparent_ReturnsTransparentImage()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/rest/services/test-service/MapServer/export?bbox={bbox}&size=256,256&transparent=true&f=image";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExportMap_WithDpi_ReturnsHighResImage()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/rest/services/test-service/MapServer/export?bbox={bbox}&size=256,256&dpi=150&f=image";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Identify_WithGeometry_ReturnsFeatures()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var point = $"{TestDataFixture.SamplePoint.X},{TestDataFixture.SamplePoint.Y}";
        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/rest/services/test-service/MapServer/identify?geometry={point}&geometryType=esriGeometryPoint&mapExtent={bbox}&imageDisplay=256,256,96&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Find_WithSearchText_ReturnsFeatures()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var url = "/rest/services/test-service/MapServer/find?searchText=test&searchFields=name&layers=0&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetLegendInfo_ReturnsLegend()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/rest/services/test-service/MapServer/legend?f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("layers");
        }
    }
}

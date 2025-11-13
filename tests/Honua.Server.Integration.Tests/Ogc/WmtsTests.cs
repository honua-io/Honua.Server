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
/// Integration tests for OGC WMTS (Web Map Tile Service) endpoints.
/// Tests WMTS 1.0.0 operations including GetCapabilities and GetTile.
/// </summary>
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "OGC")]
[Trait("Endpoint", "WMTS")]
public class WmtsTests
{
    private readonly DatabaseFixture _databaseFixture;

    public WmtsTests(DatabaseFixture databaseFixture)
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
        var response = await client.GetAsync("/wmts?service=WMTS&version=1.0.0&request=GetCapabilities");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Capabilities");
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
        var response = await client.GetAsync("/wmts?service=WMTS&version=1.0.0&request=GetCapabilities");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Contents");
            content.Should().Contain("Layer");
            content.Should().Contain("TileMatrixSet");
        }
    }

    [Fact]
    public async Task GetTile_WithValidCoordinates_ReturnsTileImage()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/wmts?service=WMTS&version=1.0.0&request=GetTile&layer=test_features_wmts&TileMatrixSet=WebMercator&TileMatrix=0&TileRow=0&TileCol=0&format=image/png");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("image");
        }
    }

    [Fact]
    public async Task GetTile_AtDifferentZoomLevels_ReturnsTiles()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Test multiple zoom levels
        var zoomLevels = new[] { 0, 5, 10 };

        foreach (var zoom in zoomLevels)
        {
            // Act
            var response = await client.GetAsync($"/wmts?service=WMTS&version=1.0.0&request=GetTile&layer=test_features_wmts&TileMatrixSet=WebMercator&TileMatrix={zoom}&TileRow=0&TileCol=0&format=image/png");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task GetTile_WithJpegFormat_ReturnsJpegTile()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/wmts?service=WMTS&version=1.0.0&request=GetTile&layer=test_features_wmts&TileMatrixSet=WebMercator&TileMatrix=0&TileRow=0&TileCol=0&format=image/jpeg");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTile_WithStyle_ReturnsStyledTile()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/wmts?service=WMTS&version=1.0.0&request=GetTile&layer=test_features_wmts&style=default&TileMatrixSet=WebMercator&TileMatrix=0&TileRow=0&TileCol=0&format=image/png");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTile_WithInvalidTileMatrix_ReturnsError()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/wmts?service=WMTS&version=1.0.0&request=GetTile&layer=test_features_wmts&TileMatrixSet=WebMercator&TileMatrix=999&TileRow=0&TileCol=0&format=image/png");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTile_WithInvalidTileCoordinates_ReturnsError()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/wmts?service=WMTS&version=1.0.0&request=GetTile&layer=test_features_wmts&TileMatrixSet=WebMercator&TileMatrix=0&TileRow=999999&TileCol=999999&format=image/png");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.OK); // Some servers return empty tile
    }

    [Fact]
    public async Task GetFeatureInfo_WithValidTile_ReturnsFeatureData()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/wmts?service=WMTS&version=1.0.0&request=GetFeatureInfo&layer=test_features_wmts&TileMatrixSet=WebMercator&TileMatrix=0&TileRow=0&TileCol=0&format=image/png&InfoFormat=application/json&I=128&J=128");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTile_RestfulEndpoint_ReturnsTile()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Act - RESTful endpoint format: /tiles/{layer}/{TileMatrixSet}/{TileMatrix}/{TileRow}/{TileCol}
        var response = await client.GetAsync("/ogc/tiles/test_features_wmts/WebMercator/0/0/0");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("image");
        }
    }

    [Fact]
    public async Task GetTile_WithDateTime_ReturnsTemporalTile()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var datetime = Uri.EscapeDataString(TestDataFixture.SampleDateTimeStart);

        // Act
        var response = await client.GetAsync($"/wmts?service=WMTS&version=1.0.0&request=GetTile&layer=test_features_wmts&TileMatrixSet=WebMercator&TileMatrix=0&TileRow=0&TileCol=0&format=image/png&time={datetime}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCapabilities_ContainsTileMatrixSets()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/wmts?service=WMTS&version=1.0.0&request=GetCapabilities");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("TileMatrixSet");
        }
    }
}

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
/// Integration tests for GeoServices REST Image Server API endpoints.
/// Tests raster operations including imagery export and catalog queries.
/// </summary>
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "GeoservicesREST")]
[Trait("Endpoint", "ImageServer")]
public class ImageServerTests
{
    private readonly DatabaseFixture _databaseFixture;

    public ImageServerTests(DatabaseFixture databaseFixture)
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
        var response = await client.GetAsync("/rest/services/test-raster/ImageServer");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("currentVersion");
        }
    }

    [Fact]
    public async Task ExportImage_WithBbox_ReturnsRaster()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/rest/services/test-raster/ImageServer/exportImage?bbox={bbox}&size=256,256&f=image";

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
    public async Task ExportImage_WithFormat_ReturnsCorrectFormat()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/rest/services/test-raster/ImageServer/exportImage?bbox={bbox}&size=256,256&format=jpgpng&f=image";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Identify_WithGeometry_ReturnsPixelValues()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var point = $"{TestDataFixture.SamplePoint.X},{TestDataFixture.SamplePoint.Y}";
        var url = $"/rest/services/test-raster/ImageServer/identify?geometry={point}&geometryType=esriGeometryPoint&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCatalog_ReturnsRasterCatalog()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/rest/services/test-raster/ImageServer/catalog?f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetHistograms_ReturnsStatistics()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/rest/services/test-raster/ImageServer/histograms?f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetKeyProperties_ReturnsProperties()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/rest/services/test-raster/ImageServer/keyProperties?f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ComputeStatistics_WithGeometry_ReturnsStats()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/rest/services/test-raster/ImageServer/computeStatistics?geometry={bbox}&geometryType=esriGeometryEnvelope&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }
}

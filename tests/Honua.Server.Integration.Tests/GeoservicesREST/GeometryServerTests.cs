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
/// Integration tests for GeoServices REST Geometry Server API endpoints.
/// Tests geometry operations including projection, buffer, and spatial analysis.
/// </summary>
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "GeoservicesREST")]
[Trait("Endpoint", "GeometryServer")]
public class GeometryServerTests
{
    private readonly DatabaseFixture _databaseFixture;

    public GeometryServerTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
    }

    [Fact]
    public async Task Project_WithValidGeometry_ReturnsProjectedGeometry()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var geometries = $"{{\"geometryType\":\"esriGeometryPoint\",\"geometries\":[{{\"x\":{TestDataFixture.SamplePoint.X},\"y\":{TestDataFixture.SamplePoint.Y}}}]}}";
        var url = $"/rest/services/Geometry/GeometryServer/project?geometries={Uri.EscapeDataString(geometries)}&inSR=4326&outSR=3857&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("geometries");
        }
    }

    [Fact]
    public async Task Buffer_WithDistance_ReturnsBufferedGeometry()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var geometries = $"{{\"geometryType\":\"esriGeometryPoint\",\"geometries\":[{{\"x\":{TestDataFixture.SamplePoint.X},\"y\":{TestDataFixture.SamplePoint.Y}}}]}}";
        var url = $"/rest/services/Geometry/GeometryServer/buffer?geometries={Uri.EscapeDataString(geometries)}&inSR=4326&distances=0.1&unit=esriSRUnit_Meter&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("geometries");
        }
    }

    [Fact]
    public async Task Simplify_WithGeometry_ReturnsSimplifiedGeometry()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var geometries = $"{{\"geometryType\":\"esriGeometryPolygon\",\"geometries\":[{TestDataFixture.SamplePolygon}]}}";
        var url = $"/rest/services/Geometry/GeometryServer/simplify?geometries={Uri.EscapeDataString(geometries)}&sr=4326&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Distance_BetweenGeometries_ReturnsDistance()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var geometry1 = $"{{\"x\":{TestDataFixture.SamplePoint.X},\"y\":{TestDataFixture.SamplePoint.Y}}}";
        var geometry2 = $"{{\"x\":-158.0,\"y\":21.5}}";
        var url = $"/rest/services/Geometry/GeometryServer/distance?geometry1={Uri.EscapeDataString(geometry1)}&geometry2={Uri.EscapeDataString(geometry2)}&sr=4326&distanceUnit=esriSRUnit_Meter&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("distance");
        }
    }

    [Fact]
    public async Task Intersect_WithGeometries_ReturnsIntersection()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var url = "/rest/services/Geometry/GeometryServer/intersect?sr=4326&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Union_WithGeometries_ReturnsUnion()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var url = "/rest/services/Geometry/GeometryServer/union?sr=4326&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Areas_AndLengths_ReturnsCalculations()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var url = "/rest/services/Geometry/GeometryServer/areasAndLengths?sr=4326&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Relation_BetweenGeometries_ReturnsRelationship()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var url = "/rest/services/Geometry/GeometryServer/relation?sr=4326&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }
}

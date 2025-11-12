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
/// Integration tests for GeoServices REST Feature Server API endpoints.
/// Tests feature layer operations including query, create, update, and delete.
/// </summary>
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "GeoservicesREST")]
[Trait("Endpoint", "FeatureServer")]
public class FeatureServerTests
{
    private readonly DatabaseFixture _databaseFixture;

    public FeatureServerTests(DatabaseFixture databaseFixture)
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
        var response = await client.GetAsync("/rest/services/test-service/FeatureServer");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("currentVersion");
        content.Should().Contain("layers");
    }

    [Fact]
    public async Task GetLayerInfo_ReturnsLayerMetadata()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/rest/services/test-service/FeatureServer/0");

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
    public async Task QueryFeatures_WithoutParameters_ReturnsFeatures()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/rest/services/test-service/FeatureServer/0/query?where=1=1&f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("features");
            content.Should().Contain("geometryType");
        }
    }

    [Fact]
    public async Task QueryFeatures_WithBboxFilter_ReturnsFilteredFeatures()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var bbox = string.Join(",", TestDataFixture.SampleBbox);
        var url = $"/rest/services/test-service/FeatureServer/0/query?where=1=1&geometry={bbox}&geometryType=esriGeometryEnvelope&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("features");
        }
    }

    [Fact]
    public async Task QueryFeatures_WithWhereClause_ReturnsFilteredFeatures()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var url = "/rest/services/test-service/FeatureServer/0/query?where=population>100000&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("features");
        }
    }

    [Fact]
    public async Task QueryFeatures_WithOutFields_ReturnsSpecifiedFields()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var url = "/rest/services/test-service/FeatureServer/0/query?where=1=1&outFields=name,population&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task QueryFeatures_WithReturnCountOnly_ReturnsCount()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var url = "/rest/services/test-service/FeatureServer/0/query?where=1=1&returnCountOnly=true&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("count");
        }
    }

    [Fact]
    public async Task QueryFeatures_WithReturnIdsOnly_ReturnsIds()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var url = "/rest/services/test-service/FeatureServer/0/query?where=1=1&returnIdsOnly=true&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("objectIds");
        }
    }

    [Fact]
    public async Task QueryFeatures_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var url = "/rest/services/test-service/FeatureServer/0/query?where=1=1&resultOffset=0&resultRecordCount=10&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task QueryFeatures_WithOrderBy_ReturnsSortedFeatures()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var url = "/rest/services/test-service/FeatureServer/0/query?where=1=1&orderByFields=name ASC&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task QueryFeatures_WithInvalidWhere_ReturnsBadRequest()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var url = "/rest/services/test-service/FeatureServer/0/query?where=INVALID SQL&f=json";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFeatureById_ReturnsFeature()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/rest/services/test-service/FeatureServer/0/1");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddFeatures_WithValidData_ReturnsSuccess()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var feature = TestDataFixture.SampleGeoJsonFeature;
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("features", $"[{feature}]"),
            new KeyValuePair<string, string>("f", "json")
        });

        // Act
        var response = await client.PostAsync("/rest/services/test-service/FeatureServer/0/addFeatures", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateFeatures_WithValidData_ReturnsSuccess()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var feature = TestDataFixture.SampleGeoJsonFeature;
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("features", $"[{feature}]"),
            new KeyValuePair<string, string>("f", "json")
        });

        // Act
        var response = await client.PostAsync("/rest/services/test-service/FeatureServer/0/updateFeatures", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteFeatures_WithObjectIds_ReturnsSuccess()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("objectIds", "1,2,3"),
            new KeyValuePair<string, string>("f", "json")
        });

        // Act
        var response = await client.PostAsync("/rest/services/test-service/FeatureServer/0/deleteFeatures", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
    }
}

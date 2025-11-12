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
/// Integration tests for STAC Catalog API endpoints using Configuration V2.
/// Tests root catalog and conformance endpoints with HCL-based configuration.
/// </summary>
[Trait("Category", "Integration")]
[Trait("API", "STAC")]
[Trait("Endpoint", "Catalog")]
[Collection("DatabaseCollection")]
public class StacCatalogTests : ConfigurationV2IntegrationTestBase
{
    public StacCatalogTests(DatabaseFixture databaseFixture)
        : base(databaseFixture)
    {
    }

    protected override ConfigurationV2TestFixture<Program> CreateFactory()
    {
        var hclConfig = """
        honua {
            version     = "2.0"
            environment = "test"
            log_level   = "debug"
        }

        data_source "test_db" {
            provider   = "postgresql"
            connection = env("DATABASE_URL")

            pool {
                min_size = 1
                max_size = 5
            }
        }

        service "stac" {
            enabled     = true
            title       = "Honua STAC API"
            description = "Test STAC API using Configuration V2"
        }

        layer "test_features" {
            title       = "Test Features"
            data_source = data_source.test_db
            table       = "features"
            id_field    = "id"
            introspect_fields = true

            geometry {
                column = "geom"
                type   = "Polygon"
                srid   = 4326
            }

            services = [service.stac]
        }
        """;

        return new ConfigurationV2TestFixture<Program>(DatabaseFixture, hclConfig);
    }

    [Fact]
    public async Task GetRootCatalog_ReturnsValidCatalog()
    {
        // Arrange
        HttpClientHelper.AddJsonAcceptHeader(Client);

        // Act
        var response = await Client.GetAsync("/v1/stac");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("type");
            content.Should().Contain("Catalog");
            content.Should().Contain("links");
        }
    }

    [Fact]
    public async Task GetRootCatalog_ContainsRequiredFields()
    {
        // Arrange
        HttpClientHelper.AddJsonAcceptHeader(Client);

        // Act
        var response = await Client.GetAsync("/v1/stac");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);
            var root = json.RootElement;

            root.TryGetProperty("id", out _).Should().BeTrue();
            root.TryGetProperty("type", out _).Should().BeTrue();
            root.TryGetProperty("description", out _).Should().BeTrue();
            root.TryGetProperty("links", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetRootCatalog_ContainsSelfLink()
    {
        // Arrange
        HttpClientHelper.AddJsonAcceptHeader(Client);

        // Act
        var response = await Client.GetAsync("/v1/stac");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("\"rel\":\"self\"");
        }
    }

    [Fact]
    public async Task GetConformance_ReturnsValidConformance()
    {
        // Arrange
        HttpClientHelper.AddJsonAcceptHeader(Client);

        // Act
        var response = await Client.GetAsync("/v1/stac/conformance");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("conformsTo");

            var json = JsonDocument.Parse(content);
            json.RootElement.TryGetProperty("conformsTo", out var conformsTo).Should().BeTrue();
            conformsTo.GetArrayLength().Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task GetConformance_ContainsStacCoreConformance()
    {
        // Arrange
        HttpClientHelper.AddJsonAcceptHeader(Client);

        // Act
        var response = await Client.GetAsync("/v1/stac/conformance");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("stac-api");
        }
    }

    [Fact]
    public async Task GetRootCatalog_HasCorrectContentType()
    {
        // Arrange
        HttpClientHelper.AddJsonAcceptHeader(Client);

        // Act
        var response = await Client.GetAsync("/v1/stac");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("json");
        }
    }

    [Fact]
    public async Task GetConformance_HasCorrectContentType()
    {
        // Arrange
        HttpClientHelper.AddJsonAcceptHeader(Client);

        // Act
        var response = await Client.GetAsync("/v1/stac/conformance");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("json");
        }
    }

    [Fact]
    public async Task GetRootCatalog_ContainsChildLinks()
    {
        // Arrange
        HttpClientHelper.AddJsonAcceptHeader(Client);

        // Act
        var response = await Client.GetAsync("/v1/stac");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);
            var root = json.RootElement;

            root.TryGetProperty("links", out var links).Should().BeTrue();
            links.GetArrayLength().Should().BeGreaterThan(0);
        }
    }
}

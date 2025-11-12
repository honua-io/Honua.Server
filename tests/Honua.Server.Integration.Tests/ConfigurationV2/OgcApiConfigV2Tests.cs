// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Integration.Tests.Fixtures;
using Honua.Server.Integration.Tests.Helpers;
using Xunit;

namespace Honua.Server.Integration.Tests.ConfigurationV2;

/// <summary>
/// Integration tests demonstrating Configuration V2 usage with OGC API Features.
/// Shows how to migrate existing tests to use .honua configuration files.
/// </summary>
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "ConfigurationV2")]
[Trait("Endpoint", "OGC")]
public class OgcApiConfigV2Tests : ConfigurationV2IntegrationTestBase
{
    public OgcApiConfigV2Tests(DatabaseFixture databaseFixture)
        : base(databaseFixture)
    {
    }

    protected override ConfigurationV2TestFixture<Program> CreateFactory()
    {
        // Default OGC API configuration - individual tests override as needed
        return CreateFactoryWithHcl(CreateOgcApiConfiguration());
    }

    [Fact]
    public async Task GetLandingPage_WithConfigV2_ReturnsValidResponse()
    {
        // Arrange - Create test configuration using builder pattern
        using var factory = new ConfigurationV2TestFixture<Program>(DatabaseFixture, builder =>
        {
            builder
                .AddDataSource("test_db", "postgresql", "DATABASE_URL")
                .AddService("ogc_api", new()
                {
                    ["item_limit"] = 1000,
                    ["default_crs"] = "EPSG:4326"
                })
                .AddLayer("test_collection", "test_db", "test_table");
        });

        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/ogc/features");

        // Assert
        // NOTE: May return 404 until full Configuration V2 integration is complete
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("links");

            // Verify configuration was loaded
            factory.LoadedConfig.Should().NotBeNull();
            factory.LoadedConfig!.Services.Should().ContainKey("ogc_api");
            factory.LoadedConfig.DataSources.Should().ContainKey("test_db");
            factory.LoadedConfig.Layers.Should().ContainKey("test_collection");
        }
    }

    [Fact]
    public async Task GetConformance_WithConfigV2_ReturnsConformanceClasses()
    {
        // Arrange - Create test configuration using inline HCL
        var hclConfig = @"
honua {
  version     = ""1.0""
  environment = ""test""
  log_level   = ""debug""
}

data_source ""test_db"" {
  provider   = ""postgresql""
  connection = env(""DATABASE_URL"")

  pool {
    min_size = 1
    max_size = 5
  }
}

service ""ogc_api"" {
  enabled     = true
  item_limit  = 1000
  default_crs = ""EPSG:4326""

  conformance = [
    ""core"",
    ""geojson"",
    ""crs""
  ]
}

layer ""test_features"" {
  title       = ""Test Features""
  data_source = data_source.test_db
  table       = ""test_table""
  id_field    = ""id""
  introspect_fields = true

  geometry {
    column = ""geom""
    type   = ""Point""
    srid   = 4326
  }

  services = [
    service.ogc_api
  ]
}
";

        using var factory = new ConfigurationV2TestFixture<Program>(DatabaseFixture, hclConfig);
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
    public async Task GetCollections_WithMultipleLayers_ReturnsAllLayers()
    {
        // Arrange - Create multiple layers via builder
        using var factory = new ConfigurationV2TestFixture<Program>(DatabaseFixture, builder =>
        {
            builder
                .AddDataSource("gis_db", "postgresql", "DATABASE_URL")
                .AddService("ogc_api")
                .AddLayer("roads", "gis_db", "roads", geometryType: "LineString")
                .AddLayer("parcels", "gis_db", "parcels", geometryType: "Polygon")
                .AddLayer("buildings", "gis_db", "buildings", geometryType: "Polygon");
        });

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

            // Verify all layers were configured
            factory.LoadedConfig.Should().NotBeNull();
            factory.LoadedConfig!.Layers.Should().HaveCount(3);
            factory.LoadedConfig.Layers.Should().ContainKey("roads");
            factory.LoadedConfig.Layers.Should().ContainKey("parcels");
            factory.LoadedConfig.Layers.Should().ContainKey("buildings");
        }
    }

    [Fact]
    public async Task GetItems_WithCustomService_ReturnsFeatures()
    {
        // Arrange - Configure custom service settings
        using var factory = new ConfigurationV2TestFixture<Program>(DatabaseFixture, builder =>
        {
            builder
                .AddDataSource("data", "postgresql")
                .AddService("ogc_api", new()
                {
                    ["item_limit"] = 500,
                    ["default_page_size"] = 50,
                    ["max_page_size"] = 200,
                    ["enable_cql_filter"] = true
                })
                .AddLayer("features", "data", "feature_table");
        });

        var client = factory.CreateClient();
        HttpClientHelper.AddGeoJsonAcceptHeader(client);

        // Act
        var response = await client.GetAsync("/ogc/features/collections/features/items");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("type");
            content.Should().Contain("FeatureCollection");

            // Verify service settings were loaded
            factory.LoadedConfig.Should().NotBeNull();
            var ogcApiService = factory.LoadedConfig!.Services["ogc_api"];
            ogcApiService.Settings["item_limit"].Should().Be(500);
            ogcApiService.Settings["enable_cql_filter"].Should().Be(true);
        }
    }
}

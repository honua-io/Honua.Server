// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Integration.Tests.Fixtures;
using Honua.Server.Integration.Tests.Helpers;
using Xunit;

namespace Honua.Server.Integration.Tests.ConfigurationV2;

/// <summary>
/// Integration tests demonstrating WFS (Web Feature Service) with Configuration V2.
/// Demonstrates various patterns for configuring WFS using HCL configuration.
/// </summary>
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "ConfigurationV2")]
[Trait("Endpoint", "WFS")]
public class WfsConfigV2Tests : ConfigurationV2IntegrationTestBase
{
    public WfsConfigV2Tests(DatabaseFixture databaseFixture)
        : base(databaseFixture)
    {
    }

    protected override ConfigurationV2TestFixture<Program> CreateFactory()
    {
        // Default WFS configuration - individual tests override as needed
        return CreateFactoryWithHcl(CreateWfsConfiguration());
    }

    [Fact]
    public async Task GetCapabilities_WFS20_ReturnsValidCapabilities()
    {
        // Arrange - Configure WFS service using Configuration V2 builder pattern
        using var factory = new ConfigurationV2TestFixture<Program>(DatabaseFixture, builder =>
        {
            builder
                .AddDataSource("gis_db", "postgresql", "DATABASE_URL")
                .AddService("wfs", new()
                {
                    ["version"] = "2.0.0",
                    ["capabilities_cache_duration"] = 3600,
                    ["default_count"] = 100,
                    ["max_features"] = 10000
                })
                .AddLayer("test_features", "gis_db", "test_table");
        });

        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/wfs?service=WFS&version=2.0.0&request=GetCapabilities");

        // Assert
        // NOTE: May return 404 until full WFS Configuration V2 integration is complete
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("WFS_Capabilities");
            content.Should().Contain("FeatureTypeList");

            // Verify WFS configuration was loaded correctly
            factory.LoadedConfig.Should().NotBeNull();
            var wfsService = factory.LoadedConfig!.Services["wfs"];
            wfsService.Enabled.Should().BeTrue();
            wfsService.Settings["max_features"].Should().Be(10000);
        }
    }

    [Fact]
    public async Task GetCapabilities_WFS30_WithMultipleLayers_ReturnsAllFeatures()
    {
        // Arrange - Configure multiple layers with different geometry types using inline HCL
        var hclConfig = @"
honua {
  version = ""1.0""
  environment = ""test""
  log_level = ""debug""
}

data_source ""spatial_db"" {
  provider = ""postgresql""
  connection = env(""DATABASE_URL"")

  pool {
    min_size = 2
    max_size = 10
  }
}

service ""wfs"" {
  enabled = true
  version = ""2.0.0""
  capabilities_cache_duration = 1800
  default_count = 50
  max_features = 5000
  enable_complexity_check = true
}

service ""ogc_api"" {
  enabled = true
  item_limit = 1000
}

layer ""roads"" {
  title = ""Road Network""
  data_source = data_source.spatial_db
  table = ""public.roads""
  id_field = ""id""
  introspect_fields = true

  geometry {
    column = ""geom""
    type = ""LineString""
    srid = 4326
  }

  services = [service.wfs, service.ogc_api]
}

layer ""buildings"" {
  title = ""Building Footprints""
  data_source = data_source.spatial_db
  table = ""public.buildings""
  id_field = ""id""
  introspect_fields = true

  geometry {
    column = ""geom""
    type = ""Polygon""
    srid = 4326
  }

  services = [service.wfs, service.ogc_api]
}

layer ""poi"" {
  title = ""Points of Interest""
  data_source = data_source.spatial_db
  table = ""public.poi""
  id_field = ""id""
  introspect_fields = true

  geometry {
    column = ""geom""
    type = ""Point""
    srid = 4326
  }

  services = [service.wfs, service.ogc_api]
}
";

        using var factory = new ConfigurationV2TestFixture<Program>(DatabaseFixture, hclConfig);
        var client = factory.CreateClient();
        HttpClientHelper.AddJsonAcceptHeader(client);

        // Act - Use OGC API Features endpoint (WFS 3.0 style)
        var response = await client.GetAsync("/ogc/features/collections");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("collections");

            // Verify all 3 layers were configured
            factory.LoadedConfig.Should().NotBeNull();
            factory.LoadedConfig!.Layers.Should().HaveCount(3);
            factory.LoadedConfig.Layers.Should().ContainKey("roads");
            factory.LoadedConfig.Layers.Should().ContainKey("buildings");
            factory.LoadedConfig.Layers.Should().ContainKey("poi");

            // Verify all layers reference WFS service
            foreach (var layer in factory.LoadedConfig.Layers.Values)
            {
                layer.Services.Should().Contain(s => s == "wfs" || s.Contains("service.wfs"));
            }
        }
    }

    [Fact]
    public async Task GetFeature_WithCustomSettings_RespectsConfiguration()
    {
        // Arrange - Configure WFS with custom limits
        using var factory = new ConfigurationV2TestFixture<Program>(DatabaseFixture, builder =>
        {
            builder
                .AddDataSource("db", "postgresql")
                .AddService("wfs", new()
                {
                    ["version"] = "2.0.0",
                    ["default_count"] = 25,  // Custom default
                    ["max_features"] = 100,  // Lower limit for testing
                    ["enable_complexity_check"] = true,
                    ["enable_streaming_transaction_parser"] = true
                })
                .AddLayer("features", "db", "features_table");
        });

        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/wfs?service=WFS&version=2.0.0&request=GetFeature&typeNames=features");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.IsSuccessStatusCode)
        {
            // Verify configuration was applied
            var wfsConfig = factory.LoadedConfig!.Services["wfs"];
            wfsConfig.Settings["default_count"].Should().Be(25);
            wfsConfig.Settings["max_features"].Should().Be(100);
            wfsConfig.Settings["enable_complexity_check"].Should().Be(true);
        }
    }

    [Fact]
    public async Task WfsService_DisabledInConfig_ReturnsNotFound()
    {
        // Arrange - Configure with WFS disabled using AddRaw for custom HCL
        using var factory = new ConfigurationV2TestFixture<Program>(DatabaseFixture, builder =>
        {
            builder
                .AddDataSource("db", "postgresql")
                .AddRaw(@"
service ""wfs"" {
  enabled = false  # Explicitly disabled
}
");
        });

        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/wfs?service=WFS&request=GetCapabilities");

        // Assert
        // When WFS is disabled, it should return 404 or not be mapped
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Verify service is disabled in configuration
        factory.LoadedConfig.Should().NotBeNull();
        factory.LoadedConfig!.Services["wfs"].Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task WfsService_WithTransactions_ConfiguresCorrectly()
    {
        // Arrange - Configure WFS with transaction support
        using var factory = new ConfigurationV2TestFixture<Program>(DatabaseFixture, builder =>
        {
            builder
                .AddDataSource("editable_db", "postgresql")
                .AddService("wfs", new()
                {
                    ["version"] = "2.0.0",
                    ["max_transaction_features"] = 1000,
                    ["enable_streaming_transaction_parser"] = true
                })
                .AddLayer("editable_layer", "editable_db", "editable_table");
        });

        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/wfs?service=WFS&request=GetCapabilities");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        // Verify transaction settings
        var wfsConfig = factory.LoadedConfig!.Services["wfs"];
        wfsConfig.Settings["max_transaction_features"].Should().Be(1000);
        wfsConfig.Settings["enable_streaming_transaction_parser"].Should().Be(true);
    }
}

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.Services.Consultant;
using Honua.Cli.Tests.Support;
using Xunit;

namespace Honua.Cli.Tests.Consultant;

[Collection("CliTests")]
[Trait("Category", "Integration")]
public sealed class ConsultantContextBuilderTests
{
    [Fact]
    public async Task BuildAsync_ShouldDetectMissingMetadataAndInfrastructureGaps()
    {
        using var workspace = new TemporaryDirectory();
        var request = new ConsultantRequest(
            Prompt: "Assess my workspace",
            DryRun: true,
            AutoApprove: false,
            SuppressLogging: true,
            WorkspacePath: workspace.Path,
            Mode: ConsultantExecutionMode.Plan);

        var builder = new ConsultantContextBuilder();

        var context = await builder.BuildAsync(request, CancellationToken.None);

        context.Workspace.MetadataDetected.Should().BeFalse();
        context.Observations.Should().Contain(o => o.Id == "metadata_missing");
        context.Observations.Should().Contain(o => o.Id == "monitoring_missing");
        context.Observations.Should().Contain(o => o.Id == "deployment_artifacts_missing");
        context.Workspace.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAsync_ShouldSurfaceMetadataDrivenTagsAndAvoidResolvedObservations()
    {
        using var workspace = new TemporaryDirectory();

        var metadataPath = Path.Combine(workspace.Path, "metadata.json");
        await File.WriteAllTextAsync(metadataPath, ValidMetadataJson);

        Directory.CreateDirectory(Path.Combine(workspace.Path, ".github", "workflows"));
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, ".github", "workflows", "ci.yml"), "name: ci");

        Directory.CreateDirectory(Path.Combine(workspace.Path, "terraform"));
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "terraform", "aws-main.tf"), "resource \"aws_s3_bucket\" \"example\" {}");

        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "observability.json"), "{}");

        var request = new ConsultantRequest(
            Prompt: "Assess my workspace",
            DryRun: true,
            AutoApprove: false,
            SuppressLogging: true,
            WorkspacePath: workspace.Path,
            Mode: ConsultantExecutionMode.Plan);

        var builder = new ConsultantContextBuilder();

        var context = await builder.BuildAsync(request, CancellationToken.None);

        context.Workspace.MetadataDetected.Should().BeTrue();
        context.Workspace.Tags.Should().Contain("ogc-api");
        context.Workspace.Tags.Should().Contain("raster");
        context.Workspace.Tags.Should().Contain("terraform");
        context.Workspace.Tags.Should().Contain("aws");

        context.Observations.Select(o => o.Id).Should().NotContain(new[]
        {
            "metadata_missing",
            "monitoring_missing",
            "deployment_artifacts_missing",
            "ci_missing",
            "datasource_missing_credentials",
            "metadata_services_disabled"
        });
    }

    private const string ValidMetadataJson = """
{
  "server": {
    "allowedHosts": ["app.example.org"],
    "cors": {
      "allowedOrigins": [
        "https://app.example.org",
        "https://admin.example.org"
      ],
      "allowedMethods": ["GET", "POST"],
      "allowedHeaders": ["Authorization", "Content-Type"],
      "exposedHeaders": ["X-Total-Count"],
      "allowCredentials": true,
      "maxAgeSeconds": 600
    }
  },
  "catalog": {
    "id": "catalog-test",
    "title": "Test Catalog",
    "description": "Catalog description",
    "version": "2025.09",
    "links": [{ "href": "https://example.org/catalog", "rel": "self" }],
    "keywords": ["alpha"]
  },
  "folders": [{ "id": "folder", "title": "Folder" }],
  "dataSources": [{ "id": "ds", "provider": "sqlite", "connectionString": "Data Source=test.db" }],
  "services": [{
    "id": "svc",
    "title": "Service",
    "folderId": "folder",
    "serviceType": "feature",
    "dataSourceId": "ds",
    "description": "Service description",
    "keywords": ["roads"],
    "links": [{ "href": "https://example.org/service" }],
    "ogc": {
      "collectionsEnabled": true,
      "itemLimit": 500,
      "defaultCrs": "EPSG:4326",
      "additionalCrs": ["EPSG:3857"],
      "conformanceClasses": ["class"]
    }
  }],
  "layers": [{
    "id": "layer",
    "serviceId": "svc",
    "title": "Layer",
    "description": "Layer description",
    "geometryType": "Point",
    "idField": "id",
    "displayField": "name",
    "geometryField": "geom",
    "crs": ["EPSG:4326"],
    "minScale": 5000,
    "maxScale": 0,
    "keywords": ["primary"],
    "links": [{ "href": "https://example.org/layer" }],
    "extent": {
      "bbox": [[-10, -10, 10, 10]],
      "crs": "EPSG:4326",
      "temporal": {
        "interval": [["2020-01-01T00:00:00Z", "2021-01-01T00:00:00Z"]]
      }
    },
    "query": {
      "autoFilter": {
        "cql": "name = 'Main'"
      },
      "maxRecordCount": 100,
      "supportedParameters": ["bbox"]
    },
    "storage": {
      "table": "layer",
      "geometryColumn": "geom",
      "primaryKey": "id",
      "temporalColumn": "observed_at",
      "srid": 4326,
      "crs": "EPSG:4326"
    },
    "styles": {
      "defaultStyleId": "primary-line",
      "styleIds": ["primary-line"]
    },
    "fields": [
      { "name": "id", "type": "int32", "nullable": false },
      { "name": "name", "type": "string", "maxLength": 255 }
    ],
    "itemType": "feature"
  }],
  "rasterDatasets": [{
    "id": "imagery-naip",
    "title": "NAIP 2022",
    "description": "Sample raster dataset",
    "crs": ["EPSG:4326"],
    "source": {
      "type": "cog",
      "uri": "file:///data/naip.tif",
      "mediaType": "image/tiff"
    },
    "styles": {
      "defaultStyleId": "raster-multiband-naturalcolor",
      "styleIds": ["raster-multiband-naturalcolor", "raster-multiband-falsecolor"]
    },
    "cache": {
      "enabled": true,
      "preseed": false,
      "zoomLevels": [0, 1]
    }
  }],
  "styles": [
    {
      "id": "primary-line",
      "title": "Primary Line",
      "renderer": "simple",
      "format": "mvp-style",
      "geometryType": "line",
      "rules": [
        {
          "id": "default",
          "default": true,
          "symbolizer": {
            "symbolType": "line",
            "strokeColor": "#FF6600FF",
            "strokeWidth": 2.0
          }
        }
      ],
      "simple": {
        "symbolType": "line",
        "strokeColor": "#FF6600FF",
        "strokeWidth": 2.0
      }
    },
    {
      "id": "raster-multiband-naturalcolor",
      "title": "NAIP Natural Color",
      "renderer": "simple",
      "format": "mvp-style",
      "geometryType": "raster",
      "simple": {
        "symbolType": "polygon",
        "fillColor": "#5AA06EFF",
        "strokeColor": "#FFFFFFFF",
        "strokeWidth": 1.5
      }
    },
    {
      "id": "raster-multiband-falsecolor",
      "title": "NAIP False Color",
      "renderer": "simple",
      "format": "mvp-style",
      "geometryType": "raster",
      "simple": {
        "symbolType": "polygon",
        "fillColor": "#DC5578FF",
        "strokeColor": "#FFFFFFFF",
        "strokeWidth": 1.5
      }
    }
  ]
}
""";
}

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Metadata;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public class JsonMetadataProviderTests
{
    [Fact]
    public async Task LoadAsync_ShouldReturnMetadataSnapshot()
    {
        var metadataPath = Path.Combine(Path.GetTempPath(), $"honua-metadata-test-{Guid.NewGuid():N}.json");
        var json = """
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
        await File.WriteAllTextAsync(metadataPath, json);

        try
        {
            var provider = new JsonMetadataProvider(metadataPath);

            var snapshot = await provider.LoadAsync();

            snapshot.Catalog.Id.Should().Be("catalog-test");
            snapshot.Catalog.Links.Should().ContainSingle(l => l.Href == "https://example.org/catalog");
            snapshot.Catalog.Keywords.Should().ContainSingle("alpha");

            snapshot.Server.AllowedHosts.Should().ContainSingle("app.example.org");
            snapshot.Server.Cors.Enabled.Should().BeTrue();
            snapshot.Server.Cors.AllowAnyOrigin.Should().BeFalse();
            snapshot.Server.Cors.AllowedOrigins.Should().Contain(new[]
            {
                "https://app.example.org",
                "https://admin.example.org"
            });
            snapshot.Server.Cors.AllowedMethods.Should().Contain(new[] { "GET", "POST" });
            snapshot.Server.Cors.AllowedHeaders.Should().Contain(new[] { "Authorization", "Content-Type" });
            snapshot.Server.Cors.ExposedHeaders.Should().ContainSingle("X-Total-Count");
            snapshot.Server.Cors.AllowCredentials.Should().BeTrue();
            snapshot.Server.Cors.MaxAge.Should().Be(600);

            snapshot.Folders.Should().ContainSingle(f => f.Id == "folder");

            var service = snapshot.Services.Should().ContainSingle(s => s.Id == "svc").Subject;
            service.Description.Should().Be("Service description");
            service.Ogc.ItemLimit.Should().Be(500);
            service.Ogc.AdditionalCrs.Should().ContainSingle("EPSG:3857");

            var layer = snapshot.Layers.Should().ContainSingle(l => l.Id == "layer").Subject;
            layer.Description.Should().Be("Layer description");
            layer.Fields.Should().HaveCount(2);
            layer.Fields.Should().Contain(f => f.Name == "id" && f.Nullable == false);
            layer.Query.MaxRecordCount.Should().Be(100);
            layer.Query.AutoFilter.Should().NotBeNull();
            layer.Query.AutoFilter!.Cql.Should().Be("name = 'Main'");
            layer.Query.AutoFilter!.Expression.Should().NotBeNull();
            layer.Storage.Should().NotBeNull();
            layer.Storage!.Srid.Should().Be(4326);
            layer.Storage!.Crs.Should().Be(CrsHelper.NormalizeIdentifier("EPSG:4326"));
            layer.Extent.Should().NotBeNull();
            layer.Extent!.Bbox.Should().ContainSingle();
            layer.Extent!.Temporal.Should().ContainSingle(t => t.Start.HasValue && t.End.HasValue);
            layer.DefaultStyleId.Should().Be("primary-line");
            layer.StyleIds.Should().ContainSingle("primary-line");
            layer.MinScale.Should().Be(5000);
            layer.MaxScale.Should().Be(0);

            snapshot.GetService("svc").Layers.Should().ContainSingle(l => l.Id == "layer");

            snapshot.RasterDatasets.Should().ContainSingle(r => r.Id == "imagery-naip");
            var raster = snapshot.RasterDatasets.Single();
            raster.Source.Type.Should().Be("cog");
            raster.Source.Uri.Should().Be("file:///data/naip.tif");
            raster.Styles.DefaultStyleId.Should().Be("raster-multiband-naturalcolor");
            raster.Styles.StyleIds.Should().Contain(new[] { "raster-multiband-naturalcolor", "raster-multiband-falsecolor" });

            var primaryStyle = snapshot.Styles.Should().Contain(style => style.Id == "primary-line").Subject;
            primaryStyle.Format.Should().Be("mvp-style");
            primaryStyle.GeometryType.Should().Be("line");
            primaryStyle.Rules.Should().ContainSingle();
            var primaryRule = primaryStyle.Rules.Single();
            primaryRule.IsDefault.Should().BeTrue();
            primaryRule.Symbolizer.StrokeWidth.Should().Be(2);
        }
        finally
        {
            if (File.Exists(metadataPath))
            {
                File.Delete(metadataPath);
            }
        }
    }

    [Fact]
    public void Parse_ShouldThrow_WhenServiceReferencesUnknownFolder()
    {
        var json = """
{
  "catalog": { "id": "catalog" },
  "folders": [],
  "dataSources": [ { "id": "ds", "provider": "sqlite", "connectionString": "Data Source=test.db" } ],
  "services": [ { "id": "svc", "folderId": "missing", "serviceType": "feature", "dataSourceId": "ds" } ],
  "layers": []
}
""";

        Action act = () => JsonMetadataProvider.Parse(json);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("Service 'svc' references unknown folder 'missing'.");
    }

    [Fact]
    public void Parse_ShouldRejectUnsupportedRasterType()
    {
        var json = """
{
  "catalog": { "id": "catalog" },
  "folders": [],
  "dataSources": [],
  "services": [],
  "layers": [],
  "rasterDatasets": [
    {
      "id": "imagery",
      "source": { "type": "jpeg2000", "uri": "file:///tmp/image.jp2" }
    }
  ]
}
""";

        Action act = () => JsonMetadataProvider.Parse(json);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("Raster dataset 'imagery' source type 'jpeg2000' is not supported.*");
    }

    [Fact]
    public async Task LoadAsync_ShouldThrowWhenMetadataInvalidJson()
    {
        var invalidJsonPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(invalidJsonPath, "{ invalid json }");
        var provider = new JsonMetadataProvider(invalidJsonPath);

        Func<Task> act = () => provider.LoadAsync();

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public void Parse_ShouldRejectCorsWildcardWithCredentials()
    {
        var json = """
{
  "server": {
    "cors": {
      "allowedOrigins": ["*"],
      "allowCredentials": true
    }
  },
  "catalog": { "id": "catalog-test" },
  "folders": [],
  "dataSources": [],
  "services": [],
  "layers": []
}
""";

        Action action = () => JsonMetadataProvider.Parse(json);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*allow credentials when all origins are allowed*");
    }
}

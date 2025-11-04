using System;
using System.Collections.Generic;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Metadata;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class MetadataDiffTests
{
    [Fact]
    public void Compute_ShouldIdentifyServiceAndLayerChanges()
    {
        var current = CreateSnapshot(
            catalogId: "catalog-v1",
            services: new[]
            {
                CreateService(
                    id: "alpha",
                    title: "Alpha",
                    folderId: "transport",
                    dataSourceId: "primary",
                    layers: new[]
                    {
                        CreateLayer("roads", "alpha", geometryType: "Point", crs: new[]{"EPSG:4326"}),
                        CreateLayer("obsolete", "alpha", geometryType: "Point", crs: new[]{"EPSG:4326"})
                    }),
                CreateService("orphan", "Orphan", folderId: "transport", dataSourceId: "primary", layers: Array.Empty<LayerDefinition>())
            });

        var proposed = CreateSnapshot(
            catalogId: "catalog-v2",
            services: new[]
            {
                CreateService(
                    id: "alpha",
                    title: "Alpha Updated",
                    folderId: "transport",
                    dataSourceId: "primary",
                    layers: new[]
                    {
                        CreateLayer("roads", "alpha", geometryType: "Polygon", crs: new[]{"EPSG:4326"}),
                        CreateLayer("alpha-secondary", "alpha", geometryType: "Point", crs: new[]{"EPSG:3857"})
                    }),
                CreateService("beta", "Beta", folderId: "transport", dataSourceId: "primary", layers: Array.Empty<LayerDefinition>())
            });

        var diff = MetadataDiff.Compute(current, proposed);

        diff.Catalog.Should().Be(new CatalogDiff("catalog-v1", "catalog-v2"));
        diff.Services.Should().BeEquivalentTo(new EntityDiffResult(
            Added: new[] { "beta" },
            Removed: new[] { "orphan" },
            Updated: new[] { "alpha" }));

        diff.Layers.Should().ContainKey("alpha");
        var layerDiff = diff.Layers["alpha"];
        layerDiff.Added.Should().BeEquivalentTo(new[] { "alpha-secondary" });
        layerDiff.Removed.Should().BeEquivalentTo(new[] { "obsolete" });
        layerDiff.Updated.Should().BeEquivalentTo(new[] { "roads" });
    }

    [Fact]
    public void CatalogTemporalExtent_ShouldBeParsedFromJson()
    {
        const string json = """
{
  "catalog": {
    "id": "catalog",
    "title": "Catalog",
    "extents": {
      "temporal": {
        "interval": [
          ["2020-01-01T00:00:00Z", "2021-01-01T00:00:00Z"],
          ["2022-01-01T12:00:00Z", null]
        ],
        "temporalReferenceSystem": "http://example.com/trs"
      }
    }
  },
  "folders": [ { "id": "transport", "title": "Transport" } ],
  "dataSources": [ { "id": "primary", "provider": "sqlite", "connectionString": "Data Source=:memory:" } ],
  "services": [ {
    "id": "svc",
    "title": "Service",
    "folderId": "transport",
    "serviceType": "feature",
    "dataSourceId": "primary",
    "enabled": true
  } ],
  "layers": [ {
    "id": "layer",
    "serviceId": "svc",
    "title": "Layer",
    "geometryType": "Point",
    "idField": "id",
    "displayField": "name",
    "geometryField": "geom",
    "fields": [ { "name": "id", "dataType": "int", "nullable": false } ],
    "storage": { "table": "layer" }
  } ]
}
""";

        var snapshot = JsonMetadataProvider.Parse(json);

        snapshot.Catalog.Extents.Should().NotBeNull();
        snapshot.Catalog.Extents!.Temporal.Should().NotBeNull();
        var temporal = snapshot.Catalog.Extents.Temporal!;
        temporal.TemporalReferenceSystem.Should().Be("http://www.opengis.net/def/uom/ISO-8601/0/Gregorian");
        temporal.Interval.Should().HaveCount(2);
        temporal.Interval[0].Start.Should().Be(DateTimeOffset.Parse("2020-01-01T00:00:00Z"));
        temporal.Interval[0].End.Should().Be(DateTimeOffset.Parse("2021-01-01T00:00:00Z"));
        temporal.Interval[1].Start.Should().Be(DateTimeOffset.Parse("2022-01-01T12:00:00Z"));
        temporal.Interval[1].End.Should().BeNull();

        var emptyTemporal = new CatalogTemporalCollectionDefinition();
        emptyTemporal.Interval.Should().NotBeNull();
        emptyTemporal.Interval.Should().BeEmpty();
    }


    private static MetadataSnapshot CreateSnapshot(string catalogId, IReadOnlyList<ServiceDefinition> services)
    {
        var catalog = new CatalogDefinition { Id = catalogId, Title = catalogId };
        var folders = new List<FolderDefinition> { new() { Id = "transport", Title = "Transport" } };
        var dataSources = new List<DataSourceDefinition>
        {
            new() { Id = "primary", Provider = "sqlite", ConnectionString = "Data Source=:memory:" }
        };

        var layers = services.SelectMany(s => s.Layers).ToArray();
        return new MetadataSnapshot(catalog, folders, dataSources, services, layers);
    }

    private static ServiceDefinition CreateService(string id, string title, string folderId, string dataSourceId, IReadOnlyList<LayerDefinition> layers)
    {
        return new ServiceDefinition
        {
            Id = id,
            Title = title,
            FolderId = folderId,
            ServiceType = "feature",
            DataSourceId = dataSourceId,
            Enabled = true,
            Layers = layers
        };
    }

    private static LayerDefinition CreateLayer(string id, string serviceId, string geometryType, IReadOnlyList<string> crs)
    {
        return new LayerDefinition
        {
            Id = id,
            ServiceId = serviceId,
            Title = id,
            GeometryType = geometryType,
            IdField = "road_id",
            DisplayField = "name",
            GeometryField = "geom",
            Crs = crs,
            Fields = new[]
            {
                new FieldDefinition { Name = "road_id", DataType = "int", StorageType = "int", Nullable = false },
                new FieldDefinition { Name = "name", DataType = "string", StorageType = "text", Nullable = true }
            },
            Storage = new LayerStorageDefinition
            {
                Table = "tbl",
                GeometryColumn = "geom",
                PrimaryKey = "road_id",
                TemporalColumn = "observed_at",
                Srid = 4326
            }
        };
    }
}



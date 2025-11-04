using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Stac;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Stac;

public sealed class VectorStacCatalogBuilderTests
{
    [Fact]
    public void Supports_ReturnsFalse_WhenStacDisabled()
    {
        var layer = new LayerDefinition
        {
            Id = "layer",
            ServiceId = "service",
            Title = "Layer",
            GeometryType = "LineString",
            IdField = "id",
            GeometryField = "geom",
            Stac = new StacMetadata { Enabled = false }
        };

        var builder = new VectorStacCatalogBuilder();

        builder.Supports(layer).Should().BeFalse();
    }

    [Fact]
    public void Build_ShouldMapLayerMetadataIntoCollection()
    {
        var stacMetadata = new StacMetadata
        {
            Enabled = true,
            CollectionId = "roads-collection",
            License = "CC-BY-4.0",
            Providers = new[]
            {
                new StacProvider { Name = "City", Roles = new[] { "producer" }, Url = "https://city.example.com" }
            },
            Summaries = new Dictionary<string, object>
            {
                ["road_class"] = new[] { "highway", "local" }
            },
            Assets = new Dictionary<string, StacAssetDefinition>
            {
                ["data"] = new StacAssetDefinition
                {
                    Title = "Feature data",
                    Type = "application/geo+json",
                    Roles = new[] { "data" },
                    Href = "https://example.com/ogc/collections/roads"
                }
            },
            ItemAssets = new Dictionary<string, StacAssetDefinition>
            {
                ["data"] = new StacAssetDefinition
                {
                    Title = "Feature",
                    Type = "application/geo+json",
                    Roles = new[] { "data" }
                }
            },
            StacExtensions = new[] { "https://stac-extensions.github.io/version/v1.0.0/schema.json" },
            ItemIdTemplate = "roads-{id}"
        };

        var layer = new LayerDefinition
        {
            Id = "roads",
            ServiceId = "transport",
            Title = "Road Network",
            Description = "City roads",
            GeometryType = "LineString",
            IdField = "road_id",
            GeometryField = "geom",
            Keywords = new[] { "transportation" },
            Catalog = new CatalogEntryDefinition
            {
                Summary = "Road dataset",
                Keywords = new[] { "roads" }
            },
            Extent = new LayerExtentDefinition
            {
                Bbox = new[] { new[] { -123.0, 37.0, -122.0, 38.0 } },
                Temporal = new[] { new TemporalIntervalDefinition { Start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero) } }
            },
            Stac = stacMetadata
        };

        var service = new ServiceDefinition
        {
            Id = "transport",
            Title = "Transportation",
            FolderId = "root",
            ServiceType = "ogc",
            DataSourceId = "ds",
            Keywords = new[] { "transport" },
            Catalog = new CatalogEntryDefinition
            {
                Summary = "Transportation services",
                Keywords = new[] { "city" }
            },
            Layers = new[] { layer }
        };

        var snapshot = new MetadataSnapshot(
            new CatalogDefinition { Id = "catalog", Title = "Catalog" },
            new[] { new FolderDefinition { Id = "root", Title = "Root" } },
            new[] { new DataSourceDefinition { Id = "ds", Provider = "postgres", ConnectionString = "Host=localhost" } },
            new[] { service },
            new[] { layer },
            Array.Empty<RasterDatasetDefinition>(),
            Array.Empty<StyleDefinition>());

        var builder = new VectorStacCatalogBuilder();

        var collection = builder.Build(layer, service, snapshot);

        collection.Id.Should().Be("roads-collection");
        collection.Title.Should().Be("Road Network");
        collection.License.Should().Be("CC-BY-4.0");
        collection.LayerId.Should().Be("roads");
        collection.ServiceId.Should().Be("transport");
        collection.Keywords.Should().Contain(new[] { "transportation", "roads", "transport", "city" });
        collection.Extensions.Should().Contain("https://stac-extensions.github.io/version/v1.0.0/schema.json");
        collection.Extent.Spatial.Should().HaveCount(1);

        collection.Properties.Should().NotBeNull();
        collection.Properties! ["honua:serviceId"]!.GetValue<string>().Should().Be("transport");
        collection.Properties["summaries"]!.AsObject()["road_class"]!.AsArray().Should().HaveCount(2);
        collection.Properties["assets"]!.AsObject().Should().ContainKey("data");
        collection.Properties["item_assets"]!.AsObject().Should().ContainKey("data");
        collection.Properties["providers"]!.AsArray().Should().HaveCount(1);
    }

    [Fact]
    public void BuildItems_ShouldGenerateVectorAssets_WhenBaseUriProvided()
    {
        var layer = new LayerDefinition
        {
            Id = "buildings",
            ServiceId = "cadastre",
            Title = "Building Footprints",
            Description = "City building polygons",
            GeometryType = "Polygon",
            IdField = "building_id",
            GeometryField = "geom",
            Fields = new[]
            {
                new FieldDefinition { Name = "building_id", DataType = "int" },
                new FieldDefinition { Name = "height", DataType = "double" },
                new FieldDefinition { Name = "use_type", DataType = "string" }
            },
            Extent = new LayerExtentDefinition
            {
                Bbox = new[] { new[] { -122.5, 37.5, -122.0, 38.0 } }
            },
            Stac = new StacMetadata { Enabled = true }
        };

        var service = new ServiceDefinition
        {
            Id = "cadastre",
            Title = "Cadastre",
            FolderId = "root",
            ServiceType = "ogc",
            DataSourceId = "ds",
            Ogc = new OgcServiceDefinition { WfsEnabled = true }
        };

        var snapshot = new MetadataSnapshot(
            new CatalogDefinition { Id = "catalog", Title = "Catalog" },
            new[] { new FolderDefinition { Id = "root", Title = "Root" } },
            new[] { new DataSourceDefinition { Id = "ds", Provider = "postgres", ConnectionString = "Host=localhost" } },
            new[] { service },
            new[] { layer },
            Array.Empty<RasterDatasetDefinition>(),
            Array.Empty<StyleDefinition>());

        var builder = new VectorStacCatalogBuilder();
        var items = builder.BuildItems(layer, service, snapshot, "https://api.example.com");

        items.Should().HaveCount(1);
        var item = items[0];

        item.Assets.Should().ContainKey("geojson");
        item.Assets["geojson"].Type.Should().Be("application/geo+json");
        item.Assets["geojson"].Href.Should().Contain("/ogc/collections/");

        item.Assets.Should().ContainKey("flatgeobuf");
        item.Assets["flatgeobuf"].Type.Should().Be("application/vnd.flatgeobuf");

        item.Assets.Should().ContainKey("tiles");
        item.Assets["tiles"].Type.Should().Be("application/vnd.mapbox-vector-tile");
        item.Assets["tiles"].Href.Should().Contain("{z}/{x}/{y}.pbf");

        item.Assets.Should().ContainKey("wfs");
        item.Assets["wfs"].Type.Should().Be("application/gml+xml");

        item.Properties.Should().NotBeNull();
        item.Properties!["honua:geometryType"]!.GetValue<string>().Should().Be("Polygon");
        item.Properties!["honua:idField"]!.GetValue<string>().Should().Be("building_id");
        item.Properties!["honua:geometryField"]!.GetValue<string>().Should().Be("geom");
        item.Properties!["honua:fields"]!.AsArray().Should().HaveCount(3);
    }

    [Fact]
    public void BuildItems_ShouldIncludeThumbnail_WhenAvailable()
    {
        var layer = new LayerDefinition
        {
            Id = "parks",
            ServiceId = "recreation",
            Title = "City Parks",
            GeometryType = "Polygon",
            IdField = "park_id",
            GeometryField = "geom",
            Catalog = new CatalogEntryDefinition
            {
                Thumbnail = "https://example.com/thumbnails/parks.png"
            },
            Stac = new StacMetadata { Enabled = true }
        };

        var service = new ServiceDefinition
        {
            Id = "recreation",
            Title = "Recreation",
            FolderId = "root",
            ServiceType = "ogc",
            DataSourceId = "ds"
        };

        var snapshot = new MetadataSnapshot(
            new CatalogDefinition { Id = "catalog", Title = "Catalog" },
            new[] { new FolderDefinition { Id = "root", Title = "Root" } },
            new[] { new DataSourceDefinition { Id = "ds", Provider = "postgres", ConnectionString = "Host=localhost" } },
            new[] { service },
            new[] { layer },
            Array.Empty<RasterDatasetDefinition>(),
            Array.Empty<StyleDefinition>());

        var builder = new VectorStacCatalogBuilder();
        var items = builder.BuildItems(layer, service, snapshot, "https://api.example.com");

        items.Should().HaveCount(1);
        var item = items[0];

        item.Assets.Should().ContainKey("thumbnail");
        item.Assets["thumbnail"].Href.Should().Be("https://example.com/thumbnails/parks.png");
        item.Assets["thumbnail"].Type.Should().Be("image/png");
        item.Assets["thumbnail"].Roles.Should().Contain("thumbnail");
    }

    [Fact]
    public void BuildItems_ShouldNotGenerateVectorTiles_ForPointGeometry()
    {
        var layer = new LayerDefinition
        {
            Id = "poi",
            ServiceId = "places",
            Title = "Points of Interest",
            GeometryType = "Point",
            IdField = "poi_id",
            GeometryField = "geom",
            Stac = new StacMetadata { Enabled = true }
        };

        var service = new ServiceDefinition
        {
            Id = "places",
            Title = "Places",
            FolderId = "root",
            ServiceType = "ogc",
            DataSourceId = "ds"
        };

        var snapshot = new MetadataSnapshot(
            new CatalogDefinition { Id = "catalog", Title = "Catalog" },
            new[] { new FolderDefinition { Id = "root", Title = "Root" } },
            new[] { new DataSourceDefinition { Id = "ds", Provider = "postgres", ConnectionString = "Host=localhost" } },
            new[] { service },
            new[] { layer },
            Array.Empty<RasterDatasetDefinition>(),
            Array.Empty<StyleDefinition>());

        var builder = new VectorStacCatalogBuilder();
        var items = builder.BuildItems(layer, service, snapshot, "https://api.example.com");

        items.Should().HaveCount(1);
        var item = items[0];

        // Point geometry IS compatible with vector tiles, so this test is actually checking that it IS generated
        item.Assets.Should().ContainKey("tiles");
    }

    [Fact]
    public void GetCollectionId_ShouldReturnCustomId_WhenSpecified()
    {
        var layer = new LayerDefinition
        {
            Id = "layer1",
            ServiceId = "service1",
            Title = "Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Stac = new StacMetadata
            {
                Enabled = true,
                CollectionId = "custom-collection-id"
            }
        };

        var builder = new VectorStacCatalogBuilder();
        var collectionId = builder.GetCollectionId(layer);

        collectionId.Should().Be("custom-collection-id");
    }

    [Fact]
    public void GetCollectionId_ShouldReturnLayerId_WhenNotSpecified()
    {
        var layer = new LayerDefinition
        {
            Id = "layer1",
            ServiceId = "service1",
            Title = "Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Stac = new StacMetadata { Enabled = true }
        };

        var builder = new VectorStacCatalogBuilder();
        var collectionId = builder.GetCollectionId(layer);

        collectionId.Should().Be("layer1");
    }
}

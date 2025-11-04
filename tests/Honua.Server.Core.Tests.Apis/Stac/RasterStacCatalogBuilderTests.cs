using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Stac;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Stac;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class RasterStacCatalogBuilderTests
{
    [Fact]
    public void BuildGeneratesItemsPerExtentEntry()
    {
        var builder = new RasterStacCatalogBuilder();

        var catalog = new CatalogDefinition
        {
            Id = "main",
            Title = "Main Catalog"
        };

        var folder = new FolderDefinition
        {
            Id = "default",
            Title = "Default"
        };

        var dataSource = new DataSourceDefinition
        {
            Id = "primary",
            Provider = "sqlite",
            ConnectionString = "Data Source=:memory:"
        };

        var temporalIntervals = new List<TemporalIntervalDefinition>
        {
            new() { Start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), End = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero) },
            new() { Start = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero), End = new DateTimeOffset(2024, 2, 2, 0, 0, 0, TimeSpan.Zero) }
        };

        var layerExtent = new LayerExtentDefinition
        {
            Bbox = new List<double[]>
            {
                new[] { -10d, -5d, 0d, 5d },
                new[] { 5d, -5d, 15d, 5d }
            },
            Temporal = temporalIntervals
        };

        var layer = new LayerDefinition
        {
            Id = "imagery",
            ServiceId = "imagery-service",
            Title = "Imagery Layer",
            GeometryType = "polygon",
            IdField = "id",
            GeometryField = "geom",
            Extent = layerExtent,
            Crs = new List<string> { "EPSG:4326" }
        };

        var service = new ServiceDefinition
        {
            Id = "imagery-service",
            Title = "Imagery Service",
            FolderId = folder.Id,
            ServiceType = "raster",
            DataSourceId = dataSource.Id,
            Layers = Array.Empty<LayerDefinition>(),
            Enabled = true
        };

        var dataset = new RasterDatasetDefinition
        {
            Id = "urban-imagery",
            Title = "Urban Imagery",
            ServiceId = service.Id,
            LayerId = layer.Id,
            Source = new RasterSourceDefinition
            {
                Type = "cog",
                Uri = "https://example.test/urban/imagery.tif"
            },
            Extent = layerExtent,
            Catalog = new CatalogEntryDefinition
            {
                Thumbnail = "https://example.test/thumbs/urban.png",
                Themes = new List<string> { "environment" },
                Keywords = new List<string> { "imagery" }
            },
            Crs = new List<string> { "EPSG:4326" },
            Keywords = new List<string> { "urban", "demo" },
            Styles = new RasterStyleDefinition
            {
                DefaultStyleId = "natural-color",
                StyleIds = new List<string> { "natural-color", "infrared" }
            }
        };

        var naturalColorStyle = new StyleDefinition
        {
            Id = "natural-color",
            Renderer = "simple",
            GeometryType = "polygon",
            Simple = new SimpleStyleDefinition { FillColor = "#ffffff" }
        };

        var infraredStyle = naturalColorStyle with
        {
            Id = "infrared"
        };

        var snapshot = new MetadataSnapshot(
            catalog,
            new[] { folder },
            new[] { dataSource },
            new[] { service },
            new[] { layer },
            new[] { dataset },
            new[] { naturalColorStyle, infraredStyle });

        var (collection, items) = builder.Build(dataset, snapshot);

        collection.Id.Should().Be(dataset.Id);
        collection.Extent.Spatial.Should().HaveCount(2);
        collection.Extensions.Should().Contain("https://stac-extensions.github.io/projection/v1.0.0/schema.json");
        collection.Properties.Should().NotBeNull();
        collection.Properties!["honua:serviceTitle"]!.GetValue<string>().Should().Be(service.Title);
        collection.Properties!["honua:layerTitle"]!.GetValue<string>().Should().Be(layer.Title);
        collection.Properties!["honua:dataSourceId"]!.GetValue<string>().Should().Be(service.DataSourceId);
        collection.Properties!["honua:themes"]!.AsArray().Select(node => node!.GetValue<string>()).Should().Contain("environment");
        collection.Properties!["honua:keywords"]!.AsArray().Select(node => node!.GetValue<string>()).Should().Contain(new[] { "urban", "demo", "imagery" });
        collection.Properties!["thumbnail"]!.GetValue<string>().Should().Be("https://example.test/thumbs/urban.png");

        items.Should().HaveCount(2);
        items[0].Extensions.Should().Contain("https://stac-extensions.github.io/projection/v1.0.0/schema.json");

        items[0].Id.Should().Be("urban-imagery-1");
        items[1].Id.Should().Be("urban-imagery-2");

        items[0].Bbox.Should().BeEquivalentTo(new[] { -10d, -5d, 0d, 5d });
        items[1].Bbox.Should().BeEquivalentTo(new[] { 5d, -5d, 15d, 5d });

        items[0].StartDatetime.Should().Be(temporalIntervals[0].Start);
        items[1].StartDatetime.Should().Be(temporalIntervals[1].Start);

        items[0].Properties!["proj:epsg"]!.GetValue<int>().Should().Be(4326);
        items[0].Properties!["honua:keywords"]!.AsArray().Select(node => node!.GetValue<string>()).Should().Contain(new[] { "urban", "demo", "imagery" });
        items[0].Properties!["honua:themes"]!.AsArray().Select(node => node!.GetValue<string>()).Should().Contain("environment");

        items[0].Assets.Should().ContainKey("cog");
        var cogAsset = items[0].Assets["cog"];
        cogAsset.Properties.Should().NotBeNull();
        cogAsset.Properties!["honua:defaultStyleId"]!.GetValue<string>().Should().Be("natural-color");
        cogAsset.Properties!["honua:styleIds"]!.AsArray().Select(node => node!.GetValue<string>()).Should().Contain(new[] { "natural-color", "infrared" });

        items[0].Assets.Should().ContainKey("thumbnail");
        var thumbnailAsset = items[0].Assets["thumbnail"];
        thumbnailAsset.Href.Should().Be("https://example.test/thumbs/urban.png");
        thumbnailAsset.Roles.Should().Contain("thumbnail");
    }
}

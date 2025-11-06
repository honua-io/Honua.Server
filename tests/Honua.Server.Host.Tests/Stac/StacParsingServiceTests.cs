using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using FluentAssertions;
using Honua.Server.Core.Stac;
using Honua.Server.Host.Stac;
using Honua.Server.Host.Stac.Services;
using Xunit;

namespace Honua.Server.Host.Tests.Stac;

[Trait("Category", "Unit")]
[Trait("Feature", "STAC")]
[Trait("Speed", "Fast")]
public sealed class StacParsingServiceTests
{
    private readonly StacParsingService _sut = new();

    [Fact]
    public void ParseCollectionFromJson_PreservesExtentLinksExtensionsAndMetadata()
    {
        // Arrange
        var collectionJson = JsonNode.Parse("""
        {
          "id": "demo-collection",
          "title": "<script>alert('demo')</script>",
          "description": "A <b>test</b> collection",
          "license": "proprietary",
          "version": "2023.1",
          "keywords": ["Imagery", "EO"],
          "extent": {
            "spatial": {
              "bbox": [
                [-180.0, -90.0, 180.0, 90.0]
              ]
            },
            "temporal": {
              "interval": [
                ["2020-01-01T00:00:00Z", "2021-01-01T00:00:00Z"]
              ]
            },
            "description": "Global coverage"
          },
          "links": [
            {
              "rel": "alternate",
              "href": "https://example.com/catalog",
              "type": "text/html",
              "title": "<b>Catalog</b>"
            }
          ],
          "stac_extensions": [
            "https://stac-extensions.github.io/eo/v1.0.0/schema.json"
          ],
          "item_assets": {
            "thumbnail": {
              "href": "https://example.com/thumb.png",
              "roles": ["thumbnail"]
            }
          },
          "providers": [
            {
              "name": "HonuaIO",
              "roles": ["producer"],
              "url": "https://honua.io"
            }
          ],
          "honua:classification": "<img src=x onerror=alert(1)>"
        }
        """)!.AsObject();

        // Act
        var record = _sut.ParseCollectionFromJson(collectionJson);

        // Assert
        record.Id.Should().Be("demo-collection");
        record.Title.Should().Be("&lt;script&gt;alert(&#39;demo&#39;)&lt;/script&gt;");
        record.Description.Should().Be("A &lt;b&gt;test&lt;/b&gt; collection");
        record.Version.Should().Be("2023.1");

        record.Keywords.Should().BeEquivalentTo(new[] { "Imagery", "EO" });

        record.Extent.Spatial.Should().HaveCount(1);
        record.Extent.Spatial.First().Should().Equal(-180.0, -90.0, 180.0, 90.0);
        record.Extent.Temporal.Should().ContainSingle();
        record.Extent.Temporal.First().Start.Should().Be(DateTimeOffset.Parse("2020-01-01T00:00:00Z"));
        record.Extent.Temporal.First().End.Should().Be(DateTimeOffset.Parse("2021-01-01T00:00:00Z"));
        record.Extent.AdditionalProperties.Should().NotBeNull();
        record.Extent.AdditionalProperties!["description"]!.GetValue<string>()
            .Should().Be("Global coverage");

        record.Links.Should().ContainSingle();
        record.Links.First().Rel.Should().Be("alternate");
        record.Links.First().Href.Should().Be("https://example.com/catalog");
        record.Links.First().Title.Should().Be("&lt;b&gt;Catalog&lt;/b&gt;");

        record.Extensions.Should().ContainSingle()
            .Which.Should().Be("https://stac-extensions.github.io/eo/v1.0.0/schema.json");

        record.Properties.Should().NotBeNull();
        record.Properties!.ContainsKey("item_assets").Should().BeTrue();
        record.Properties.ContainsKey("providers").Should().BeTrue();
        record.Properties.ContainsKey("honua:classification").Should().BeTrue();
        record.Properties["honua:classification"]!.GetValue<string>()
            .Should().Be("&lt;img src=x onerror=alert(1)&gt;");
    }

    [Fact]
    public void BuildCollection_ExposesAdditionalPropertiesAndRespectsPathBase()
    {
        // Arrange
        var collectionJson = JsonNode.Parse("""
        {
          "id": "demo-collection",
          "title": "Demo",
          "extent": {
            "spatial": { "bbox": [[-10, -10, 10, 10]] },
            "temporal": { "interval": [["2020-01-01T00:00:00Z", null]] }
          },
          "links": [
            {
              "rel": "related",
              "href": "https://example.com/data.geojson",
              "type": "application/geo+json"
            }
          ],
          "stac_extensions": ["https://stac-extensions.github.io/eo/v1.0.0/schema.json"],
          "item_assets": {
            "thumbnail": {
              "href": "https://example.com/thumb.png"
            }
          },
          "providers": [
            { "name": "HonuaIO", "roles": ["producer"] }
          ],
          "honua:custom": "value"
        }
        """)!.AsObject();

        var record = _sut.ParseCollectionFromJson(collectionJson);
        var baseUri = new Uri("https://api.example.com/platform");

        // Act
        var response = StacApiMapper.BuildCollection(record, baseUri);

        // Assert
        response.Links.Should().Contain(l => l.Rel == "self" &&
            l.Href == "https://api.example.com/platform/stac/collections/demo-collection");

        response.ItemAssets.Should().NotBeNull();
        response.Providers.Should().NotBeNull();

        response.AdditionalFields.Should().ContainKey("honua:custom");
        response.AdditionalFields["honua:custom"]!.GetValue<string>().Should().Be("value");

        response.StacExtensions.Should().Contain("https://stac-extensions.github.io/eo/v1.0.0/schema.json");
    }

    [Fact]
    public void ParseItemFromJson_PopulatesMetadataAndTemporalFields()
    {
        // Arrange
        var itemJson = JsonNode.Parse("""
        {
          "id": "demo-item",
          "title": "<script>Item</script>",
          "description": "Imagery for <b>analysis</b>",
          "links": [
            {
              "rel": "alternate",
              "href": "https://example.com/alt",
              "type": "text/html"
            }
          ],
          "assets": {
            "thumbnail": {
              "href": "https://example.com/thumb.png",
              "title": "<b>Thumb</b>",
              "description": "A <i>thumbnail</i>",
              "type": "image/png",
              "roles": ["thumbnail"]
            }
          },
          "stac_extensions": [
            "https://stac-extensions.github.io/eo/v1.0.0/schema.json"
          ],
          "bbox": [10, 20, 30, 40],
          "geometry": { "type": "Point", "coordinates": [20, 30] },
          "properties": {
            "datetime": "2021-05-02T00:00:00Z",
            "start_datetime": "2021-05-01T00:00:00Z",
            "end_datetime": "2021-05-03T00:00:00Z"
          }
        }
        """)!.AsObject();

        // Act
        var item = _sut.ParseItemFromJson(itemJson, "demo-collection");

        // Assert
        item.Id.Should().Be("demo-item");
        item.CollectionId.Should().Be("demo-collection");
        item.Title.Should().Be("&lt;script&gt;Item&lt;/script&gt;");
        item.Description.Should().Be("Imagery for &lt;b&gt;analysis&lt;/b&gt;");

        item.Links.Should().ContainSingle(l =>
            l.Rel == "alternate" && l.Href == "https://example.com/alt");

        item.Assets.Should().ContainKey("thumbnail");
        var asset = item.Assets["thumbnail"];
        asset.Title.Should().Be("&lt;b&gt;Thumb&lt;/b&gt;");
        asset.Description.Should().Be("A &lt;i&gt;thumbnail&lt;/i&gt;");
        asset.Type.Should().Be("image/png");
        asset.Roles.Should().ContainSingle(r => r == "thumbnail");

        item.Extensions.Should().ContainSingle()
            .Which.Should().Be("https://stac-extensions.github.io/eo/v1.0.0/schema.json");

        item.Bbox.Should().Equal(10, 20, 30, 40);
        item.Geometry.Should().Contain("\"type\":\"Point\"");
        item.Datetime.Should().Be(DateTimeOffset.Parse("2021-05-02T00:00:00Z"));
        item.StartDatetime.Should().Be(DateTimeOffset.Parse("2021-05-01T00:00:00Z"));
        item.EndDatetime.Should().Be(DateTimeOffset.Parse("2021-05-03T00:00:00Z"));
    }

    [Fact]
    public void MergeItemPatch_UpdatesMetadataAndRecomputesTemporalFields()
    {
        // Arrange
        var existing = new StacItemRecord
        {
            Id = "demo-item",
            CollectionId = "demo-collection",
            Title = "Original",
            Description = "Original description",
            Properties = new JsonObject
            {
                ["datetime"] = "2021-05-01T00:00:00Z"
            },
            Assets = new Dictionary<string, StacAsset>(),
            Links = Array.Empty<StacLink>(),
            Extensions = Array.Empty<string>(),
            Datetime = DateTimeOffset.Parse("2021-05-01T00:00:00Z"),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        var patch = JsonNode.Parse("""
        {
          "title": "<script>Updated</script>",
          "description": "New <b>description</b>",
          "links": [
            { "rel": "self", "href": "https://example.com/items/demo-item" }
          ],
          "assets": {
            "data": {
              "href": "https://example.com/data.tif",
              "type": "image/tiff",
              "roles": ["data"]
            }
          },
          "stac_extensions": [
            "https://stac-extensions.github.io/eo/v1.0.0/schema.json"
          ],
          "bbox": [0, 0, 1, 1],
          "properties": {
            "start_datetime": "2021-06-01T00:00:00Z",
            "end_datetime": "2021-06-02T00:00:00Z"
          }
        }
        """)!.AsObject();

        // Act
        var merged = _sut.MergeItemPatch(existing, patch);

        // Assert
        merged.Title.Should().Be("&lt;script&gt;Updated&lt;/script&gt;");
        merged.Description.Should().Be("New &lt;b&gt;description&lt;/b&gt;");
        merged.Links.Should().ContainSingle(l => l.Rel == "self");
        merged.Assets.Should().ContainKey("data");
        merged.Assets["data"].Type.Should().Be("image/tiff");
        merged.Extensions.Should().Contain("https://stac-extensions.github.io/eo/v1.0.0/schema.json");
        merged.Bbox.Should().Equal(0, 0, 1, 1);
        merged.Datetime.Should().BeNull();
        merged.StartDatetime.Should().Be(DateTimeOffset.Parse("2021-06-01T00:00:00Z"));
        merged.EndDatetime.Should().Be(DateTimeOffset.Parse("2021-06-02T00:00:00Z"));
    }
}

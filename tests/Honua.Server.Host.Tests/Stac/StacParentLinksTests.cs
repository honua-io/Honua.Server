using System;
using System.Linq;
using FluentAssertions;
using Honua.Server.Core.Stac;
using Honua.Server.Host.Stac;
using Xunit;

namespace Honua.Server.Host.Tests.Stac;

/// <summary>
/// Tests for STAC 1.0+ parent link compliance.
/// STAC 1.0 requires Collections to have a "parent" link pointing to their Catalog,
/// and Items to have a "parent" link pointing to their Collection.
/// </summary>
public sealed class StacParentLinksTests
{
    private readonly Uri _baseUri = new("https://example.com");

    [Fact]
    public void BuildCollection_ShouldIncludeParentLinkToCatalog()
    {
        // Arrange
        var collection = new StacCollectionRecord
        {
            Id = "test-collection",
            Title = "Test Collection",
            Description = "A test collection",
            License = "CC-BY-4.0",
            Extent = new StacExtent
            {
                Spatial = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } },
                Temporal = new[] { new StacTemporalInterval { Start = DateTimeOffset.UtcNow } }
            }
        };

        // Act
        var response = StacApiMapper.BuildCollection(collection, _baseUri);

        // Assert
        response.Links.Should().NotBeNull();
        response.Links.Should().HaveCountGreaterThanOrEqualTo(4, "should include self, root, parent, and items links");

        var parentLink = response.Links.FirstOrDefault(l => l.Rel == "parent");
        parentLink.Should().NotBeNull("Collections must have a parent link to the Catalog (STAC 1.0 requirement)");
        parentLink!.Href.Should().Be("https://example.com/stac", "parent should point to the catalog root");
        parentLink.Type.Should().Be("application/json");
        parentLink.Title.Should().Be("Parent Catalog");
    }

    [Fact]
    public void BuildCollection_ParentLinkShouldBeDifferentFromRoot()
    {
        // Arrange
        var collection = new StacCollectionRecord
        {
            Id = "test-collection",
            License = "MIT",
            Extent = StacExtent.Empty
        };

        // Act
        var response = StacApiMapper.BuildCollection(collection, _baseUri);

        // Assert
        var parentLink = response.Links.FirstOrDefault(l => l.Rel == "parent");
        var rootLink = response.Links.FirstOrDefault(l => l.Rel == "root");

        parentLink.Should().NotBeNull();
        rootLink.Should().NotBeNull();

        // Both should point to the same URL but are distinct link objects
        parentLink!.Href.Should().Be(rootLink!.Href, "parent and root both point to catalog in a flat hierarchy");
        parentLink.Rel.Should().NotBe(rootLink.Rel, "parent and root have different relationship types");
    }

    [Fact]
    public void BuildItem_ShouldIncludeParentLinkToCollection()
    {
        // Arrange
        var item = new StacItemRecord
        {
            Id = "test-item",
            CollectionId = "my-collection",
            Title = "Test Item",
            Geometry = "{\"type\":\"Point\",\"coordinates\":[0,0]}"
        };

        // Act
        var response = StacApiMapper.BuildItem(item, _baseUri);

        // Assert
        response.Links.Should().NotBeNull();
        response.Links.Should().HaveCountGreaterThanOrEqualTo(4, "should include self, collection, parent, and root links");

        var parentLink = response.Links.FirstOrDefault(l => l.Rel == "parent");
        parentLink.Should().NotBeNull("Items must have a parent link to their Collection (STAC 1.0 requirement)");
        parentLink!.Href.Should().Be("https://example.com/stac/collections/my-collection", "parent should point to the collection");
        parentLink.Type.Should().Be("application/json");
        parentLink.Title.Should().Be("my-collection", "title should be the collection ID");
    }

    [Fact]
    public void BuildItem_ParentLinkShouldMatchCollectionLink()
    {
        // Arrange
        var item = new StacItemRecord
        {
            Id = "test-item",
            CollectionId = "test-col",
            Geometry = null
        };

        // Act
        var response = StacApiMapper.BuildItem(item, _baseUri);

        // Assert
        var parentLink = response.Links.FirstOrDefault(l => l.Rel == "parent");
        var collectionLink = response.Links.FirstOrDefault(l => l.Rel == "collection");

        parentLink.Should().NotBeNull();
        collectionLink.Should().NotBeNull();

        // Both should point to the same collection URL
        parentLink!.Href.Should().Be(collectionLink!.Href, "parent and collection both point to the same collection");
        parentLink.Title.Should().Be(collectionLink.Title, "parent and collection have the same title");
        parentLink.Rel.Should().NotBe(collectionLink.Rel, "parent and collection have different relationship types");
    }

    [Fact]
    public void BuildItem_ParentLinkShouldHandleSpecialCharactersInCollectionId()
    {
        // Arrange
        var item = new StacItemRecord
        {
            Id = "item-1",
            CollectionId = "collection with spaces & special!chars",
            Geometry = null
        };

        // Act
        var response = StacApiMapper.BuildItem(item, _baseUri);

        // Assert
        var parentLink = response.Links.FirstOrDefault(l => l.Rel == "parent");
        parentLink.Should().NotBeNull();

        // URL should be properly escaped
        parentLink!.Href.Should().Contain("collection%20with%20spaces", "spaces should be URL encoded");
        parentLink.Href.Should().Contain("%26", "ampersand should be URL encoded");
        parentLink.Href.Should().Contain("%21", "exclamation should be URL encoded");
    }

    [Fact]
    public void BuildSearchCollection_ItemsShouldHaveParentLinks()
    {
        // Arrange
        var items = new[]
        {
            new StacItemRecord
            {
                Id = "item-1",
                CollectionId = "col-1",
                Geometry = null
            },
            new StacItemRecord
            {
                Id = "item-2",
                CollectionId = "col-2",
                Geometry = null
            }
        };

        // Act
        var response = StacApiMapper.BuildSearchCollection(items, _baseUri, matched: 2, nextToken: null);

        // Assert
        response.Features.Should().HaveCount(2);

        foreach (var feature in response.Features)
        {
            var parentLink = feature.Links.FirstOrDefault(l => l.Rel == "parent");
            parentLink.Should().NotBeNull($"Item {feature.Id} must have a parent link");
            parentLink!.Href.Should().StartWith("https://example.com/stac/collections/");
        }
    }

    [Fact]
    public void BuildItemCollection_ItemsShouldHaveParentLinks()
    {
        // Arrange
        var collection = new StacCollectionRecord
        {
            Id = "test-collection",
            License = "proprietary",
            Extent = StacExtent.Empty
        };

        var items = new[]
        {
            new StacItemRecord
            {
                Id = "item-1",
                CollectionId = "test-collection",
                Geometry = null
            }
        };

        // Act
        var response = StacApiMapper.BuildItemCollection(items, collection, _baseUri, matched: 1, nextToken: null);

        // Assert
        response.Features.Should().HaveCount(1);

        var feature = response.Features[0];
        var parentLink = feature.Links.FirstOrDefault(l => l.Rel == "parent");
        parentLink.Should().NotBeNull();
        parentLink!.Href.Should().Be("https://example.com/stac/collections/test-collection");
    }

    [Fact]
    public void BuildCollection_ShouldMaintainLinkOrder()
    {
        // Arrange
        var collection = new StacCollectionRecord
        {
            Id = "col",
            License = "MIT",
            Extent = StacExtent.Empty
        };

        // Act
        var response = StacApiMapper.BuildCollection(collection, _baseUri);

        // Assert
        // Check that standard links appear in expected order: self, root, parent, items
        var links = response.Links.Take(4).ToList();
        links[0].Rel.Should().Be("self");
        links[1].Rel.Should().Be("root");
        links[2].Rel.Should().Be("parent");
        links[3].Rel.Should().Be("items");
    }

    [Fact]
    public void BuildItem_ShouldMaintainLinkOrder()
    {
        // Arrange
        var item = new StacItemRecord
        {
            Id = "item",
            CollectionId = "col",
            Geometry = null
        };

        // Act
        var response = StacApiMapper.BuildItem(item, _baseUri);

        // Assert
        // Check that standard links appear in expected order: self, collection, parent, root
        var links = response.Links.Take(4).ToList();
        links[0].Rel.Should().Be("self");
        links[1].Rel.Should().Be("collection");
        links[2].Rel.Should().Be("parent");
        links[3].Rel.Should().Be("root");
    }
}

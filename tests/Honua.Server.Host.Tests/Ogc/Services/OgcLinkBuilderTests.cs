using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Ogc.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc.Services;

public class OgcLinkBuilderTests
{
    private readonly OgcLinkBuilder _builder;

    public OgcLinkBuilderTests()
    {
        _builder = new OgcLinkBuilder();
    }

    [Fact]
    public void BuildLink_WithBasicParameters_CreatesLink()
    {
        // Arrange
        var request = CreateHttpRequest("https://example.com", "/ogc");

        // Act
        var link = _builder.BuildLink(request, "/ogc/collections", "self", "application/json", "Collections");

        // Assert
        Assert.NotNull(link);
        Assert.Equal("self", link.Rel);
        Assert.Equal("application/json", link.Type);
        Assert.Equal("Collections", link.Title);
        Assert.StartsWith("https://example.com/ogc/collections", link.Href);
    }

    [Fact]
    public void BuildLink_WithQuery_IncludesQueryParameters()
    {
        // Arrange
        var request = CreateHttpRequest("https://example.com", "/ogc");
        var query = new FeatureQuery(Limit: 10, Offset: 20);

        // Act
        var link = _builder.BuildLink(request, "/ogc/collections/test/items", "self", "application/json", "Items", query);

        // Assert
        Assert.Contains("limit=10", link.Href);
        Assert.Contains("offset=20", link.Href);
    }

    [Fact]
    public void BuildLink_WithOverrides_AppliesOverrides()
    {
        // Arrange
        var request = CreateHttpRequest("https://example.com", "/ogc");
        var query = new FeatureQuery(Limit: 10);
        var overrides = new Dictionary<string, string?> { ["limit"] = "50", ["custom"] = "value" };

        // Act
        var link = _builder.BuildLink(request, "/ogc/test", "self", "application/json", "Test", query, overrides);

        // Assert
        Assert.Contains("limit=50", link.Href);
        Assert.Contains("custom=value", link.Href);
    }

    [Fact]
    public void BuildLink_WithNullOverride_RemovesParameter()
    {
        // Arrange
        var request = CreateHttpRequest("https://example.com", "/ogc");
        var query = new FeatureQuery(Limit: 10);
        var overrides = new Dictionary<string, string?> { ["limit"] = null };

        // Act
        var link = _builder.BuildLink(request, "/ogc/test", "self", "application/json", "Test", query, overrides);

        // Assert
        Assert.DoesNotContain("limit=", link.Href);
    }

    [Fact]
    public void BuildHref_WithBbox_IncludesBboxParameter()
    {
        // Arrange
        var request = CreateHttpRequest("https://example.com", "/ogc");
        var bbox = new BoundingBox(-180, -90, 180, 90, "EPSG:4326");
        var query = new FeatureQuery(Bbox: bbox);

        // Act
        var href = _builder.BuildHref(request, "/ogc/test", query, null);

        // Assert
        Assert.Contains("bbox=", href);
        Assert.Contains("bbox-crs=", href);
    }

    [Fact]
    public void BuildHref_WithTemporal_IncludesDatetimeParameter()
    {
        // Arrange
        var request = CreateHttpRequest("https://example.com", "/ogc");
        var temporal = new TemporalInterval(
            System.DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
            System.DateTimeOffset.Parse("2024-12-31T23:59:59Z"));
        var query = new FeatureQuery(Temporal: temporal);

        // Act
        var href = _builder.BuildHref(request, "/ogc/test", query, null);

        // Assert
        Assert.Contains("datetime=", href);
        Assert.Contains("2024-01-01", href);
        Assert.Contains("2024-12-31", href);
    }

    [Fact]
    public void BuildHref_WithResultTypeHits_IncludesResultTypeParameter()
    {
        // Arrange
        var request = CreateHttpRequest("https://example.com", "/ogc");
        var query = new FeatureQuery(ResultType: FeatureResultType.Hits);

        // Act
        var href = _builder.BuildHref(request, "/ogc/test", query, null);

        // Assert
        Assert.Contains("resultType=hits", href);
    }

    [Fact]
    public void BuildCollectionLinks_CreatesExpectedLinks()
    {
        // Arrange
        var request = CreateHttpRequest("https://example.com", "/ogc");
        var service = CreateServiceDefinition();
        var layer = CreateLayerDefinition();

        // Act
        var links = _builder.BuildCollectionLinks(request, service, layer, "test-collection");

        // Assert
        Assert.NotEmpty(links);
        Assert.Contains(links, l => l.Rel == "self");
        Assert.Contains(links, l => l.Rel == "items");
        Assert.Contains(links, l => l.Rel == "queryables");
        Assert.Contains(links, l => l.Rel == "alternate" && l.Type.Contains("kml"));
        Assert.Contains(links, l => l.Rel == "alternate" && l.Type.Contains("kmz"));
        Assert.Contains(links, l => l.Rel == "alternate" && l.Type.Contains("geopackage"));
    }

    [Fact]
    public void BuildCollectionLinks_WithDefaultStyle_IncludesStylesheetLink()
    {
        // Arrange
        var request = CreateHttpRequest("https://example.com", "/ogc");
        var service = CreateServiceDefinition();
        var layer = CreateLayerDefinition(defaultStyleId: "default-style");

        // Act
        var links = _builder.BuildCollectionLinks(request, service, layer, "test-collection");

        // Assert
        Assert.Contains(links, l => l.Rel == "stylesheet" && l.Href.Contains("default-style"));
    }

    [Fact]
    public void BuildItemsLinks_IncludesAlternateFormats()
    {
        // Arrange
        var request = CreateHttpRequest("https://example.com", "/ogc");
        var query = new FeatureQuery(Limit: 10);

        // Act
        var links = _builder.BuildItemsLinks(request, "test-collection", query, 100, OgcResponseFormat.GeoJson, "application/geo+json");

        // Assert
        Assert.Contains(links, l => l.Rel == "alternate" && l.Type.Contains("kml"));
        Assert.Contains(links, l => l.Rel == "alternate" && l.Type.Contains("topojson"));
        Assert.Contains(links, l => l.Rel == "alternate" && l.Type.Contains("flatgeobuf"));
        Assert.Contains(links, l => l.Rel == "alternate" && l.Type.Contains("arrow"));
        Assert.Contains(links, l => l.Rel == "alternate" && l.Type.Contains("csv"));
    }

    [Fact]
    public void BuildItemsLinks_WithPagination_IncludesNextLink()
    {
        // Arrange
        var request = CreateHttpRequest("https://example.com", "/ogc");
        var query = new FeatureQuery(Limit: 10, Offset: 0);

        // Act
        var links = _builder.BuildItemsLinks(request, "test-collection", query, 100, OgcResponseFormat.GeoJson, "application/geo+json");

        // Assert
        Assert.Contains(links, l => l.Rel == "next");
        var nextLink = links.First(l => l.Rel == "next");
        Assert.Contains("offset=10", nextLink.Href);
    }

    [Fact]
    public void BuildItemsLinks_WithPagination_IncludesPrevLink()
    {
        // Arrange
        var request = CreateHttpRequest("https://example.com", "/ogc");
        var query = new FeatureQuery(Limit: 10, Offset: 20);

        // Act
        var links = _builder.BuildItemsLinks(request, "test-collection", query, 100, OgcResponseFormat.GeoJson, "application/geo+json");

        // Assert
        Assert.Contains(links, l => l.Rel == "prev");
        var prevLink = links.First(l => l.Rel == "prev");
        Assert.Contains("offset=10", prevLink.Href);
    }

    [Fact]
    public void BuildItemsLinks_WithHitsResultType_ExcludesPaginationLinks()
    {
        // Arrange
        var request = CreateHttpRequest("https://example.com", "/ogc");
        var query = new FeatureQuery(Limit: 10, Offset: 0, ResultType: FeatureResultType.Hits);

        // Act
        var links = _builder.BuildItemsLinks(request, "test-collection", query, 100, OgcResponseFormat.GeoJson, "application/geo+json");

        // Assert
        Assert.DoesNotContain(links, l => l.Rel == "next");
        Assert.DoesNotContain(links, l => l.Rel == "prev");
    }

    [Fact]
    public void BuildSearchLinks_IncludesCollectionsInHref()
    {
        // Arrange
        var request = CreateHttpRequest("https://example.com", "/ogc");
        var collections = new List<string> { "collection1", "collection2" };
        var query = new FeatureQuery(Limit: 10);

        // Act
        var links = _builder.BuildSearchLinks(request, collections, query, 50, "application/geo+json");

        // Assert
        var selfLink = links.First(l => l.Rel == "self");
        Assert.Contains("collections=collection1,collection2", selfLink.Href);
    }

    [Fact]
    public void BuildSearchLinks_WithPagination_IncludesNextLink()
    {
        // Arrange
        var request = CreateHttpRequest("https://example.com", "/ogc");
        var collections = new List<string> { "collection1" };
        var query = new FeatureQuery(Limit: 10, Offset: 0);

        // Act
        var links = _builder.BuildSearchLinks(request, collections, query, 50, "application/geo+json");

        // Assert
        Assert.Contains(links, l => l.Rel == "next");
        var nextLink = links.First(l => l.Rel == "next");
        Assert.Contains("offset=10", nextLink.Href);
        Assert.Contains("collections=collection1", nextLink.Href);
    }

    [Fact]
    public void BuildTileMatrixSetLinks_ReturnsStandardMatrixSets()
    {
        // Arrange
        var request = CreateHttpRequest("https://example.com", "/ogc");

        // Act
        var links = _builder.BuildTileMatrixSetLinks(request, "test-collection", "default");

        // Assert
        Assert.Equal(2, links.Count);
        // Both WorldCrs84Quad and WorldWebMercatorQuad should be present
    }

    [Fact]
    public void ToLink_ConvertsLinkDefinition()
    {
        // Arrange
        var linkDef = new LinkDefinition
        {
            Href = "https://example.com/link",
            Rel = "alternate",
            Type = "text/html",
            Title = "Test Link"
        };

        // Act
        var link = _builder.ToLink(linkDef);

        // Assert
        Assert.Equal(linkDef.Href, link.Href);
        Assert.Equal(linkDef.Rel, link.Rel);
        Assert.Equal(linkDef.Type, link.Type);
        Assert.Equal(linkDef.Title, link.Title);
    }

    [Fact]
    public void ToLink_WithMissingRel_UsesRelatedAsDefault()
    {
        // Arrange
        var linkDef = new LinkDefinition
        {
            Href = "https://example.com/link",
            Rel = null
        };

        // Act
        var link = _builder.ToLink(linkDef);

        // Assert
        Assert.Equal("related", link.Rel);
    }

    // Helper methods
    private HttpRequest CreateHttpRequest(string host, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString(host.Replace("https://", "").Replace("http://", ""));
        context.Request.PathBase = "";
        context.Request.Path = path;
        return context.Request;
    }

    private ServiceDefinition CreateServiceDefinition()
    {
        return new ServiceDefinition
        {
            Id = "test-service",
            Title = "Test Service",
            ServiceType = "OGC API",
            Ogc = new ServiceOgcDefinition()
        };
    }

    private LayerDefinition CreateLayerDefinition(string? defaultStyleId = null)
    {
        return new LayerDefinition
        {
            Id = "test-layer",
            Title = "Test Layer",
            DefaultStyleId = defaultStyleId,
            StyleIds = new List<string>(),
            Links = new List<LinkDefinition>()
        };
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Ogc.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc.Services;

public class OgcFeaturesRenderingHandlerTests
{
    private readonly OgcFeaturesRenderingHandler _handler;

    public OgcFeaturesRenderingHandlerTests()
    {
        _handler = new OgcFeaturesRenderingHandler();
    }

    [Fact]
    public void WantsHtml_WithHtmlFormatParameter_ReturnsTrue()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?f=html");

        // Act
        var result = _handler.WantsHtml(httpContext.Request);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void WantsHtml_WithJsonFormatParameter_ReturnsFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?f=json");

        // Act
        var result = _handler.WantsHtml(httpContext.Request);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void WantsHtml_WithHtmlAcceptHeader_ReturnsTrue()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Accept = "text/html";

        // Act
        var result = _handler.WantsHtml(httpContext.Request);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void WantsHtml_WithJsonAcceptHeader_ReturnsFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Accept = "application/json";

        // Act
        var result = _handler.WantsHtml(httpContext.Request);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void WantsHtml_WithWildcardAccept_ReturnsFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Accept = "*/*";

        // Act
        var result = _handler.WantsHtml(httpContext.Request);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RenderLandingHtml_WithValidSnapshot_ReturnsHtml()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");
        var snapshot = new MetadataSnapshot
        {
            Catalog = new CatalogDefinition
            {
                Id = "test-catalog",
                Title = "Test Catalog",
                Description = "Test Description"
            },
            Services = new List<ServiceDefinition>()
        };
        var links = new List<OgcLink>();

        // Act
        var result = _handler.RenderLandingHtml(httpContext.Request, snapshot, links);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("<!DOCTYPE html>", result);
        Assert.Contains("Test Catalog", result);
        Assert.Contains("Test Description", result);
    }

    [Fact]
    public void RenderLandingHtml_WithServices_IncludesServiceList()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");
        var snapshot = new MetadataSnapshot
        {
            Catalog = new CatalogDefinition { Id = "catalog", Title = "Catalog" },
            Services = new List<ServiceDefinition>
            {
                new() { Id = "service1", Title = "Service 1", ServiceType = "FeatureServer" }
            }
        };
        var links = new List<OgcLink>();

        // Act
        var result = _handler.RenderLandingHtml(httpContext.Request, snapshot, links);

        // Assert
        Assert.Contains("Services", result);
        Assert.Contains("Service 1", result);
        Assert.Contains("FeatureServer", result);
    }

    [Fact]
    public void RenderCollectionsHtml_WithNoCollections_ShowsNoCollectionsMessage()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");
        var snapshot = new MetadataSnapshot
        {
            Catalog = new CatalogDefinition(),
            Services = new List<ServiceDefinition>()
        };
        var collections = new List<OgcSharedHandlers.CollectionSummary>();

        // Act
        var result = _handler.RenderCollectionsHtml(httpContext.Request, snapshot, collections);

        // Assert
        Assert.Contains("No collections are published", result);
    }

    [Fact]
    public void RenderCollectionsHtml_WithCollections_ShowsTable()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");
        var snapshot = new MetadataSnapshot
        {
            Catalog = new CatalogDefinition(),
            Services = new List<ServiceDefinition>()
        };
        var collections = new List<OgcSharedHandlers.CollectionSummary>
        {
            new("service::layer", "Test Collection", "Description", "Feature", new[] { "EPSG:4326" }, "EPSG:4326")
        };

        // Act
        var result = _handler.RenderCollectionsHtml(httpContext.Request, snapshot, collections);

        // Assert
        Assert.Contains("<table>", result);
        Assert.Contains("Test Collection", result);
        Assert.Contains("Feature", result);
        Assert.Contains("EPSG:4326", result);
    }

    [Fact]
    public void RenderCollectionHtml_WithValidCollection_ReturnsHtml()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");
        var service = new ServiceDefinition { Id = "service", Title = "Service" };
        var layer = new LayerDefinition
        {
            Id = "layer",
            Title = "Layer",
            Description = "Layer Description",
            ItemType = "Feature"
        };
        var crs = new[] { "EPSG:4326", "EPSG:3857" };
        var links = new List<OgcLink>();

        // Act
        var result = _handler.RenderCollectionHtml(
            httpContext.Request, service, layer, "service::layer", crs, links);

        // Assert
        Assert.Contains("Layer", result);
        Assert.Contains("Layer Description", result);
        Assert.Contains("Feature", result);
        Assert.Contains("EPSG:4326", result);
    }

    [Fact]
    public void RenderFeatureCollectionHtml_WithHitsOnly_ShowsHitsMessage()
    {
        // Arrange
        var features = new List<OgcSharedHandlers.HtmlFeatureEntry>();
        var links = new List<OgcLink>();

        // Act
        var result = _handler.RenderFeatureCollectionHtml(
            "Title", null, features, 100, 0, null, links, hitsOnly: true);

        // Assert
        Assert.Contains("Result type is <code>hits</code>", result);
    }

    [Fact]
    public void RenderFeatureCollectionHtml_WithNoFeatures_ShowsNoFeaturesMessage()
    {
        // Arrange
        var features = new List<OgcSharedHandlers.HtmlFeatureEntry>();
        var links = new List<OgcLink>();

        // Act
        var result = _handler.RenderFeatureCollectionHtml(
            "Title", null, features, 0, 0, null, links, hitsOnly: false);

        // Assert
        Assert.Contains("No features found", result);
    }

    [Fact]
    public void RenderFeatureCollectionHtml_WithFeatures_ShowsFeatureDetails()
    {
        // Arrange
        var features = new List<OgcSharedHandlers.HtmlFeatureEntry>
        {
            new("collection", "Collection", new FeatureComponents(
                "feature-1",
                "Feature 1",
                new Dictionary<string, object?> { ["name"] = "Test" },
                null, null, null, null, null, null, null, null))
        };
        var links = new List<OgcLink>();

        // Act
        var result = _handler.RenderFeatureCollectionHtml(
            "Title", null, features, 1, 1, null, links, hitsOnly: false);

        // Assert
        Assert.Contains("Feature 1", result);
        Assert.Contains("name", result);
        Assert.Contains("Test", result);
    }

    [Fact]
    public void RenderFeatureHtml_WithValidFeature_ReturnsHtml()
    {
        // Arrange
        var entry = new OgcSharedHandlers.HtmlFeatureEntry(
            "collection",
            "Collection",
            new FeatureComponents(
                "feature-1",
                "Feature 1",
                new Dictionary<string, object?> { ["name"] = "Test", ["value"] = 42 },
                null, null, null, null, null, null, null, null));
        var links = new List<OgcLink>();

        // Act
        var result = _handler.RenderFeatureHtml("Title", "Description", entry, null, links);

        // Assert
        Assert.Contains("Title", result);
        Assert.Contains("Description", result);
        Assert.Contains("Properties", result);
        Assert.Contains("name", result);
        Assert.Contains("Test", result);
    }

    [Fact]
    public void FormatPropertyValue_WithNull_ReturnsEmptyString()
    {
        // Act
        var result = _handler.FormatPropertyValue(null);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void FormatPropertyValue_WithString_ReturnsString()
    {
        // Act
        var result = _handler.FormatPropertyValue("test value");

        // Assert
        Assert.Equal("test value", result);
    }

    [Fact]
    public void FormatPropertyValue_WithBoolean_ReturnsStringRepresentation()
    {
        // Act
        var resultTrue = _handler.FormatPropertyValue(true);
        var resultFalse = _handler.FormatPropertyValue(false);

        // Assert
        Assert.Equal("true", resultTrue);
        Assert.Equal("false", resultFalse);
    }

    [Fact]
    public void FormatPropertyValue_WithNumber_ReturnsStringRepresentation()
    {
        // Act
        var result = _handler.FormatPropertyValue(42);

        // Assert
        Assert.Contains("42", result);
    }

    [Fact]
    public void FormatPropertyValue_WithByteArray_ReturnsFormattedString()
    {
        // Arrange
        var bytes = new byte[] { 1, 2, 3 };

        // Act
        var result = _handler.FormatPropertyValue(bytes);

        // Assert
        Assert.Contains("[binary:", result);
        Assert.Contains("3 bytes]", result);
    }

    [Fact]
    public void FormatPropertyValue_WithJsonNode_ReturnsJsonString()
    {
        // Arrange
        var node = JsonNode.Parse("{\"key\":\"value\"}");

        // Act
        var result = _handler.FormatPropertyValue(node);

        // Assert
        Assert.Contains("key", result);
        Assert.Contains("value", result);
    }

    [Fact]
    public void FormatGeometryValue_WithNull_ReturnsNull()
    {
        // Act
        var result = _handler.FormatGeometryValue(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FormatGeometryValue_WithJsonNode_ReturnsJsonString()
    {
        // Arrange
        var geometry = JsonNode.Parse("{\"type\":\"Point\",\"coordinates\":[0,0]}");

        // Act
        var result = _handler.FormatGeometryValue(geometry);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Point", result);
        Assert.Contains("coordinates", result);
    }

    [Fact]
    public void FormatGeometryValue_WithString_ReturnsString()
    {
        // Arrange
        var geometry = "POINT(0 0)";

        // Act
        var result = _handler.FormatGeometryValue(geometry);

        // Assert
        Assert.Equal("POINT(0 0)", result);
    }
}

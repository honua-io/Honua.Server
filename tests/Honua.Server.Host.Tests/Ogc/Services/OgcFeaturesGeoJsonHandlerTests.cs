// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Host.Ogc.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc.Services;

public class OgcFeaturesGeoJsonHandlerTests
{
    private readonly OgcFeaturesGeoJsonHandler _handler;

    public OgcFeaturesGeoJsonHandlerTests()
    {
        _handler = new OgcFeaturesGeoJsonHandler();
    }

    [Fact]
    public void BuildFeatureLinks_WithValidFeatureId_ReturnsLinks()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com");

        var layer = new LayerDefinition
        {
            Id = "test-layer",
            Title = "Test Layer",
            GeometryField = "geom",
            IdField = "id"
        };

        var components = new FeatureComponents(
            FeatureId: "123",
            RawId: "123",
            Properties: new Dictionary<string, object?>(),
            Geometry: null);

        // Act
        var links = _handler.BuildFeatureLinks(
            context.Request,
            "service::layer",
            layer,
            components,
            null);

        // Assert
        Assert.NotNull(links);
        Assert.NotEmpty(links);
        Assert.Contains(links, l => l.Rel == "self");
        Assert.Contains(links, l => l.Rel == "collection");
    }

    [Fact]
    public void EnumerateGeoJsonFeatures_WithFeatureCollection_ReturnsFeatures()
    {
        // Arrange
        var json = @"{
            ""type"": ""FeatureCollection"",
            ""features"": [
                {""type"": ""Feature"", ""properties"": {""name"": ""test1""}},
                {""type"": ""Feature"", ""properties"": {""name"": ""test2""}}
            ]
        }";

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Act
        var features = _handler.EnumerateGeoJsonFeatures(root);

        // Assert
        var featureList = new List<JsonElement>(features);
        Assert.Equal(2, featureList.Count);
    }

    [Fact]
    public void EnumerateGeoJsonFeatures_WithSingleFeature_ReturnsSingleFeature()
    {
        // Arrange
        var json = @"{""type"": ""Feature"", ""properties"": {""name"": ""test""}}";

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Act
        var features = _handler.EnumerateGeoJsonFeatures(root);

        // Assert
        var featureList = new List<JsonElement>(features);
        Assert.Single(featureList);
    }

    [Fact]
    public void EnumerateGeoJsonFeatures_WithArray_ReturnsAllElements()
    {
        // Arrange
        var json = @"[
            {""type"": ""Feature"", ""properties"": {""name"": ""test1""}},
            {""type"": ""Feature"", ""properties"": {""name"": ""test2""}}
        ]";

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Act
        var features = _handler.EnumerateGeoJsonFeatures(root);

        // Assert
        var featureList = new List<JsonElement>(features);
        Assert.Equal(2, featureList.Count);
    }

    [Fact]
    public void ReadGeoJsonAttributes_WithValidFeature_ExtractsProperties()
    {
        // Arrange
        var json = @"{
            ""type"": ""Feature"",
            ""id"": ""123"",
            ""properties"": {
                ""name"": ""Test Feature"",
                ""value"": 42
            },
            ""geometry"": {
                ""type"": ""Point"",
                ""coordinates"": [1.0, 2.0]
            }
        }";

        using var document = JsonDocument.Parse(json);
        var featureElement = document.RootElement;

        var layer = new LayerDefinition
        {
            Id = "test-layer",
            IdField = "id",
            GeometryField = "geom"
        };

        // Act
        var attributes = _handler.ReadGeoJsonAttributes(
            featureElement,
            layer,
            removeId: false,
            out var featureId);

        // Assert
        Assert.NotNull(attributes);
        Assert.Equal("123", featureId);
        Assert.Contains("name", attributes.Keys);
        Assert.Equal("Test Feature", attributes["name"]);
        Assert.Contains("value", attributes.Keys);
        Assert.Contains("geom", attributes.Keys); // Geometry should be mapped to layer's geometry field
    }

    [Fact]
    public void ReadGeoJsonAttributes_WithRemoveId_RemovesIdFromAttributes()
    {
        // Arrange
        var json = @"{
            ""type"": ""Feature"",
            ""properties"": {
                ""id"": 123,
                ""name"": ""Test""
            }
        }";

        using var document = JsonDocument.Parse(json);
        var featureElement = document.RootElement;

        var layer = new LayerDefinition
        {
            Id = "test-layer",
            IdField = "id",
            GeometryField = "geom"
        };

        // Act
        var attributes = _handler.ReadGeoJsonAttributes(
            featureElement,
            layer,
            removeId: true,
            out var featureId);

        // Assert
        Assert.NotNull(attributes);
        Assert.Equal("123", featureId);
        Assert.DoesNotContain("id", attributes.Keys);
    }

    [Fact]
    public async Task ParseJsonDocumentAsync_WithValidJson_ReturnsDocument()
    {
        // Arrange
        var json = @"{""type"": ""Feature"", ""properties"": {}}";
        var context = new DefaultHttpContext();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        context.Request.Body = stream;
        context.Request.ContentLength = stream.Length;

        // Act
        using var document = await _handler.ParseJsonDocumentAsync(context.Request, CancellationToken.None);

        // Assert
        Assert.NotNull(document);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }

    [Fact]
    public async Task ParseJsonDocumentAsync_WithInvalidJson_ReturnsNull()
    {
        // Arrange
        var invalidJson = @"{invalid json";
        var context = new DefaultHttpContext();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(invalidJson));
        context.Request.Body = stream;
        context.Request.ContentLength = stream.Length;

        // Act
        using var document = await _handler.ParseJsonDocumentAsync(context.Request, CancellationToken.None);

        // Assert
        Assert.Null(document);
    }

    [Fact]
    public async Task ParseJsonDocumentAsync_WithOversizedPayload_ThrowsException()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.ContentLength = 200L * 1024 * 1024; // 200 MB (over default limit)
        var stream = new MemoryStream();
        context.Request.Body = stream;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.ParseJsonDocumentAsync(context.Request, CancellationToken.None));
    }

    [Fact]
    public void ToFeature_WithValidData_ReturnsGeoJsonFeature()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com");

        var layer = new LayerDefinition
        {
            Id = "test-layer",
            Title = "Test Layer",
            GeometryField = "geom",
            IdField = "id"
        };

        var record = new FeatureRecord(
            FeatureId: "123",
            Attributes: new Dictionary<string, object?>
            {
                ["name"] = "Test Feature",
                ["value"] = 42
            });

        var query = new FeatureQuery();

        var components = new FeatureComponents(
            FeatureId: "123",
            RawId: "123",
            Properties: new Dictionary<string, object?>
            {
                ["name"] = "Test Feature",
                ["value"] = 42
            },
            Geometry: null);

        // Act
        var feature = _handler.ToFeature(
            context.Request,
            "service::layer",
            layer,
            record,
            query,
            components,
            null);

        // Assert
        Assert.NotNull(feature);
        var featureDict = feature as dynamic;
        Assert.Equal("Feature", featureDict.type);
        Assert.Equal("123", featureDict.id);
        Assert.NotNull(featureDict.properties);
        Assert.NotNull(featureDict.links);
    }
}

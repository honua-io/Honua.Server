using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Serialization;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Serialization;

[Trait("Category", "Unit")]
public class JsonLdFeatureFormatterTests
{
    private static LayerDefinition CreateTestLayer()
    {
        return new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            Description = "A test layer for JSON-LD formatting",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "name", DataType = "string", Alias = "Name" },
                new() { Name = "population", DataType = "integer", Alias = "Population" },
                new() { Name = "area", DataType = "double", Alias = "Area" },
                new() { Name = "established", DataType = "date", Alias = "Established Date" }
            }
        };
    }

    private static object CreateTestFeature(string id)
    {
        return new
        {
            type = "Feature",
            id,
            geometry = new
            {
                type = "Point",
                coordinates = new[] { -122.4194, 37.7749 }
            },
            properties = new Dictionary<string, object>
            {
                ["name"] = "San Francisco",
                ["population"] = 873965,
                ["area"] = 121.4,
                ["established"] = "1850-04-15"
            }
        };
    }

    [Fact]
    public void CreateContext_WithLayer_ReturnsValidJsonLdContext()
    {
        // Arrange
        var layer = CreateTestLayer();

        // Act
        var context = JsonLdFeatureFormatter.CreateContext(layer);

        // Assert
        Assert.NotNull(context);
        Assert.NotNull(context["@vocab"]);
        Assert.NotNull(context["geosparql"]);
        Assert.NotNull(context["geometry"]);
        Assert.NotNull(context["properties"]);
        Assert.NotNull(context["name"]);
        Assert.NotNull(context["population"]);
    }

    [Fact]
    public void CreateContext_WithLayerFields_MapsFieldTypesToXsd()
    {
        // Arrange
        var layer = CreateTestLayer();

        // Act
        var context = JsonLdFeatureFormatter.CreateContext(layer);

        // Assert
        var nameField = context["name"]?.AsObject();
        Assert.NotNull(nameField);
        Assert.Equal("http://www.w3.org/2001/XMLSchema#string", nameField["@type"]?.ToString());

        var populationField = context["population"]?.AsObject();
        Assert.NotNull(populationField);
        Assert.Equal("http://www.w3.org/2001/XMLSchema#integer", populationField["@type"]?.ToString());

        var areaField = context["area"]?.AsObject();
        Assert.NotNull(areaField);
        Assert.Equal("http://www.w3.org/2001/XMLSchema#double", areaField["@type"]?.ToString());
    }

    [Fact]
    public void ToJsonLdFeature_WithValidFeature_ReturnsJsonLdDocument()
    {
        // Arrange
        var layer = CreateTestLayer();
        var feature = CreateTestFeature("test-feature-1");
        var baseUri = "https://example.com";
        var collectionId = "test-collection";

        // Act
        var jsonLd = JsonLdFeatureFormatter.ToJsonLdFeature(baseUri, collectionId, layer, feature);

        // Assert
        Assert.NotNull(jsonLd);
        Assert.NotNull(jsonLd["@context"]);
        Assert.Equal("geosparql:Feature", jsonLd["@type"]?.ToString());
        Assert.Equal($"{baseUri}/ogc/collections/{collectionId}/items/test-feature-1", jsonLd["@id"]?.ToString());
        Assert.NotNull(jsonLd["geometry"]);
        Assert.NotNull(jsonLd["properties"]);
    }

    [Fact]
    public void ToJsonLdFeature_WithCustomContext_UsesProvidedContext()
    {
        // Arrange
        var layer = CreateTestLayer();
        var feature = CreateTestFeature("test-feature-2");
        var baseUri = "https://example.com";
        var collectionId = "test-collection";
        var customContext = new JsonObject { ["custom"] = "value" };

        // Act
        var jsonLd = JsonLdFeatureFormatter.ToJsonLdFeature(baseUri, collectionId, layer, feature, customContext);

        // Assert
        Assert.NotNull(jsonLd["@context"]);
        Assert.Equal("value", jsonLd["@context"]?["custom"]?.ToString());
    }

    [Fact]
    public void ToJsonLdFeature_PreservesGeometry()
    {
        // Arrange
        var layer = CreateTestLayer();
        var feature = CreateTestFeature("test-feature-3");
        var baseUri = "https://example.com";
        var collectionId = "test-collection";

        // Act
        var jsonLd = JsonLdFeatureFormatter.ToJsonLdFeature(baseUri, collectionId, layer, feature);

        // Assert
        var geometry = jsonLd["geometry"]?.AsObject();
        Assert.NotNull(geometry);
        Assert.Equal("Point", geometry["type"]?.ToString());
        Assert.NotNull(geometry["coordinates"]);
    }

    [Fact]
    public void ToJsonLdFeature_PreservesProperties()
    {
        // Arrange
        var layer = CreateTestLayer();
        var feature = CreateTestFeature("test-feature-4");
        var baseUri = "https://example.com";
        var collectionId = "test-collection";

        // Act
        var jsonLd = JsonLdFeatureFormatter.ToJsonLdFeature(baseUri, collectionId, layer, feature);

        // Assert
        var properties = jsonLd["properties"]?.AsObject();
        Assert.NotNull(properties);
        Assert.Equal("San Francisco", properties["name"]?.ToString());
        Assert.Equal(873965, properties["population"]?.GetValue<int>());
    }

    [Fact]
    public void ToJsonLdFeatureCollection_WithFeatures_ReturnsValidCollection()
    {
        // Arrange
        var layer = CreateTestLayer();
        var features = new List<object>
        {
            CreateTestFeature("feature-1"),
            CreateTestFeature("feature-2"),
            CreateTestFeature("feature-3")
        };
        var baseUri = "https://example.com";
        var collectionId = "test-collection";

        // Act
        var jsonLd = JsonLdFeatureFormatter.ToJsonLdFeatureCollection(
            baseUri, collectionId, layer, features, 3, 3, null);

        // Assert
        Assert.NotNull(jsonLd);
        Assert.Equal("geosparql:FeatureCollection", jsonLd["@type"]?.ToString());
        Assert.Equal($"{baseUri}/ogc/collections/{collectionId}/items", jsonLd["@id"]?.ToString());
        Assert.NotNull(jsonLd["@context"]);
        Assert.NotNull(jsonLd["features"]);
        var featuresArray = jsonLd["features"]?.AsArray();
        Assert.Equal(3, featuresArray?.Count);
    }

    [Fact]
    public void ToJsonLdFeatureCollection_IncludesMetadata()
    {
        // Arrange
        var layer = CreateTestLayer();
        var features = new List<object> { CreateTestFeature("feature-1") };
        var baseUri = "https://example.com";
        var collectionId = "test-collection";

        // Act
        var jsonLd = JsonLdFeatureFormatter.ToJsonLdFeatureCollection(
            baseUri, collectionId, layer, features, 10, 1, null);

        // Assert
        Assert.Equal(10, jsonLd["numberMatched"]?.GetValue<long>());
        Assert.Equal(1, jsonLd["numberReturned"]?.GetValue<long>());
    }

    [Fact]
    public void ToJsonLdFeatureCollection_FeaturesDoNotHaveDuplicateContext()
    {
        // Arrange
        var layer = CreateTestLayer();
        var features = new List<object> { CreateTestFeature("feature-1") };
        var baseUri = "https://example.com";
        var collectionId = "test-collection";

        // Act
        var jsonLd = JsonLdFeatureFormatter.ToJsonLdFeatureCollection(
            baseUri, collectionId, layer, features, 1, 1, null);

        // Assert
        var featuresArray = jsonLd["features"]?.AsArray();
        var firstFeature = featuresArray?[0]?.AsObject();
        Assert.NotNull(firstFeature);
        Assert.False(firstFeature.ContainsKey("@context"), "Features in a collection should not have duplicate @context");
    }

    [Fact]
    public void Serialize_WithValidJsonLd_ReturnsJsonString()
    {
        // Arrange
        var layer = CreateTestLayer();
        var feature = CreateTestFeature("test-feature-5");
        var baseUri = "https://example.com";
        var collectionId = "test-collection";
        var jsonLd = JsonLdFeatureFormatter.ToJsonLdFeature(baseUri, collectionId, layer, feature);

        // Act
        var serialized = JsonLdFeatureFormatter.Serialize(jsonLd);

        // Assert
        Assert.NotNull(serialized);
        Assert.NotEmpty(serialized);

        // Verify it's valid JSON
        var parsed = JsonDocument.Parse(serialized);
        Assert.NotNull(parsed);
    }

    [Fact]
    public void ToJsonLdFeature_ThrowsArgumentNullException_WhenFeatureIsNull()
    {
        // Arrange
        var layer = CreateTestLayer();
        var baseUri = "https://example.com";
        var collectionId = "test-collection";

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            JsonLdFeatureFormatter.ToJsonLdFeature(baseUri, collectionId, layer, null!));
    }
}

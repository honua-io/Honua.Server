using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Serialization;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Serialization;

[Trait("Category", "Unit")]
public class GeoJsonTFeatureFormatterTests
{
    private static object CreateTestFeature(string id, string? datetime = null)
    {
        var properties = new Dictionary<string, object>
        {
            ["name"] = "Test Feature",
            ["description"] = "A test feature"
        };

        if (datetime != null)
        {
            properties["datetime"] = datetime;
        }

        return new
        {
            type = "Feature",
            id,
            geometry = new
            {
                type = "Point",
                coordinates = new[] { -122.4194, 37.7749 }
            },
            properties
        };
    }

    [Fact]
    public void ToGeoJsonTFeature_WithValidFeature_ReturnsGeoJsonTDocument()
    {
        // Arrange
        var feature = CreateTestFeature("test-1");

        // Act
        var geoJsonT = GeoJsonTFeatureFormatter.ToGeoJsonTFeature(feature);

        // Assert
        Assert.NotNull(geoJsonT);
        Assert.Equal("Feature", geoJsonT["type"]?.ToString());
        Assert.Equal("test-1", geoJsonT["id"]?.ToString());
        Assert.NotNull(geoJsonT["geometry"]);
        Assert.NotNull(geoJsonT["properties"]);
    }

    [Fact]
    public void ToGeoJsonTFeature_PreservesGeometry()
    {
        // Arrange
        var feature = CreateTestFeature("test-2");

        // Act
        var geoJsonT = GeoJsonTFeatureFormatter.ToGeoJsonTFeature(feature);

        // Assert
        var geometry = geoJsonT["geometry"]?.AsObject();
        Assert.NotNull(geometry);
        Assert.Equal("Point", geometry["type"]?.ToString());
        var coordinates = geometry["coordinates"]?.AsArray();
        Assert.NotNull(coordinates);
        Assert.Equal(2, coordinates.Count);
    }

    [Fact]
    public void ToGeoJsonTFeature_PreservesProperties()
    {
        // Arrange
        var feature = CreateTestFeature("test-3", "2024-01-15T10:30:00Z");

        // Act
        var geoJsonT = GeoJsonTFeatureFormatter.ToGeoJsonTFeature(feature);

        // Assert
        var properties = geoJsonT["properties"]?.AsObject();
        Assert.NotNull(properties);
        Assert.Equal("Test Feature", properties["name"]?.ToString());
        Assert.Equal("A test feature", properties["description"]?.ToString());
    }

    [Fact]
    public void ToGeoJsonTFeature_WithTimeField_ExtractsTemporalProperty()
    {
        // Arrange
        var feature = CreateTestFeature("test-4", "2024-01-15T10:30:00Z");

        // Act
        var geoJsonT = GeoJsonTFeatureFormatter.ToGeoJsonTFeature(feature, timeField: "datetime");

        // Assert
        var when = geoJsonT["when"]?.AsObject();
        Assert.NotNull(when);
        Assert.NotNull(when["instant"]);
    }

    [Fact]
    public void ToGeoJsonTFeature_WithStartEndFields_ExtractsTemporalRange()
    {
        // Arrange
        var feature = new
        {
            type = "Feature",
            id = "test-5",
            geometry = new
            {
                type = "Point",
                coordinates = new[] { -122.4194, 37.7749 }
            },
            properties = new Dictionary<string, object>
            {
                ["name"] = "Event",
                ["startDate"] = "2024-01-15T10:00:00Z",
                ["endDate"] = "2024-01-15T18:00:00Z"
            }
        };

        // Act
        var geoJsonT = GeoJsonTFeatureFormatter.ToGeoJsonTFeature(
            feature,
            startTimeField: "startDate",
            endTimeField: "endDate");

        // Assert
        var when = geoJsonT["when"]?.AsObject();
        Assert.NotNull(when);
        Assert.NotNull(when["start"]);
        Assert.NotNull(when["end"]);
    }

    [Fact]
    public void ToGeoJsonTFeature_WithCommonTimeFields_AutoDetectsTime()
    {
        // Arrange
        var feature = CreateTestFeature("test-6", "2024-01-15T10:30:00Z");

        // Act
        var geoJsonT = GeoJsonTFeatureFormatter.ToGeoJsonTFeature(feature);

        // Assert
        var when = geoJsonT["when"]?.AsObject();
        Assert.NotNull(when);
        Assert.NotNull(when["instant"]);
        Assert.Equal("2024-01-15T10:30:00Z", when["instant"]?.ToString());
    }

    [Fact]
    public void ToGeoJsonTFeature_WithoutTemporalData_DoesNotAddWhen()
    {
        // Arrange
        var feature = new
        {
            type = "Feature",
            id = "test-7",
            geometry = new
            {
                type = "Point",
                coordinates = new[] { -122.4194, 37.7749 }
            },
            properties = new Dictionary<string, object>
            {
                ["name"] = "Static Feature"
            }
        };

        // Act
        var geoJsonT = GeoJsonTFeatureFormatter.ToGeoJsonTFeature(feature);

        // Assert
        Assert.False(geoJsonT.ContainsKey("when"));
    }

    [Fact]
    public void ToGeoJsonTFeatureCollection_WithFeatures_ReturnsValidCollection()
    {
        // Arrange
        var features = new List<object>
        {
            CreateTestFeature("feature-1", "2024-01-15T10:00:00Z"),
            CreateTestFeature("feature-2", "2024-01-15T11:00:00Z"),
            CreateTestFeature("feature-3", "2024-01-15T12:00:00Z")
        };

        // Act
        var geoJsonT = GeoJsonTFeatureFormatter.ToGeoJsonTFeatureCollection(
            features, 3, 3);

        // Assert
        Assert.NotNull(geoJsonT);
        Assert.Equal("FeatureCollection", geoJsonT["type"]?.ToString());
        Assert.NotNull(geoJsonT["features"]);
        var featuresArray = geoJsonT["features"]?.AsArray();
        Assert.Equal(3, featuresArray?.Count);
    }

    [Fact]
    public void ToGeoJsonTFeatureCollection_IncludesMetadata()
    {
        // Arrange
        var features = new List<object> { CreateTestFeature("feature-1") };

        // Act
        var geoJsonT = GeoJsonTFeatureFormatter.ToGeoJsonTFeatureCollection(
            features, 10, 1);

        // Assert
        Assert.Equal(10, geoJsonT["numberMatched"]?.GetValue<long>());
        Assert.Equal(1, geoJsonT["numberReturned"]?.GetValue<long>());
    }

    [Fact]
    public void ToGeoJsonTFeatureCollection_WithLinks_IncludesLinks()
    {
        // Arrange
        var features = new List<object> { CreateTestFeature("feature-1") };
        var links = new[]
        {
            new { rel = "self", href = "/items", type = "application/geo+json-t" }
        };

        // Act
        var geoJsonT = GeoJsonTFeatureFormatter.ToGeoJsonTFeatureCollection(
            features, 1, 1, links: links);

        // Assert
        Assert.NotNull(geoJsonT["links"]);
    }

    [Fact]
    public void Serialize_WithValidGeoJsonT_ReturnsJsonString()
    {
        // Arrange
        var feature = CreateTestFeature("test-8", "2024-01-15T10:30:00Z");
        var geoJsonT = GeoJsonTFeatureFormatter.ToGeoJsonTFeature(feature);

        // Act
        var serialized = GeoJsonTFeatureFormatter.Serialize(geoJsonT);

        // Assert
        Assert.NotNull(serialized);
        Assert.NotEmpty(serialized);

        // Verify it's valid JSON
        var parsed = JsonDocument.Parse(serialized);
        Assert.NotNull(parsed);
    }

    [Fact]
    public void ToGeoJsonTFeature_ThrowsArgumentNullException_WhenFeatureIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            GeoJsonTFeatureFormatter.ToGeoJsonTFeature(null!));
    }

    [Fact]
    public void ToGeoJsonTFeatureCollection_ThrowsArgumentNullException_WhenFeaturesIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            GeoJsonTFeatureFormatter.ToGeoJsonTFeatureCollection(null!, 0, 0));
    }

    [Fact]
    public void ToGeoJsonTFeatureCollection_WithTemporalFields_PropagatesFieldNames()
    {
        // Arrange
        var features = new List<object>
        {
            new
            {
                type = "Feature",
                id = "test-9",
                geometry = new { type = "Point", coordinates = new[] { 0.0, 0.0 } },
                properties = new Dictionary<string, object>
                {
                    ["start_time"] = "2024-01-15T10:00:00Z",
                    ["end_time"] = "2024-01-15T18:00:00Z"
                }
            }
        };

        // Act
        var geoJsonT = GeoJsonTFeatureFormatter.ToGeoJsonTFeatureCollection(
            features,
            1,
            1,
            startTimeField: "start_time",
            endTimeField: "end_time");

        // Assert
        var featuresArray = geoJsonT["features"]?.AsArray();
        var firstFeature = featuresArray?[0]?.AsObject();
        var when = firstFeature?["when"]?.AsObject();
        Assert.NotNull(when);
        Assert.NotNull(when["start"]);
        Assert.NotNull(when["end"]);
    }
}

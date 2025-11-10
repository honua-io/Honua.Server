// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Elevation;
using Xunit;

namespace Honua.Server.Core.Tests.Elevation;

/// <summary>
/// Unit tests for GeoJsonElevationEnricher.
/// Tests 3D coordinate enrichment for various geometry types.
/// </summary>
public class GeoJsonElevationEnricherTests
{
    [Fact]
    public async Task EnrichGeometryAsync_Point_AddsZCoordinate()
    {
        // Arrange
        var geometryJson = """
        {
            "type": "Point",
            "coordinates": [-122.4194, 37.7749]
        }
        """;
        var geometry = JsonDocument.Parse(geometryJson).RootElement;
        var elevationService = new DefaultElevationService(defaultElevation: 50.0);
        var context = new ElevationContext
        {
            ServiceId = "test",
            LayerId = "test",
            Configuration = new ElevationConfiguration { DefaultElevation = 50.0 }
        };

        // Act
        var enriched = await GeoJsonElevationEnricher.EnrichGeometryAsync(
            JsonNode.Parse(geometryJson),
            elevationService,
            context);

        // Assert
        Assert.NotNull(enriched);
        var enrichedObj = enriched.AsObject();
        Assert.True(enrichedObj.TryGetPropertyValue("coordinates", out var coords));
        var coordsArray = coords!.AsArray();
        Assert.Equal(3, coordsArray.Count);
        Assert.Equal(-122.4194, coordsArray[0]!.GetValue<double>());
        Assert.Equal(37.7749, coordsArray[1]!.GetValue<double>());
        Assert.Equal(50.0, coordsArray[2]!.GetValue<double>());
    }

    [Fact]
    public async Task EnrichGeometryAsync_LineString_AddsZCoordinatesToAllPoints()
    {
        // Arrange
        var geometryJson = """
        {
            "type": "LineString",
            "coordinates": [
                [-122.4194, 37.7749],
                [-122.4184, 37.7759],
                [-122.4174, 37.7769]
            ]
        }
        """;
        var elevationService = new DefaultElevationService(defaultElevation: 100.0);
        var context = new ElevationContext
        {
            ServiceId = "test",
            LayerId = "test",
            Configuration = new ElevationConfiguration { DefaultElevation = 100.0 }
        };

        // Act
        var enriched = await GeoJsonElevationEnricher.EnrichGeometryAsync(
            JsonNode.Parse(geometryJson),
            elevationService,
            context);

        // Assert
        Assert.NotNull(enriched);
        var enrichedObj = enriched.AsObject();
        Assert.True(enrichedObj.TryGetPropertyValue("coordinates", out var coords));
        var coordsArray = coords!.AsArray();
        Assert.Equal(3, coordsArray.Count);

        foreach (var point in coordsArray)
        {
            var pointArray = point!.AsArray();
            Assert.Equal(3, pointArray.Count);
            Assert.Equal(100.0, pointArray[2]!.GetValue<double>());
        }
    }

    [Fact]
    public async Task EnrichGeometryAsync_Polygon_AddsZCoordinatesToRings()
    {
        // Arrange
        var geometryJson = """
        {
            "type": "Polygon",
            "coordinates": [[
                [-122.4194, 37.7749],
                [-122.4184, 37.7749],
                [-122.4184, 37.7759],
                [-122.4194, 37.7759],
                [-122.4194, 37.7749]
            ]]
        }
        """;
        var elevationService = new DefaultElevationService(defaultElevation: 25.0);
        var context = new ElevationContext
        {
            ServiceId = "test",
            LayerId = "test",
            Configuration = new ElevationConfiguration { DefaultElevation = 25.0 }
        };

        // Act
        var enriched = await GeoJsonElevationEnricher.EnrichGeometryAsync(
            JsonNode.Parse(geometryJson),
            elevationService,
            context);

        // Assert
        Assert.NotNull(enriched);
        var enrichedObj = enriched.AsObject();
        Assert.True(enrichedObj.TryGetPropertyValue("coordinates", out var coords));
        var rings = coords!.AsArray();
        Assert.Single(rings);

        var ring = rings[0]!.AsArray();
        Assert.Equal(5, ring.Count);

        foreach (var point in ring)
        {
            var pointArray = point!.AsArray();
            Assert.Equal(3, pointArray.Count);
            Assert.Equal(25.0, pointArray[2]!.GetValue<double>());
        }
    }

    [Fact]
    public async Task EnrichGeometryAsync_MultiPoint_AddsZCoordinates()
    {
        // Arrange
        var geometryJson = """
        {
            "type": "MultiPoint",
            "coordinates": [
                [-122.4194, 37.7749],
                [-122.4184, 37.7759]
            ]
        }
        """;
        var elevationService = new DefaultElevationService(defaultElevation: 75.0);
        var context = new ElevationContext
        {
            ServiceId = "test",
            LayerId = "test",
            Configuration = new ElevationConfiguration { DefaultElevation = 75.0 }
        };

        // Act
        var enriched = await GeoJsonElevationEnricher.EnrichGeometryAsync(
            JsonNode.Parse(geometryJson),
            elevationService,
            context);

        // Assert
        Assert.NotNull(enriched);
        var enrichedObj = enriched.AsObject();
        Assert.True(enrichedObj.TryGetPropertyValue("coordinates", out var coords));
        var points = coords!.AsArray();
        Assert.Equal(2, points.Count);

        foreach (var point in points)
        {
            var pointArray = point!.AsArray();
            Assert.Equal(3, pointArray.Count);
            Assert.Equal(75.0, pointArray[2]!.GetValue<double>());
        }
    }

    [Fact]
    public async Task EnrichGeometryAsync_WithAttributeElevation_UsesCorrectElevation()
    {
        // Arrange
        var geometryJson = """
        {
            "type": "Point",
            "coordinates": [-122.4194, 37.7749]
        }
        """;
        var elevationService = new AttributeElevationService();
        var context = new ElevationContext
        {
            ServiceId = "test",
            LayerId = "test",
            FeatureAttributes = new Dictionary<string, object?>
            {
                { "elevation", 123.45 }
            },
            Configuration = new ElevationConfiguration
            {
                Source = "attribute",
                ElevationAttribute = "elevation"
            }
        };

        // Act
        var enriched = await GeoJsonElevationEnricher.EnrichGeometryAsync(
            JsonNode.Parse(geometryJson),
            elevationService,
            context);

        // Assert
        Assert.NotNull(enriched);
        var enrichedObj = enriched.AsObject();
        Assert.True(enrichedObj.TryGetPropertyValue("coordinates", out var coords));
        var coordsArray = coords!.AsArray();
        Assert.Equal(3, coordsArray.Count);
        Assert.Equal(123.45, coordsArray[2]!.GetValue<double>());
    }

    [Fact]
    public void AddBuildingHeight_WithValidHeight_AddsProperty()
    {
        // Arrange
        var properties = new JsonObject
        {
            ["name"] = "Test Building"
        };
        var context = new ElevationContext
        {
            ServiceId = "test",
            LayerId = "test",
            FeatureAttributes = new Dictionary<string, object?>
            {
                { "height_m", 50.5 }
            },
            Configuration = new ElevationConfiguration
            {
                Source = "attribute",
                HeightAttribute = "height_m",
                IncludeHeight = true
            }
        };

        // Act
        var enriched = GeoJsonElevationEnricher.AddBuildingHeight(properties, context);

        // Assert
        Assert.NotNull(enriched);
        Assert.True(enriched.TryGetPropertyValue("height", out var height));
        Assert.Equal(50.5, height!.GetValue<double>());
    }

    [Fact]
    public void AddBuildingHeight_WithoutHeight_ReturnsUnmodified()
    {
        // Arrange
        var properties = new JsonObject
        {
            ["name"] = "Test Building"
        };
        var context = new ElevationContext
        {
            ServiceId = "test",
            LayerId = "test",
            FeatureAttributes = new Dictionary<string, object?>
            {
                { "name", "test" }
            },
            Configuration = new ElevationConfiguration
            {
                Source = "attribute",
                IncludeHeight = false
            }
        };

        // Act
        var enriched = GeoJsonElevationEnricher.AddBuildingHeight(properties, context);

        // Assert
        Assert.NotNull(enriched);
        Assert.False(enriched.TryGetPropertyValue("height", out _));
        Assert.Equal("Test Building", enriched["name"]!.GetValue<string>());
    }
}

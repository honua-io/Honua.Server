using Honua.MapSDK.Models;
using System.Text.Json;
using Xunit;

namespace Honua.MapSDK.Tests.Models;

/// <summary>
/// Unit tests for GeoJson3D model.
/// Tests 3D GeoJSON parsing, dimension detection, and Z coordinate extraction.
/// </summary>
public class GeoJson3DTests
{
    #region Dimension Detection Tests

    [Fact]
    public void FromGeoJson_2DPoint_DetectsDimension2()
    {
        // Arrange
        var json = """
        {
            "type": "Point",
            "coordinates": [-122.4194, 37.7749]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;

        // Act
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Assert
        Assert.Equal(2, geoJson3D.Dimension);
        Assert.False(geoJson3D.HasZ);
        Assert.Equal("Point", geoJson3D.Type);
    }

    [Fact]
    public void FromGeoJson_3DPoint_DetectsDimension3()
    {
        // Arrange
        var json = """
        {
            "type": "Point",
            "coordinates": [-122.4194, 37.7749, 50.0]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;

        // Act
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Assert
        Assert.Equal(3, geoJson3D.Dimension);
        Assert.True(geoJson3D.HasZ);
        Assert.Equal("Point", geoJson3D.Type);
    }

    [Fact]
    public void FromGeoJson_4DPoint_DetectsDimension4()
    {
        // Arrange
        var json = """
        {
            "type": "Point",
            "coordinates": [-122.4194, 37.7749, 50.0, 100.0]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;

        // Act
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Assert
        Assert.Equal(4, geoJson3D.Dimension);
        Assert.True(geoJson3D.HasZ);
        Assert.Equal("Point", geoJson3D.Type);
    }

    #endregion

    #region LineString Tests

    [Fact]
    public void FromGeoJson_3DLineString_ParsesCorrectly()
    {
        // Arrange
        var json = """
        {
            "type": "LineString",
            "coordinates": [
                [-122.4194, 37.7749, 50.0],
                [-122.4184, 37.7759, 60.0],
                [-122.4174, 37.7769, 70.0]
            ]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;

        // Act
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Assert
        Assert.Equal("LineString", geoJson3D.Type);
        Assert.Equal(3, geoJson3D.Dimension);
        Assert.True(geoJson3D.HasZ);
        Assert.NotNull(geoJson3D.ZMin);
        Assert.NotNull(geoJson3D.ZMax);
        Assert.Equal(50.0, geoJson3D.ZMin.Value);
        Assert.Equal(70.0, geoJson3D.ZMax.Value);
    }

    [Fact]
    public void FromGeoJson_2DLineString_ParsesWithoutZ()
    {
        // Arrange
        var json = """
        {
            "type": "LineString",
            "coordinates": [
                [-122.4194, 37.7749],
                [-122.4184, 37.7759],
                [-122.4174, 37.7769]
            ]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;

        // Act
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Assert
        Assert.Equal("LineString", geoJson3D.Type);
        Assert.Equal(2, geoJson3D.Dimension);
        Assert.False(geoJson3D.HasZ);
        Assert.Null(geoJson3D.ZMin);
        Assert.Null(geoJson3D.ZMax);
    }

    #endregion

    #region Polygon Tests

    [Fact]
    public void FromGeoJson_3DPolygon_ParsesCorrectly()
    {
        // Arrange
        var json = """
        {
            "type": "Polygon",
            "coordinates": [[
                [-122.4194, 37.7749, 10.0],
                [-122.4184, 37.7749, 20.0],
                [-122.4184, 37.7759, 30.0],
                [-122.4194, 37.7759, 40.0],
                [-122.4194, 37.7749, 10.0]
            ]]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;

        // Act
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Assert
        Assert.Equal("Polygon", geoJson3D.Type);
        Assert.Equal(3, geoJson3D.Dimension);
        Assert.True(geoJson3D.HasZ);
        Assert.Equal(10.0, geoJson3D.ZMin.Value);
        Assert.Equal(40.0, geoJson3D.ZMax.Value);
    }

    #endregion

    #region OGC Type Name Tests

    [Fact]
    public void OgcTypeName_2DPoint_ReturnsPoint()
    {
        // Arrange
        var json = """
        {
            "type": "Point",
            "coordinates": [-122.4194, 37.7749]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Act
        var typeName = geoJson3D.OgcTypeName;

        // Assert
        Assert.Equal("Point", typeName);
    }

    [Fact]
    public void OgcTypeName_3DPoint_ReturnsPointZ()
    {
        // Arrange
        var json = """
        {
            "type": "Point",
            "coordinates": [-122.4194, 37.7749, 50.0]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Act
        var typeName = geoJson3D.OgcTypeName;

        // Assert
        Assert.Equal("PointZ", typeName);
    }

    [Fact]
    public void OgcTypeName_4DPoint_ReturnsPointZM()
    {
        // Arrange
        var json = """
        {
            "type": "Point",
            "coordinates": [-122.4194, 37.7749, 50.0, 100.0]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Act
        var typeName = geoJson3D.OgcTypeName;

        // Assert
        Assert.Equal("PointZM", typeName);
    }

    [Fact]
    public void OgcTypeName_3DLineString_ReturnsLineStringZ()
    {
        // Arrange
        var json = """
        {
            "type": "LineString",
            "coordinates": [
                [-122.4194, 37.7749, 50.0],
                [-122.4184, 37.7759, 60.0]
            ]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Act
        var typeName = geoJson3D.OgcTypeName;

        // Assert
        Assert.Equal("LineStringZ", typeName);
    }

    #endregion

    #region Z Statistics Tests

    [Fact]
    public void GetZStatistics_3DPoint_ReturnsStatistics()
    {
        // Arrange
        var json = """
        {
            "type": "Point",
            "coordinates": [-122.4194, 37.7749, 50.0]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Act
        var stats = geoJson3D.GetZStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(50.0, stats.Min);
        Assert.Equal(50.0, stats.Max);
        Assert.Equal(50.0, stats.Mean);
        Assert.Equal(0.0, stats.Range);
        Assert.Equal(1, stats.Count);
    }

    [Fact]
    public void GetZStatistics_3DLineString_CalculatesCorrectly()
    {
        // Arrange
        var json = """
        {
            "type": "LineString",
            "coordinates": [
                [-122.4194, 37.7749, 10.0],
                [-122.4184, 37.7759, 20.0],
                [-122.4174, 37.7769, 30.0]
            ]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Act
        var stats = geoJson3D.GetZStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(10.0, stats.Min);
        Assert.Equal(30.0, stats.Max);
        Assert.Equal(20.0, stats.Mean);
        Assert.Equal(20.0, stats.Range);
        Assert.Equal(3, stats.Count);
    }

    [Fact]
    public void GetZStatistics_2DGeometry_ReturnsNull()
    {
        // Arrange
        var json = """
        {
            "type": "Point",
            "coordinates": [-122.4194, 37.7749]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Act
        var stats = geoJson3D.GetZStatistics();

        // Assert
        Assert.Null(stats);
    }

    #endregion

    #region Z Range Validation Tests

    [Fact]
    public void ValidateZRange_ValidElevations_ReturnsTrue()
    {
        // Arrange
        var json = """
        {
            "type": "LineString",
            "coordinates": [
                [-122.4194, 37.7749, 10.0],
                [-122.4184, 37.7759, 100.0],
                [-122.4174, 37.7769, 500.0]
            ]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Act
        var isValid = geoJson3D.ValidateZRange();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateZRange_ElevationTooHigh_ReturnsFalse()
    {
        // Arrange - Elevation higher than Mt. Everest
        var json = """
        {
            "type": "Point",
            "coordinates": [-122.4194, 37.7749, 10000.0]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Act
        var isValid = geoJson3D.ValidateZRange();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateZRange_ElevationTooLow_ReturnsFalse()
    {
        // Arrange - Elevation lower than Dead Sea
        var json = """
        {
            "type": "Point",
            "coordinates": [-122.4194, 37.7749, -600.0]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Act
        var isValid = geoJson3D.ValidateZRange();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateZRange_CustomRange_ValidatesCorrectly()
    {
        // Arrange
        var json = """
        {
            "type": "Point",
            "coordinates": [-122.4194, 37.7749, 50.0]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Act
        var isValid = geoJson3D.ValidateZRange(minElevation: 0, maxElevation: 100);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateZRange_2DGeometry_ReturnsTrue()
    {
        // Arrange
        var json = """
        {
            "type": "Point",
            "coordinates": [-122.4194, 37.7749]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Act
        var isValid = geoJson3D.ValidateZRange();

        // Assert
        Assert.True(isValid); // No Z to validate
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void FromGeoJson_MissingType_ThrowsException()
    {
        // Arrange
        var json = """
        {
            "coordinates": [-122.4194, 37.7749, 50.0]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => GeoJson3D.FromGeoJson(geometryJson));
    }

    [Fact]
    public void FromGeoJson_MissingCoordinates_ThrowsException()
    {
        // Arrange
        var json = """
        {
            "type": "Point"
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => GeoJson3D.FromGeoJson(geometryJson));
    }

    #endregion

    #region Complex Geometry Tests

    [Fact]
    public void FromGeoJson_3DPolygonWithHole_ParsesCorrectly()
    {
        // Arrange - Polygon with exterior ring and hole
        var json = """
        {
            "type": "Polygon",
            "coordinates": [
                [
                    [-122.52, 37.78, 0.0],
                    [-122.50, 37.78, 0.0],
                    [-122.50, 37.80, 0.0],
                    [-122.52, 37.80, 0.0],
                    [-122.52, 37.78, 0.0]
                ],
                [
                    [-122.515, 37.785, 10.0],
                    [-122.505, 37.785, 10.0],
                    [-122.505, 37.795, 10.0],
                    [-122.515, 37.795, 10.0],
                    [-122.515, 37.785, 10.0]
                ]
            ]
        }
        """;
        var geometryJson = JsonDocument.Parse(json).RootElement;

        // Act
        var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

        // Assert
        Assert.Equal("Polygon", geoJson3D.Type);
        Assert.Equal(3, geoJson3D.Dimension);
        Assert.Equal(0.0, geoJson3D.ZMin.Value);
        Assert.Equal(10.0, geoJson3D.ZMax.Value);
    }

    #endregion
}

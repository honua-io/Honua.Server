// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Enterprise.Geoprocessing.Operations.Tests;

/// <summary>
/// Unit tests for GeometryLoader
/// These tests demonstrate the usage of the GeometryLoader class
/// Note: Collection and URL tests require live dependencies and are marked as integration tests
/// </summary>
public class GeometryLoaderTests
{
    #region WKT Tests

    [Fact]
    public async Task LoadFromWkt_Point_ReturnsGeometry()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "wkt",
            Source = "POINT(-122.4194 37.7749)"
        };

        // Act
        var geometries = await GeometryLoader.LoadGeometriesAsync(input);

        // Assert
        Assert.NotNull(geometries);
        Assert.Single(geometries);
        Assert.IsType<Point>(geometries[0]);
        var point = (Point)geometries[0];
        Assert.Equal(-122.4194, point.X, 4);
        Assert.Equal(37.7749, point.Y, 4);
    }

    [Fact]
    public async Task LoadFromWkt_Polygon_ReturnsGeometry()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "wkt",
            Source = "POLYGON((0 0, 10 0, 10 10, 0 10, 0 0))"
        };

        // Act
        var geometries = await GeometryLoader.LoadGeometriesAsync(input);

        // Assert
        Assert.NotNull(geometries);
        Assert.Single(geometries);
        Assert.IsType<Polygon>(geometries[0]);
        var polygon = (Polygon)geometries[0];
        Assert.Equal(5, polygon.Coordinates.Length); // 5 coordinates (closed ring)
    }

    [Fact]
    public async Task LoadFromWkt_InvalidFormat_ThrowsException()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "wkt",
            Source = "INVALID WKT FORMAT"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => GeometryLoader.LoadGeometriesAsync(input));
        Assert.Contains("Invalid WKT format", ex.Message);
    }

    #endregion

    #region GeoJSON Tests

    [Fact]
    public async Task LoadFromGeoJson_Point_ReturnsGeometry()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "geojson",
            Source = "{\"type\":\"Point\",\"coordinates\":[-122.4194,37.7749]}"
        };

        // Act
        var geometries = await GeometryLoader.LoadGeometriesAsync(input);

        // Assert
        Assert.NotNull(geometries);
        Assert.Single(geometries);
        Assert.IsType<Point>(geometries[0]);
        var point = (Point)geometries[0];
        Assert.Equal(-122.4194, point.X, 4);
        Assert.Equal(37.7749, point.Y, 4);
    }

    [Fact]
    public async Task LoadFromGeoJson_Polygon_ReturnsGeometry()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "geojson",
            Source = "{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[10,0],[10,10],[0,10],[0,0]]]}"
        };

        // Act
        var geometries = await GeometryLoader.LoadGeometriesAsync(input);

        // Assert
        Assert.NotNull(geometries);
        Assert.Single(geometries);
        Assert.IsType<Polygon>(geometries[0]);
    }

    [Fact]
    public async Task LoadFromGeoJson_GeometryCollection_ReturnsMultipleGeometries()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "geojson",
            Source = "{\"type\":\"GeometryCollection\",\"geometries\":[{\"type\":\"Point\",\"coordinates\":[0,0]},{\"type\":\"Point\",\"coordinates\":[1,1]}]}"
        };

        // Act
        var geometries = await GeometryLoader.LoadGeometriesAsync(input);

        // Assert
        Assert.NotNull(geometries);
        Assert.Equal(2, geometries.Count);
        Assert.All(geometries, g => Assert.IsType<Point>(g));
    }

    [Fact]
    public async Task LoadFromGeoJson_FeatureCollection_ExtractsAllGeometries()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "geojson",
            Source = @"{
                ""type"": ""FeatureCollection"",
                ""features"": [
                    {
                        ""type"": ""Feature"",
                        ""geometry"": {""type"": ""Point"", ""coordinates"": [0, 0]},
                        ""properties"": {""name"": ""Point1""}
                    },
                    {
                        ""type"": ""Feature"",
                        ""geometry"": {""type"": ""Point"", ""coordinates"": [1, 1]},
                        ""properties"": {""name"": ""Point2""}
                    },
                    {
                        ""type"": ""Feature"",
                        ""geometry"": {""type"": ""Point"", ""coordinates"": [2, 2]},
                        ""properties"": {""name"": ""Point3""}
                    }
                ]
            }"
        };

        // Act
        var geometries = await GeometryLoader.LoadGeometriesAsync(input);

        // Assert
        Assert.NotNull(geometries);
        Assert.Equal(3, geometries.Count);
        Assert.All(geometries, g => Assert.IsType<Point>(g));
    }

    [Fact]
    public async Task LoadFromGeoJson_FeatureCollectionWithEmptyGeometries_SkipsEmpty()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "geojson",
            Source = @"{
                ""type"": ""FeatureCollection"",
                ""features"": [
                    {
                        ""type"": ""Feature"",
                        ""geometry"": {""type"": ""Point"", ""coordinates"": [0, 0]},
                        ""properties"": {}
                    },
                    {
                        ""type"": ""Feature"",
                        ""geometry"": null,
                        ""properties"": {}
                    }
                ]
            }"
        };

        // Act
        var geometries = await GeometryLoader.LoadGeometriesAsync(input);

        // Assert
        Assert.NotNull(geometries);
        Assert.Single(geometries); // Only non-null geometry
    }

    [Fact]
    public async Task LoadFromGeoJson_InvalidFormat_ThrowsException()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "geojson",
            Source = "{\"invalid\": \"json\"}"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => GeometryLoader.LoadGeometriesAsync(input));
        Assert.Contains("Invalid GeoJSON format", ex.Message);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task LoadGeometries_NullInput_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => GeometryLoader.LoadGeometriesAsync(null!));
    }

    [Fact]
    public async Task LoadGeometries_EmptyType_ThrowsException()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "",
            Source = "POINT(0 0)"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => GeometryLoader.LoadGeometriesAsync(input));
        Assert.Contains("Input type cannot be null or empty", ex.Message);
    }

    [Fact]
    public async Task LoadGeometries_EmptySource_ThrowsException()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "wkt",
            Source = ""
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => GeometryLoader.LoadGeometriesAsync(input));
        Assert.Contains("Input source cannot be null or empty", ex.Message);
    }

    [Fact]
    public async Task LoadGeometries_UnsupportedType_ThrowsException()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "shapefile",
            Source = "some-file.shp"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotImplementedException>(
            () => GeometryLoader.LoadGeometriesAsync(input));
        Assert.Contains("not supported", ex.Message);
        Assert.Contains("shapefile", ex.Message);
    }

    #endregion

    #region Collection Tests (Integration)

    // Note: These tests require a live PostgreSQL database and are skipped by default
    // To run these tests, set up a test database and remove the Skip attribute

    [Fact(Skip = "Requires PostgreSQL database")]
    public async Task LoadFromCollection_ValidTable_ReturnsGeometries()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "collection",
            Source = "test_points",
            Parameters = new Dictionary<string, object>
            {
                { "connectionString", "Host=localhost;Database=test_geo;Username=test;Password=test" },
                { "geometryColumn", "geom" }
            }
        };

        // Act
        var geometries = await GeometryLoader.LoadGeometriesAsync(input);

        // Assert
        Assert.NotNull(geometries);
        Assert.NotEmpty(geometries);
    }

    [Fact(Skip = "Requires PostgreSQL database")]
    public async Task LoadFromCollection_WithFilter_ReturnsFilteredGeometries()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "collection",
            Source = "cities",
            Filter = "population > 1000000",
            Parameters = new Dictionary<string, object>
            {
                { "connectionString", "Host=localhost;Database=test_geo;Username=test;Password=test" }
            }
        };

        // Act
        var geometries = await GeometryLoader.LoadGeometriesAsync(input);

        // Assert
        Assert.NotNull(geometries);
        Assert.NotEmpty(geometries);
    }

    [Fact]
    public async Task LoadFromCollection_NoConnectionString_ThrowsException()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "collection",
            Source = "test_table"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => GeometryLoader.LoadGeometriesAsync(input));
        Assert.Contains("Connection string required", ex.Message);
    }

    #endregion

    #region URL Tests (Integration)

    // Note: These tests require internet connectivity and are skipped by default
    // Replace with actual test URLs or mock HTTP responses for unit testing

    [Fact(Skip = "Requires internet connectivity")]
    public async Task LoadFromUrl_ValidGeoJsonUrl_ReturnsGeometries()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "url",
            Source = "https://example.com/test-features.geojson"
        };

        // Act
        var geometries = await GeometryLoader.LoadGeometriesAsync(input);

        // Assert
        Assert.NotNull(geometries);
        Assert.NotEmpty(geometries);
    }

    [Fact]
    public async Task LoadFromUrl_InvalidUrl_ThrowsException()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "url",
            Source = "not-a-valid-url"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => GeometryLoader.LoadGeometriesAsync(input));
        Assert.Contains("Invalid URL format", ex.Message);
    }

    [Fact]
    public async Task LoadFromUrl_FtpScheme_ThrowsException()
    {
        // Arrange
        var input = new GeoprocessingInput
        {
            Type = "url",
            Source = "ftp://example.com/data.geojson"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => GeometryLoader.LoadGeometriesAsync(input));
        Assert.Contains("Only HTTP and HTTPS URLs are supported", ex.Message);
    }

    #endregion

    #region Case Sensitivity Tests

    [Fact]
    public async Task LoadGeometries_TypeCaseInsensitive_Works()
    {
        // Arrange - test that type matching is case-insensitive
        var inputs = new[]
        {
            new GeoprocessingInput { Type = "WKT", Source = "POINT(0 0)" },
            new GeoprocessingInput { Type = "Wkt", Source = "POINT(0 0)" },
            new GeoprocessingInput { Type = "wkt", Source = "POINT(0 0)" },
            new GeoprocessingInput { Type = "GEOJSON", Source = "{\"type\":\"Point\",\"coordinates\":[0,0]}" }
        };

        // Act & Assert - all should work
        foreach (var input in inputs)
        {
            var geometries = await GeometryLoader.LoadGeometriesAsync(input);
            Assert.NotNull(geometries);
            Assert.Single(geometries);
        }
    }

    #endregion
}

using System;
using Honua.Server.Core.GeometryValidation;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Geometry;

/// <summary>
/// Comprehensive tests for GeometryComplexityValidator DOS protection.
/// Validates that all complexity limits are enforced correctly.
/// </summary>
public sealed class GeometryComplexityValidatorTests
{
    private readonly GeometryFactory _factory = new();

    [Fact]
    public void Validate_NullGeometry_ThrowsArgumentNullException()
    {
        // Arrange
        var validator = new GeometryComplexityValidator();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!));
    }

    [Fact]
    public void Validate_SimplePoint_Succeeds()
    {
        // Arrange
        var validator = new GeometryComplexityValidator();
        var point = _factory.CreatePoint(new Coordinate(1.0, 2.0));

        // Act & Assert (no exception thrown)
        validator.Validate(point);
    }

    [Fact]
    public void Validate_SimpleLineString_Succeeds()
    {
        // Arrange
        var validator = new GeometryComplexityValidator();
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(1, 1),
            new Coordinate(2, 2)
        };
        var lineString = _factory.CreateLineString(coords);

        // Act & Assert
        validator.Validate(lineString);
    }

    [Fact]
    public void Validate_SimplePolygon_Succeeds()
    {
        // Arrange
        var validator = new GeometryComplexityValidator();
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(4, 0),
            new Coordinate(4, 4),
            new Coordinate(0, 4),
            new Coordinate(0, 0)
        };
        var polygon = _factory.CreatePolygon(coords);

        // Act & Assert
        validator.Validate(polygon);
    }

    [Fact]
    public void Validate_PolygonExceedsVertexCount_ThrowsComplexityException()
    {
        // Arrange
        var options = new GeometryComplexityOptions { MaxVertexCount = 100 };
        var validator = new GeometryComplexityValidator(options);

        // Create polygon with 101 vertices (exceeds limit)
        var coords = new Coordinate[102]; // 101 + 1 to close ring
        for (int i = 0; i < 101; i++)
        {
            coords[i] = new Coordinate(i, i);
        }
        coords[101] = coords[0]; // Close ring

        var polygon = _factory.CreatePolygon(coords);

        // Act & Assert
        var ex = Assert.Throws<GeometryComplexityException>(() => validator.Validate(polygon));
        Assert.Equal("VERTEX_COUNT_EXCEEDED", ex.ErrorCode);
        Assert.Equal(102, ex.ActualValue);
        Assert.Equal(100, ex.MaxValue);
    }

    [Fact]
    public void Validate_PolygonAtExactVertexLimit_Succeeds()
    {
        // Arrange
        var options = new GeometryComplexityOptions { MaxVertexCount = 100 };
        var validator = new GeometryComplexityValidator(options);

        // Create polygon with exactly 100 vertices
        var coords = new Coordinate[101]; // 100 + 1 to close ring
        for (int i = 0; i < 100; i++)
        {
            coords[i] = new Coordinate(i, i);
        }
        coords[100] = coords[0];

        var polygon = _factory.CreatePolygon(coords);

        // Act & Assert
        validator.Validate(polygon);
    }

    [Fact]
    public void Validate_PolygonWithManyRings_ThrowsComplexityException()
    {
        // Arrange
        var options = new GeometryComplexityOptions { MaxRingCount = 5 };
        var validator = new GeometryComplexityValidator(options);

        // Create exterior ring
        var shell = _factory.CreateLinearRing(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(100, 0),
            new Coordinate(100, 100),
            new Coordinate(0, 100),
            new Coordinate(0, 0)
        });

        // Create 6 holes (exceeds limit of 5 total rings)
        var holes = new LinearRing[6];
        for (int i = 0; i < 6; i++)
        {
            var offset = i * 10.0 + 5;
            holes[i] = _factory.CreateLinearRing(new[]
            {
                new Coordinate(offset, offset),
                new Coordinate(offset + 5, offset),
                new Coordinate(offset + 5, offset + 5),
                new Coordinate(offset, offset + 5),
                new Coordinate(offset, offset)
            });
        }

        var polygon = _factory.CreatePolygon(shell, holes);

        // Act & Assert
        var ex = Assert.Throws<GeometryComplexityException>(() => validator.Validate(polygon));
        Assert.Equal("RING_COUNT_EXCEEDED", ex.ErrorCode);
        Assert.Equal(7, ex.ActualValue); // 1 exterior + 6 holes
        Assert.Equal(5, ex.MaxValue);
    }

    [Fact]
    public void Validate_GeometryCollectionExceedsNestingDepth_ThrowsComplexityException()
    {
        // Arrange
        var options = new GeometryComplexityOptions { MaxNestingDepth = 2 };
        var validator = new GeometryComplexityValidator(options);

        // Create deeply nested collection: depth 3 (exceeds limit of 2)
        var point = _factory.CreatePoint(new Coordinate(1, 1));
        var collection1 = _factory.CreateGeometryCollection(new NetTopologySuite.Geometries.Geometry[] { point });
        var collection2 = _factory.CreateGeometryCollection(new NetTopologySuite.Geometries.Geometry[] { collection1 });
        var collection3 = _factory.CreateGeometryCollection(new NetTopologySuite.Geometries.Geometry[] { collection2 });

        // Act & Assert
        var ex = Assert.Throws<GeometryComplexityException>(() => validator.Validate(collection3));
        Assert.Equal("NESTING_DEPTH_EXCEEDED", ex.ErrorCode);
        Assert.True(ex.ActualValue > 2);
        Assert.Equal(2, ex.MaxValue);
    }

    [Fact]
    public void Validate_CoordinateExceedsPrecision_ThrowsComplexityException()
    {
        // Arrange
        var options = new GeometryComplexityOptions { MaxCoordinatePrecision = 6 };
        var validator = new GeometryComplexityValidator(options);

        // Create point with 10 decimal places (exceeds limit of 6)
        var point = _factory.CreatePoint(new Coordinate(1.1234567890, 2.9876543210));

        // Act & Assert
        var ex = Assert.Throws<GeometryComplexityException>(() => validator.Validate(point));
        Assert.Equal("COORDINATE_PRECISION_EXCEEDED", ex.ErrorCode);
        Assert.True(ex.ActualValue > 6);
        Assert.Equal(6, ex.MaxValue);
    }

    [Fact]
    public void Validate_GeometryExceedsSizeLimit_ThrowsComplexityException()
    {
        // Arrange
        var options = new GeometryComplexityOptions
        {
            MaxGeometrySizeBytes = 500, // Very small limit for testing
            MaxVertexCount = 1000000 // High enough to not trigger vertex limit first
        };
        var validator = new GeometryComplexityValidator(options);

        // Create large polygon with many vertices
        var coords = new Coordinate[501];
        for (int i = 0; i < 500; i++)
        {
            coords[i] = new Coordinate(i * 0.123456789, i * 0.987654321);
        }
        coords[500] = coords[0];

        var polygon = _factory.CreatePolygon(coords);

        // Act & Assert
        var ex = Assert.Throws<GeometryComplexityException>(() => validator.Validate(polygon));
        Assert.Equal("GEOMETRY_SIZE_EXCEEDED", ex.ErrorCode);
        Assert.True(ex.ActualValue > 500);
        Assert.Equal(500, ex.MaxValue);
    }

    [Fact]
    public void Validate_MultiPolygonWithExcessiveRings_ThrowsComplexityException()
    {
        // Arrange
        var options = new GeometryComplexityOptions { MaxRingCount = 10 };
        var validator = new GeometryComplexityValidator(options);

        // Create 6 simple polygons (6 rings total, within limit individually)
        var polygons = new Polygon[6];
        for (int i = 0; i < 6; i++)
        {
            var offset = i * 20.0;
            polygons[i] = _factory.CreatePolygon(new[]
            {
                new Coordinate(offset, offset),
                new Coordinate(offset + 10, offset),
                new Coordinate(offset + 10, offset + 10),
                new Coordinate(offset, offset + 10),
                new Coordinate(offset, offset)
            });
        }

        var multiPolygon = _factory.CreateMultiPolygon(polygons);

        // Act & Assert - Should succeed as 6 < 10
        validator.Validate(multiPolygon);

        // Now add more polygons to exceed limit
        var morePolygons = new Polygon[11];
        for (int i = 0; i < 11; i++)
        {
            var offset = i * 20.0;
            morePolygons[i] = _factory.CreatePolygon(new[]
            {
                new Coordinate(offset, offset),
                new Coordinate(offset + 10, offset),
                new Coordinate(offset + 10, offset + 10),
                new Coordinate(offset, offset + 10),
                new Coordinate(offset, offset)
            });
        }

        var tooManyRings = _factory.CreateMultiPolygon(morePolygons);

        var ex = Assert.Throws<GeometryComplexityException>(() => validator.Validate(tooManyRings));
        Assert.Equal("RING_COUNT_EXCEEDED", ex.ErrorCode);
    }

    [Fact]
    public void Validate_DisabledValidation_DoesNotThrow()
    {
        // Arrange
        var options = new GeometryComplexityOptions
        {
            EnableValidation = false,
            MaxVertexCount = 10 // Very restrictive, but disabled
        };
        var validator = new GeometryComplexityValidator(options);

        // Create polygon that would exceed limits if validation were enabled
        var coords = new Coordinate[102];
        for (int i = 0; i < 101; i++)
        {
            coords[i] = new Coordinate(i, i);
        }
        coords[101] = coords[0];
        var polygon = _factory.CreatePolygon(coords);

        // Act & Assert - Should succeed because validation is disabled
        validator.Validate(polygon);
    }

    [Fact]
    public void ValidateCollection_MultipleGeometries_ValidatesCumulativeLimits()
    {
        // Arrange
        var options = new GeometryComplexityOptions { MaxVertexCount = 100 };
        var validator = new GeometryComplexityValidator(options);

        // Create 3 polygons, each with 40 vertices (120 total, exceeds limit)
        var geometries = new NetTopologySuite.Geometries.Geometry[3];
        for (int j = 0; j < 3; j++)
        {
            var coords = new Coordinate[41];
            for (int i = 0; i < 40; i++)
            {
                coords[i] = new Coordinate(j * 100 + i, i);
            }
            coords[40] = coords[0];
            geometries[j] = _factory.CreatePolygon(coords);
        }

        // Act & Assert
        var ex = Assert.Throws<GeometryComplexityException>(() => validator.ValidateCollection(geometries));
        Assert.Equal("VERTEX_COUNT_EXCEEDED", ex.ErrorCode);
        Assert.True(ex.ActualValue > 100);
    }

    [Fact]
    public void Validate_MultiPoint_Succeeds()
    {
        // Arrange
        var validator = new GeometryComplexityValidator();
        var points = new Point[]
        {
            _factory.CreatePoint(new Coordinate(0, 0)),
            _factory.CreatePoint(new Coordinate(1, 1)),
            _factory.CreatePoint(new Coordinate(2, 2))
        };
        var multiPoint = _factory.CreateMultiPoint(points);

        // Act & Assert
        validator.Validate(multiPoint);
    }

    [Fact]
    public void Validate_MultiLineString_Succeeds()
    {
        // Arrange
        var validator = new GeometryComplexityValidator();
        var lines = new LineString[]
        {
            _factory.CreateLineString(new[] { new Coordinate(0, 0), new Coordinate(1, 1) }),
            _factory.CreateLineString(new[] { new Coordinate(2, 2), new Coordinate(3, 3) })
        };
        var multiLineString = _factory.CreateMultiLineString(lines);

        // Act & Assert
        validator.Validate(multiLineString);
    }

    [Fact]
    public void Options_InvalidMaxVertexCount_ThrowsArgumentException()
    {
        // Arrange
        var options = new GeometryComplexityOptions { MaxVertexCount = -1 };

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        Assert.Contains("MaxVertexCount", ex.Message);
    }

    [Fact]
    public void Options_InvalidMaxRingCount_ThrowsArgumentException()
    {
        // Arrange
        var options = new GeometryComplexityOptions { MaxRingCount = 0 };

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        Assert.Contains("MaxRingCount", ex.Message);
    }

    [Fact]
    public void Options_InvalidMaxCoordinatePrecision_ThrowsArgumentException()
    {
        // Arrange
        var options = new GeometryComplexityOptions { MaxCoordinatePrecision = 20 };

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        Assert.Contains("MaxCoordinatePrecision", ex.Message);
    }

    [Fact]
    public void Exception_VertexCountExceeded_HasCorrectProperties()
    {
        // Arrange & Act
        var ex = GeometryComplexityException.VertexCountExceeded(100000, 10000);

        // Assert
        Assert.Equal("VERTEX_COUNT_EXCEEDED", ex.ErrorCode);
        Assert.Equal(100000, ex.ActualValue);
        Assert.Equal(10000, ex.MaxValue);
        Assert.Contains("100,000", ex.Message);
        Assert.Contains("10,000", ex.Message);
        Assert.NotNull(ex.Suggestion);
        Assert.Contains("ST_Simplify", ex.Suggestion);
    }

    [Fact]
    public void Exception_RingCountExceeded_HasCorrectProperties()
    {
        // Arrange & Act
        var ex = GeometryComplexityException.RingCountExceeded(150, 100);

        // Assert
        Assert.Equal("RING_COUNT_EXCEEDED", ex.ErrorCode);
        Assert.Equal(150, ex.ActualValue);
        Assert.Equal(100, ex.MaxValue);
        Assert.NotNull(ex.Suggestion);
    }

    [Fact]
    public void Validate_LineStringExceedsCoordinateCount_ThrowsComplexityException()
    {
        // Arrange
        var options = new GeometryComplexityOptions { MaxCoordinateCount = 50 };
        var validator = new GeometryComplexityValidator(options);

        // Create line string with 51 coordinates
        var coords = new Coordinate[51];
        for (int i = 0; i < 51; i++)
        {
            coords[i] = new Coordinate(i, i);
        }
        var lineString = _factory.CreateLineString(coords);

        // Act & Assert
        var ex = Assert.Throws<GeometryComplexityException>(() => validator.Validate(lineString));
        Assert.Equal("COORDINATE_COUNT_EXCEEDED", ex.ErrorCode);
        Assert.Equal(51, ex.ActualValue);
        Assert.Equal(50, ex.MaxValue);
    }

    [Fact]
    public void Validate_CoordinatesWithinPrecisionLimit_Succeeds()
    {
        // Arrange
        var options = new GeometryComplexityOptions { MaxCoordinatePrecision = 9 };
        var validator = new GeometryComplexityValidator(options);

        // Create point with 6 decimal places (within limit)
        var point = _factory.CreatePoint(new Coordinate(1.123456, 2.987654));

        // Act & Assert
        validator.Validate(point);
    }
}

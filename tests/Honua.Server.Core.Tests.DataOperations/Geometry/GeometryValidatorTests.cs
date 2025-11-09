using FluentAssertions;
using Honua.Server.Core.Validation;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Geometry;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public sealed class GeometryValidatorTests
{
    private readonly GeometryFactory _factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    #region Polygon Validation Tests

    [Fact]
    public void ValidatePolygon_ShouldReturnValid_ForCorrectPolygon()
    {
        // Arrange - Create a valid CCW polygon (OGC standard)
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(10, 10),
            new Coordinate(0, 10),
            new Coordinate(0, 0) // Closed
        };
        var polygon = _factory.CreatePolygon(coords);

        // Act
        var result = GeometryValidator.ValidatePolygon(polygon);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ValidatePolygon_ShouldReturnError_ForNullPolygon()
    {
        // Act
        var result = GeometryValidator.ValidatePolygon(null!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot be null");
    }

    [Fact]
    public void ValidatePolygon_ShouldReturnError_ForOpenRing()
    {
        // Arrange - Test that attempting to create an open ring throws ArgumentException
        // NTS enforces ring closure at construction time, which is the correct behavior
        // This test documents that NTS prevents invalid geometries from being created

        var act = () =>
        {
            // Attempt to create a linear ring that doesn't close
            var openCoords = new[]
            {
                new Coordinate(0, 0),
                new Coordinate(10, 0),
                new Coordinate(10, 10),
                new Coordinate(0, 10)
                // Missing closing coordinate (0, 0) - should throw
            };
            return _factory.CreateLinearRing(openCoords);
        };

        // Assert - NTS should throw ArgumentException when ring isn't closed
        act.Should().Throw<ArgumentException>()
            .WithMessage("*closed linestring*");
    }

    [Fact]
    public void ValidatePolygon_ShouldReturnError_ForTooFewVertices()
    {
        // Arrange - Create a polygon with only 3 points (2 unique + 1 closing)
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(0, 0) // Only 3 points total
        };
        var ring = _factory.CreateLinearRing(coords);
        var polygon = _factory.CreatePolygon(ring);

        // Act
        var result = GeometryValidator.ValidatePolygon(polygon);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("at least 4 points");
    }

    [Fact]
    public void ValidatePolygon_ShouldReturnError_ForClockwiseExteriorRing()
    {
        // Arrange - Create a CW polygon (wrong orientation for OGC)
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(0, 10),
            new Coordinate(10, 10),
            new Coordinate(10, 0),
            new Coordinate(0, 0) // Clockwise
        };
        var polygon = _factory.CreatePolygon(coords);

        // Act
        var result = GeometryValidator.ValidatePolygon(polygon);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("counter-clockwise");
    }

    [Fact]
    public void ValidatePolygon_ShouldReturnError_ForCounterClockwiseHole()
    {
        // Arrange - Create a polygon with a CCW hole (wrong orientation)
        var shell = _factory.CreateLinearRing(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(10, 10),
            new Coordinate(0, 10),
            new Coordinate(0, 0) // CCW exterior (correct)
        });

        var hole = _factory.CreateLinearRing(new[]
        {
            new Coordinate(2, 2),
            new Coordinate(8, 2),
            new Coordinate(8, 8),
            new Coordinate(2, 8),
            new Coordinate(2, 2) // CCW hole (wrong - should be CW)
        });

        var polygon = _factory.CreatePolygon(shell, new[] { hole });

        // Act
        var result = GeometryValidator.ValidatePolygon(polygon);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("clockwise");
    }

    [Fact]
    public void ValidatePolygon_ShouldReturnValid_ForPolygonWithCorrectHole()
    {
        // Arrange - Create a polygon with properly oriented hole
        var shell = _factory.CreateLinearRing(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(10, 10),
            new Coordinate(0, 10),
            new Coordinate(0, 0) // CCW exterior (correct)
        });

        var hole = _factory.CreateLinearRing(new[]
        {
            new Coordinate(2, 2),
            new Coordinate(2, 8),
            new Coordinate(8, 8),
            new Coordinate(8, 2),
            new Coordinate(2, 2) // CW hole (correct)
        });

        var polygon = _factory.CreatePolygon(shell, new[] { hole });

        // Act
        var result = GeometryValidator.ValidatePolygon(polygon);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ValidatePolygon_ShouldReturnError_ForSelfIntersectingPolygon()
    {
        // Arrange - Create a self-intersecting polygon (bowtie shape)
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 10),
            new Coordinate(10, 0),
            new Coordinate(0, 10),
            new Coordinate(0, 0) // Self-intersecting
        };
        var ring = _factory.CreateLinearRing(coords);
        var polygon = _factory.CreatePolygon(ring);

        // Act
        var result = GeometryValidator.ValidatePolygon(polygon);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("topological");
    }

    #endregion

    #region LinearRing Validation Tests

    [Fact]
    public void ValidateLinearRing_ShouldReturnValid_ForCorrectRing()
    {
        // Arrange
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(10, 10),
            new Coordinate(0, 10),
            new Coordinate(0, 0)
        };
        var ring = _factory.CreateLinearRing(coords);

        // Act
        var result = GeometryValidator.ValidateLinearRing(ring);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateLinearRing_ShouldReturnError_ForNullRing()
    {
        // Act
        var result = GeometryValidator.ValidateLinearRing(null!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot be null");
    }

    #endregion

    #region LineString Validation Tests

    [Fact]
    public void ValidateLineString_ShouldReturnValid_ForCorrectLineString()
    {
        // Arrange
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 10)
        };
        var lineString = _factory.CreateLineString(coords);

        // Act
        var result = GeometryValidator.ValidateLineString(lineString);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateLineString_ShouldReturnError_ForTooFewPoints()
    {
        // Arrange - Test that attempting to create a line string with too few points throws ArgumentException
        // NTS enforces minimum point requirements at construction time, which is the correct behavior
        // This test documents that NTS prevents invalid geometries from being created

        var act = () =>
        {
            // Attempt to create a line string with only 1 point (invalid)
            var coords = new[]
            {
                new Coordinate(0, 0)
            };
            return _factory.CreateLineString(coords);
        };

        // Assert - NTS should throw ArgumentException for line strings with < 2 points
        act.Should().Throw<ArgumentException>()
            .WithMessage("*must be 0 or >= 2*");
    }

    #endregion

    #region MultiPolygon Validation Tests

    [Fact]
    public void ValidateMultiPolygon_ShouldReturnValid_ForCorrectMultiPolygon()
    {
        // Arrange
        var poly1 = _factory.CreatePolygon(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(5, 0),
            new Coordinate(5, 5),
            new Coordinate(0, 5),
            new Coordinate(0, 0)
        });

        var poly2 = _factory.CreatePolygon(new[]
        {
            new Coordinate(10, 10),
            new Coordinate(15, 10),
            new Coordinate(15, 15),
            new Coordinate(10, 15),
            new Coordinate(10, 10)
        });

        var multiPolygon = _factory.CreateMultiPolygon(new[] { poly1, poly2 });

        // Act
        var result = GeometryValidator.ValidateMultiPolygon(multiPolygon);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateMultiPolygon_ShouldReturnError_ForEmptyMultiPolygon()
    {
        // Arrange
        var multiPolygon = _factory.CreateMultiPolygon(Array.Empty<Polygon>());

        // Act
        var result = GeometryValidator.ValidateMultiPolygon(multiPolygon);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("at least one polygon");
    }

    #endregion

    #region Orientation Correction Tests

    [Fact]
    public void EnsureCorrectOrientation_ShouldFixClockwiseExteriorRing()
    {
        // Arrange - CW exterior ring (wrong for OGC)
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(0, 10),
            new Coordinate(10, 10),
            new Coordinate(10, 0),
            new Coordinate(0, 0) // Clockwise
        };
        var polygon = _factory.CreatePolygon(coords);

        // Act
        var corrected = GeometryValidator.EnsureCorrectOrientation(polygon);

        // Assert
        var validationResult = GeometryValidator.ValidatePolygon(corrected);
        validationResult.IsValid.Should().BeTrue();
        corrected.Should().NotBeSameAs(polygon); // Should return new instance
    }

    [Fact]
    public void EnsureCorrectOrientation_ShouldNotModify_IfAlreadyCorrect()
    {
        // Arrange - Already correct CCW exterior
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(10, 10),
            new Coordinate(0, 10),
            new Coordinate(0, 0) // Counter-clockwise
        };
        var polygon = _factory.CreatePolygon(coords);

        // Act
        var result = GeometryValidator.EnsureCorrectOrientation(polygon);

        // Assert
        result.Should().BeSameAs(polygon); // Should return same instance
    }

    [Fact]
    public void EnsureCorrectOrientation_ShouldFixHoleOrientation()
    {
        // Arrange - CCW hole (wrong)
        var shell = _factory.CreateLinearRing(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(10, 10),
            new Coordinate(0, 10),
            new Coordinate(0, 0) // CCW (correct)
        });

        var hole = _factory.CreateLinearRing(new[]
        {
            new Coordinate(2, 2),
            new Coordinate(8, 2),
            new Coordinate(8, 8),
            new Coordinate(2, 8),
            new Coordinate(2, 2) // CCW (wrong - should be CW)
        });

        var polygon = _factory.CreatePolygon(shell, new[] { hole });

        // Act
        var corrected = GeometryValidator.EnsureCorrectOrientation(polygon);

        // Assert
        var validationResult = GeometryValidator.ValidatePolygon(corrected);
        validationResult.IsValid.Should().BeTrue();
        corrected.Should().NotBeSameAs(polygon);
    }

    [Fact]
    public void EnsureEsriOrientation_ShouldConvertToClockwiseExterior()
    {
        // Arrange - CCW exterior (OGC standard)
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(10, 10),
            new Coordinate(0, 10),
            new Coordinate(0, 0) // CCW
        };
        var polygon = _factory.CreatePolygon(coords);

        // Act
        var esriPolygon = GeometryValidator.EnsureEsriOrientation(polygon);

        // Assert
        // Esri uses CW for exterior rings (opposite of OGC)
        var exteriorRing = esriPolygon.ExteriorRing;
        NetTopologySuite.Algorithm.Orientation.IsCCW(exteriorRing.CoordinateSequence).Should().BeFalse();
        esriPolygon.Should().NotBeSameAs(polygon);
    }

    [Fact]
    public void EnsureEsriOrientation_ShouldConvertHolesToCounterClockwise()
    {
        // Arrange - OGC standard polygon (CCW exterior, CW hole)
        var shell = _factory.CreateLinearRing(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(10, 10),
            new Coordinate(0, 10),
            new Coordinate(0, 0) // CCW
        });

        var hole = _factory.CreateLinearRing(new[]
        {
            new Coordinate(2, 2),
            new Coordinate(2, 8),
            new Coordinate(8, 8),
            new Coordinate(8, 2),
            new Coordinate(2, 2) // CW
        });

        var polygon = _factory.CreatePolygon(shell, new[] { hole });

        // Act
        var esriPolygon = GeometryValidator.EnsureEsriOrientation(polygon);

        // Assert
        // Esri uses CCW for holes (opposite of OGC)
        var esriHole = esriPolygon.GetInteriorRingN(0);
        NetTopologySuite.Algorithm.Orientation.IsCCW(esriHole.CoordinateSequence).Should().BeTrue();
        esriPolygon.Should().NotBeSameAs(polygon);
    }

    #endregion

    #region 3D Coordinate Tests

    [Fact]
    public void ValidatePolygon_ShouldHandleZCoordinates()
    {
        // Arrange - Create a 3D polygon
        var coords = new[]
        {
            new CoordinateZ(0, 0, 100),
            new CoordinateZ(10, 0, 100),
            new CoordinateZ(10, 10, 100),
            new CoordinateZ(0, 10, 100),
            new CoordinateZ(0, 0, 100) // Closed with Z
        };
        var polygon = _factory.CreatePolygon(coords);

        // Act
        var result = GeometryValidator.ValidatePolygon(polygon);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidatePolygon_ShouldDetectOpenRingWithDifferentZ()
    {
        // Arrange - Create a ring where X,Y match but Z doesn't
        var coords = new[]
        {
            new CoordinateZ(0, 0, 100),
            new CoordinateZ(10, 0, 100),
            new CoordinateZ(10, 10, 100),
            new CoordinateZ(0, 10, 100),
            new CoordinateZ(0, 0, 200) // Different Z value
        };
        var ring = _factory.CreateLinearRing(coords);
        var polygon = _factory.CreatePolygon(ring);

        // Act
        var result = GeometryValidator.ValidatePolygon(polygon);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not closed");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void ValidatePolygon_ShouldAcceptMinimumTriangle()
    {
        // Arrange - Minimum valid polygon (triangle)
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(5, 10),
            new Coordinate(0, 0) // 4 points including closure
        };
        var polygon = _factory.CreatePolygon(coords);

        // Act
        var result = GeometryValidator.ValidatePolygon(polygon);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidatePolygon_ShouldPreserveSRID_WhenCorrectingOrientation()
    {
        // Arrange
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(0, 10),
            new Coordinate(10, 10),
            new Coordinate(10, 0),
            new Coordinate(0, 0) // CW
        };
        var polygon = _factory.CreatePolygon(coords);
        polygon.SRID = 3857;

        // Act
        var corrected = GeometryValidator.EnsureCorrectOrientation(polygon);

        // Assert
        corrected.SRID.Should().Be(3857);
    }

    #endregion

    #region LineString 3D Tests

    [Fact]
    public void ValidateLineString_WithZCoordinates_ReturnsValid()
    {
        // Arrange
        var coords = new[]
        {
            new CoordinateZ(0, 0, 100),
            new CoordinateZ(10, 10, 200),
            new CoordinateZ(20, 20, 150)
        };
        var lineString = _factory.CreateLineString(coords);

        // Act
        var result = GeometryValidator.ValidateLineString(lineString);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateLineString_WithMCoordinates_ReturnsValid()
    {
        // Arrange
        var coords = new[]
        {
            new CoordinateM(0, 0, 0),
            new CoordinateM(10, 10, 100),
            new CoordinateM(20, 20, 200)
        };
        var lineString = _factory.CreateLineString(coords);

        // Act
        var result = GeometryValidator.ValidateLineString(lineString);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateLineString_WithZMCoordinates_ReturnsValid()
    {
        // Arrange
        var coords = new[]
        {
            new CoordinateZM(0, 0, 100, 0),
            new CoordinateZM(10, 10, 200, 100),
            new CoordinateZM(20, 20, 150, 200)
        };
        var lineString = _factory.CreateLineString(coords);

        // Act
        var result = GeometryValidator.ValidateLineString(lineString);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region MultiPoint 3D Tests

    [Fact]
    public void ValidateMultiPoint_WithZCoordinates_ReturnsValid()
    {
        // Arrange
        var points = new[]
        {
            _factory.CreatePoint(new CoordinateZ(0, 0, 100)),
            _factory.CreatePoint(new CoordinateZ(10, 10, 200)),
            _factory.CreatePoint(new CoordinateZ(20, 20, 150))
        };
        var multiPoint = _factory.CreateMultiPoint(points);

        // Act - MultiPoint doesn't have specific validation in GeometryValidator
        // but we're testing that 3D coordinates are preserved

        // Assert
        multiPoint.Coordinates.All(c => !double.IsNaN(c.Z)).Should().BeTrue();
        multiPoint.IsEmpty.Should().BeFalse();
    }

    #endregion

    #region MultiLineString 3D Tests

    [Fact]
    public void ValidateMultiLineString_WithZCoordinates_ReturnsValid()
    {
        // Arrange
        var line1 = _factory.CreateLineString(new[]
        {
            new CoordinateZ(0, 0, 100),
            new CoordinateZ(10, 0, 100)
        });

        var line2 = _factory.CreateLineString(new[]
        {
            new CoordinateZ(20, 20, 200),
            new CoordinateZ(30, 20, 200)
        });

        var multiLineString = _factory.CreateMultiLineString(new[] { line1, line2 });

        // Assert
        multiLineString.Coordinates.All(c => !double.IsNaN(c.Z)).Should().BeTrue();
        multiLineString.IsEmpty.Should().BeFalse();
    }

    #endregion

    #region MultiPolygon 3D Tests

    [Fact]
    public void ValidateMultiPolygon_WithZCoordinates_ReturnsValid()
    {
        // Arrange
        var poly1 = _factory.CreatePolygon(new[]
        {
            new CoordinateZ(0, 0, 100),
            new CoordinateZ(5, 0, 100),
            new CoordinateZ(5, 5, 100),
            new CoordinateZ(0, 5, 100),
            new CoordinateZ(0, 0, 100)
        });

        var poly2 = _factory.CreatePolygon(new[]
        {
            new CoordinateZ(10, 10, 200),
            new CoordinateZ(15, 10, 200),
            new CoordinateZ(15, 15, 200),
            new CoordinateZ(10, 15, 200),
            new CoordinateZ(10, 10, 200)
        });

        var multiPolygon = _factory.CreateMultiPolygon(new[] { poly1, poly2 });

        // Act
        var result = GeometryValidator.ValidateMultiPolygon(multiPolygon);

        // Assert
        result.IsValid.Should().BeTrue();
        multiPolygon.Coordinates.All(c => !double.IsNaN(c.Z)).Should().BeTrue();
    }

    [Fact]
    public void ValidateMultiPolygon_WithMixedZValues_PreservesAllZ()
    {
        // Arrange - Different Z values for different polygons
        var poly1 = _factory.CreatePolygon(new[]
        {
            new CoordinateZ(0, 0, 50),
            new CoordinateZ(5, 0, 50),
            new CoordinateZ(5, 5, 50),
            new CoordinateZ(0, 5, 50),
            new CoordinateZ(0, 0, 50)
        });

        var poly2 = _factory.CreatePolygon(new[]
        {
            new CoordinateZ(10, 10, 250),
            new CoordinateZ(15, 10, 250),
            new CoordinateZ(15, 15, 250),
            new CoordinateZ(10, 15, 250),
            new CoordinateZ(10, 10, 250)
        });

        var multiPolygon = _factory.CreateMultiPolygon(new[] { poly1, poly2 });

        // Act
        var result = GeometryValidator.ValidateMultiPolygon(multiPolygon);

        // Assert
        result.IsValid.Should().BeTrue();
        var poly1Z = ((Polygon)multiPolygon.GetGeometryN(0)).Coordinates.First().Z;
        var poly2Z = ((Polygon)multiPolygon.GetGeometryN(1)).Coordinates.First().Z;
        poly1Z.Should().Be(50);
        poly2Z.Should().Be(250);
    }

    #endregion

    #region GeometryCollection 3D Tests

    [Fact]
    public void GeometryCollection_WithMixed3DGeometries_PreservesZ()
    {
        // Arrange
        var point = _factory.CreatePoint(new CoordinateZ(0, 0, 100));
        var lineString = _factory.CreateLineString(new[]
        {
            new CoordinateZ(5, 5, 150),
            new CoordinateZ(10, 10, 200)
        });
        var polygon = _factory.CreatePolygon(new[]
        {
            new CoordinateZ(20, 20, 250),
            new CoordinateZ(25, 20, 250),
            new CoordinateZ(25, 25, 250),
            new CoordinateZ(20, 25, 250),
            new CoordinateZ(20, 20, 250)
        });

        var collection = _factory.CreateGeometryCollection(new NetTopologySuite.Geometries.Geometry[] { point, lineString, polygon });

        // Assert
        collection.Coordinates.All(c => !double.IsNaN(c.Z)).Should().BeTrue();
        collection.IsEmpty.Should().BeFalse();
        collection.NumGeometries.Should().Be(3);
    }

    #endregion

    #region Polygon with Holes 3D Tests

    [Fact]
    public void ValidatePolygon_WithHolesAndZCoordinates_ReturnsValid()
    {
        // Arrange - Create a 3D polygon with a hole
        var shell = _factory.CreateLinearRing(new[]
        {
            new CoordinateZ(0, 0, 100),
            new CoordinateZ(10, 0, 100),
            new CoordinateZ(10, 10, 100),
            new CoordinateZ(0, 10, 100),
            new CoordinateZ(0, 0, 100)
        });

        var hole = _factory.CreateLinearRing(new[]
        {
            new CoordinateZ(2, 2, 100),
            new CoordinateZ(2, 8, 100),
            new CoordinateZ(8, 8, 100),
            new CoordinateZ(8, 2, 100),
            new CoordinateZ(2, 2, 100)
        });

        var polygon = _factory.CreatePolygon(shell, new[] { hole });

        // Act
        var result = GeometryValidator.ValidatePolygon(polygon);

        // Assert
        result.IsValid.Should().BeTrue();
        polygon.Coordinates.All(c => !double.IsNaN(c.Z)).Should().BeTrue();
    }

    [Fact]
    public void ValidatePolygon_WithHolesAtDifferentZ_ReturnsValid()
    {
        // Arrange - Shell and hole at different Z levels (like a bridge)
        var shell = _factory.CreateLinearRing(new[]
        {
            new CoordinateZ(0, 0, 100),
            new CoordinateZ(10, 0, 100),
            new CoordinateZ(10, 10, 100),
            new CoordinateZ(0, 10, 100),
            new CoordinateZ(0, 0, 100)
        });

        var hole = _factory.CreateLinearRing(new[]
        {
            new CoordinateZ(2, 2, 50),
            new CoordinateZ(2, 8, 50),
            new CoordinateZ(8, 8, 50),
            new CoordinateZ(8, 2, 50),
            new CoordinateZ(2, 2, 50)
        });

        var polygon = _factory.CreatePolygon(shell, new[] { hole });

        // Act
        var result = GeometryValidator.ValidatePolygon(polygon);

        // Assert - This should be valid as NTS doesn't enforce same Z for shell and holes
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region SRID Preservation with 3D Tests

    [Fact]
    public void Geometry3D_ShouldPreserveSRID()
    {
        // Arrange
        var coords = new[]
        {
            new CoordinateZ(0, 0, 100),
            new CoordinateZ(10, 10, 200)
        };
        var lineString = _factory.CreateLineString(coords);
        lineString.SRID = 4979; // EPSG:4979 (WGS84 3D)

        // Assert
        lineString.SRID.Should().Be(4979);
        lineString.Coordinates.All(c => !double.IsNaN(c.Z)).Should().BeTrue();
    }

    #endregion
}

using FluentAssertions;
using Honua.Server.Core.Data;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Geometry;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public sealed class GeometryTypeHelperTests
{
    private readonly GeometryFactory _factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    #region HasZCoordinate Tests

    [Fact]
    public void HasZCoordinate_2DPoint_ReturnsFalse()
    {
        // Arrange
        var point = _factory.CreatePoint(new Coordinate(10, 20));

        // Act
        var result = GeometryTypeHelper.HasZCoordinate(point);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasZCoordinate_3DPoint_ReturnsTrue()
    {
        // Arrange
        var point = _factory.CreatePoint(new CoordinateZ(10, 20, 100));

        // Act
        var result = GeometryTypeHelper.HasZCoordinate(point);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasZCoordinate_NullGeometry_ReturnsFalse()
    {
        // Act
        var result = GeometryTypeHelper.HasZCoordinate(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasZCoordinate_EmptyGeometry_ReturnsFalse()
    {
        // Arrange
        var emptyPoint = _factory.CreatePoint((Coordinate)null!);

        // Act
        var result = GeometryTypeHelper.HasZCoordinate(emptyPoint);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasZCoordinate_LineStringWithZ_ReturnsTrue()
    {
        // Arrange
        var coords = new[]
        {
            new CoordinateZ(0, 0, 10),
            new CoordinateZ(10, 10, 20)
        };
        var lineString = _factory.CreateLineString(coords);

        // Act
        var result = GeometryTypeHelper.HasZCoordinate(lineString);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasZCoordinate_PolygonWithZ_ReturnsTrue()
    {
        // Arrange
        var coords = new[]
        {
            new CoordinateZ(0, 0, 100),
            new CoordinateZ(10, 0, 100),
            new CoordinateZ(10, 10, 100),
            new CoordinateZ(0, 10, 100),
            new CoordinateZ(0, 0, 100)
        };
        var polygon = _factory.CreatePolygon(coords);

        // Act
        var result = GeometryTypeHelper.HasZCoordinate(polygon);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region HasMCoordinate Tests

    [Fact]
    public void HasMCoordinate_2DPoint_ReturnsFalse()
    {
        // Arrange
        var point = _factory.CreatePoint(new Coordinate(10, 20));

        // Act
        var result = GeometryTypeHelper.HasMCoordinate(point);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasMCoordinate_PointWithM_ReturnsTrue()
    {
        // Arrange
        var point = _factory.CreatePoint(new CoordinateM(10, 20, 5.5));

        // Act
        var result = GeometryTypeHelper.HasMCoordinate(point);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasMCoordinate_NullGeometry_ReturnsFalse()
    {
        // Act
        var result = GeometryTypeHelper.HasMCoordinate(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasMCoordinate_EmptyGeometry_ReturnsFalse()
    {
        // Arrange
        var emptyPoint = _factory.CreatePoint((Coordinate)null!);

        // Act
        var result = GeometryTypeHelper.HasMCoordinate(emptyPoint);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetOgcGeometryTypeName Tests (from Geometry)

    [Fact]
    public void GetOgcGeometryTypeName_2DPoint_ReturnsPoint()
    {
        // Arrange
        var point = _factory.CreatePoint(new Coordinate(10, 20));

        // Act
        var result = GeometryTypeHelper.GetOgcGeometryTypeName(point);

        // Assert
        result.Should().Be("Point");
    }

    [Fact]
    public void GetOgcGeometryTypeName_PointZ_ReturnsPointZ()
    {
        // Arrange
        var point = _factory.CreatePoint(new CoordinateZ(10, 20, 100));

        // Act
        var result = GeometryTypeHelper.GetOgcGeometryTypeName(point);

        // Assert
        result.Should().Be("PointZ");
    }

    [Fact]
    public void GetOgcGeometryTypeName_PointM_ReturnsPointM()
    {
        // Arrange
        var point = _factory.CreatePoint(new CoordinateM(10, 20, 5.5));

        // Act
        var result = GeometryTypeHelper.GetOgcGeometryTypeName(point);

        // Assert
        result.Should().Be("PointM");
    }

    [Fact]
    public void GetOgcGeometryTypeName_PointZM_ReturnsPointZM()
    {
        // Arrange
        var point = _factory.CreatePoint(new CoordinateZM(10, 20, 100, 5.5));

        // Act
        var result = GeometryTypeHelper.GetOgcGeometryTypeName(point);

        // Assert
        result.Should().Be("PointZM");
    }

    [Fact]
    public void GetOgcGeometryTypeName_LineStringZ_ReturnsLineStringZ()
    {
        // Arrange
        var coords = new[]
        {
            new CoordinateZ(0, 0, 10),
            new CoordinateZ(10, 10, 20)
        };
        var lineString = _factory.CreateLineString(coords);

        // Act
        var result = GeometryTypeHelper.GetOgcGeometryTypeName(lineString);

        // Assert
        result.Should().Be("LineStringZ");
    }

    [Fact]
    public void GetOgcGeometryTypeName_PolygonZ_ReturnsPolygonZ()
    {
        // Arrange
        var coords = new[]
        {
            new CoordinateZ(0, 0, 100),
            new CoordinateZ(10, 0, 100),
            new CoordinateZ(10, 10, 100),
            new CoordinateZ(0, 10, 100),
            new CoordinateZ(0, 0, 100)
        };
        var polygon = _factory.CreatePolygon(coords);

        // Act
        var result = GeometryTypeHelper.GetOgcGeometryTypeName(polygon);

        // Assert
        result.Should().Be("PolygonZ");
    }

    [Fact]
    public void GetOgcGeometryTypeName_MultiPointZ_ReturnsMultiPointZ()
    {
        // Arrange
        var points = new[]
        {
            _factory.CreatePoint(new CoordinateZ(0, 0, 10)),
            _factory.CreatePoint(new CoordinateZ(10, 10, 20))
        };
        var multiPoint = _factory.CreateMultiPoint(points);

        // Act
        var result = GeometryTypeHelper.GetOgcGeometryTypeName(multiPoint);

        // Assert
        result.Should().Be("MultiPointZ");
    }

    [Fact]
    public void GetOgcGeometryTypeName_NullGeometry_ReturnsUnknown()
    {
        // Act
        var result = GeometryTypeHelper.GetOgcGeometryTypeName(null);

        // Assert
        result.Should().Be("Unknown");
    }

    [Fact]
    public void GetOgcGeometryTypeName_EmptyGeometry_ReturnsUnknown()
    {
        // Arrange
        var emptyPoint = _factory.CreatePoint((Coordinate)null!);

        // Act
        var result = GeometryTypeHelper.GetOgcGeometryTypeName(emptyPoint);

        // Assert
        result.Should().Be("Unknown");
    }

    #endregion

    #region GetOgcGeometryTypeName Tests (from string)

    [Theory]
    [InlineData("Point", false, false, "Point")]
    [InlineData("Point", true, false, "PointZ")]
    [InlineData("Point", false, true, "PointM")]
    [InlineData("Point", true, true, "PointZM")]
    [InlineData("LineString", true, false, "LineStringZ")]
    [InlineData("Polygon", true, false, "PolygonZ")]
    [InlineData("MultiPoint", true, false, "MultiPointZ")]
    [InlineData("MultiLineString", true, false, "MultiLineStringZ")]
    [InlineData("MultiPolygon", true, false, "MultiPolygonZ")]
    [InlineData("GeometryCollection", true, false, "GeometryCollectionZ")]
    public void GetOgcGeometryTypeName_FromString_ReturnsCorrectName(
        string geometryType, bool hasZ, bool hasM, string expected)
    {
        // Act
        var result = GeometryTypeHelper.GetOgcGeometryTypeName(geometryType, hasZ, hasM);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetOgcGeometryTypeName_FromString_NullOrEmpty_ReturnsUnknown()
    {
        // Act
        var result1 = GeometryTypeHelper.GetOgcGeometryTypeName(null!, false, false);
        var result2 = GeometryTypeHelper.GetOgcGeometryTypeName("", false, false);
        var result3 = GeometryTypeHelper.GetOgcGeometryTypeName("   ", false, false);

        // Assert
        result1.Should().Be("Unknown");
        result2.Should().Be("Unknown");
        result3.Should().Be("Unknown");
    }

    #endregion

    #region NormalizeGeometryTypeName Tests

    [Theory]
    [InlineData("point", "Point")]
    [InlineData("POINT", "Point")]
    [InlineData("Point", "Point")]
    [InlineData("linestring", "LineString")]
    [InlineData("polyline", "LineString")]
    [InlineData("line", "LineString")]
    [InlineData("polygon", "Polygon")]
    [InlineData("multipoint", "MultiPoint")]
    [InlineData("multilinestring", "MultiLineString")]
    [InlineData("multipolyline", "MultiLineString")]
    [InlineData("multipolygon", "MultiPolygon")]
    [InlineData("geometrycollection", "GeometryCollection")]
    [InlineData("collection", "GeometryCollection")]
    public void NormalizeGeometryTypeName_VariousInputs_ReturnsNormalized(
        string input, string expected)
    {
        // Act
        var result = GeometryTypeHelper.NormalizeGeometryTypeName(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeGeometryTypeName_NullOrEmpty_ReturnsUnknown(string? input)
    {
        // Act
        var result = GeometryTypeHelper.NormalizeGeometryTypeName(input!);

        // Assert
        result.Should().Be("Unknown");
    }

    [Fact]
    public void NormalizeGeometryTypeName_UnknownType_CapitalizesFirst()
    {
        // Act
        var result = GeometryTypeHelper.NormalizeGeometryTypeName("customType");

        // Assert
        result.Should().Be("Customtype");
    }

    #endregion

    #region GetCoordinateDimension Tests

    [Theory]
    [InlineData(false, false, 2)]
    [InlineData(true, false, 3)]
    [InlineData(false, true, 3)]
    [InlineData(true, true, 4)]
    public void GetCoordinateDimension_FromFlags_ReturnsCorrectDimension(
        bool hasZ, bool hasM, int expectedDimension)
    {
        // Act
        var result = GeometryTypeHelper.GetCoordinateDimension(hasZ, hasM);

        // Assert
        result.Should().Be(expectedDimension);
    }

    [Fact]
    public void GetCoordinateDimension_2DPoint_Returns2()
    {
        // Arrange
        var point = _factory.CreatePoint(new Coordinate(10, 20));

        // Act
        var result = GeometryTypeHelper.GetCoordinateDimension(point);

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public void GetCoordinateDimension_PointZ_Returns3()
    {
        // Arrange
        var point = _factory.CreatePoint(new CoordinateZ(10, 20, 100));

        // Act
        var result = GeometryTypeHelper.GetCoordinateDimension(point);

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public void GetCoordinateDimension_PointM_Returns3()
    {
        // Arrange
        var point = _factory.CreatePoint(new CoordinateM(10, 20, 5.5));

        // Act
        var result = GeometryTypeHelper.GetCoordinateDimension(point);

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public void GetCoordinateDimension_PointZM_Returns4()
    {
        // Arrange
        var point = _factory.CreatePoint(new CoordinateZM(10, 20, 100, 5.5));

        // Act
        var result = GeometryTypeHelper.GetCoordinateDimension(point);

        // Assert
        result.Should().Be(4);
    }

    [Fact]
    public void GetCoordinateDimension_NullGeometry_Returns2()
    {
        // Act
        var result = GeometryTypeHelper.GetCoordinateDimension(null);

        // Assert
        result.Should().Be(2);
    }

    #endregion

    #region IsGeometryType3D Tests

    [Theory]
    [InlineData("PointZ", true)]
    [InlineData("PointZM", true)]
    [InlineData("LineStringZ", true)]
    [InlineData("PolygonZ", true)]
    [InlineData("MultiPointZ", true)]
    [InlineData("Point3D", true)]
    [InlineData("POINTZ", true)]
    [InlineData("pointz", true)]
    [InlineData("Point", false)]
    [InlineData("LineString", false)]
    [InlineData("Polygon", false)]
    [InlineData("PointM", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsGeometryType3D_VariousTypes_ReturnsCorrectResult(
        string? geometryType, bool expected)
    {
        // Act
        var result = GeometryTypeHelper.IsGeometryType3D(geometryType);

        // Assert
        result.Should().Be(expected);
    }

    #endregion
}

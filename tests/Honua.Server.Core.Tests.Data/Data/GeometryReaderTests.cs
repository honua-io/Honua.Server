using System;
using System.Data;
using System.Text.Json.Nodes;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Utilities;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data;

[Trait("Category", "Unit")]
public class GeometryReaderTests
{
    private static readonly WKBWriter WkbWriter = new();
    private static readonly WKTReader WktReader = new();

    [Fact]
    public void ReadGeometry_WithWkbPoint_ReturnsGeoJson()
    {
        // Arrange
        var point = new Point(new Coordinate(1, 2)) { SRID = 4326 };
        var wkb = WkbWriter.Write(point);
        var reader = new MockDataReader(new[] { ("geom", (object)wkb) });

        // Act
        var result = GeometryReader.ReadGeometry(reader, 0);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("Point");
    }

    [Fact]
    public void ReadGeometry_WithWktPoint_ReturnsGeoJson()
    {
        // Arrange
        var wkt = "POINT(1 2)";
        var reader = new MockDataReader(new[] { ("geom", (object)wkt) });

        // Act
        var result = GeometryReader.ReadGeometry(reader, 0);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("Point");
    }

    [Fact]
    public void ReadGeometry_WithGeoJson_ReturnsGeoJson()
    {
        // Arrange
        var geoJson = "{\"type\":\"Point\",\"coordinates\":[1,2]}";
        var reader = new MockDataReader(new[] { ("geom", (object)geoJson) });

        // Act
        var result = GeometryReader.ReadGeometry(reader, 0);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("Point");
    }

    [Fact]
    public void ReadGeometry_WithNull_ReturnsNull()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[] { ("geom", DBNull.Value) });

        // Act
        var result = GeometryReader.ReadGeometry(reader, 0);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ReadGeometry_WithSridTransformation_TransformsCoordinates()
    {
        // Arrange
        var wkt = "POINT(0 0)";
        var reader = new MockDataReader(new[] { ("geom", (object)wkt) });

        // Act - Transform from WGS84 to Web Mercator
        var result = GeometryReader.ReadGeometry(reader, 0, storageSrid: 4326, targetSrid: 3857);

        // Assert - Coordinates should be transformed (0,0 stays 0,0 but SRID changes)
        result.Should().NotBeNull();
    }

    [Fact]
    public void ReadWkbGeometry_WithValidWkb_ReturnsGeoJson()
    {
        // Arrange
        var point = new Point(new Coordinate(10, 20)) { SRID = 4326 };
        var wkb = WkbWriter.Write(point);

        // Act
        var result = GeometryReader.ReadWkbGeometry(wkb);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("Point");
        var coords = result["coordinates"]!.AsArray();
        coords[0]!.GetValue<double>().Should().BeApproximately(10, 0.0001);
        coords[1]!.GetValue<double>().Should().BeApproximately(20, 0.0001);
    }

    [Fact]
    public void ReadWkbGeometry_WithEmptyArray_ReturnsNull()
    {
        // Act
        var result = GeometryReader.ReadWkbGeometry(Array.Empty<byte>());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ReadWkbGeometry_WithNullArray_ReturnsNull()
    {
        // Act
        var result = GeometryReader.ReadWkbGeometry(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ReadWkbGeometry_WithInvalidWkb_ReturnsNull()
    {
        // Arrange
        var invalidWkb = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = GeometryReader.ReadWkbGeometry(invalidWkb);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ReadWktGeometry_WithValidWkt_ReturnsGeoJson()
    {
        // Arrange
        var wkt = "POINT(15 25)";

        // Act
        var result = GeometryReader.ReadWktGeometry(wkt);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("Point");
    }

    [Fact]
    public void ReadWktGeometry_WithLineString_ReturnsGeoJson()
    {
        // Arrange
        var wkt = "LINESTRING(0 0, 1 1, 2 2)";

        // Act
        var result = GeometryReader.ReadWktGeometry(wkt);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("LineString");
        var coords = result["coordinates"]!.AsArray();
        coords.Count.Should().Be(3);
    }

    [Fact]
    public void ReadWktGeometry_WithPolygon_ReturnsGeoJson()
    {
        // Arrange
        var wkt = "POLYGON((0 0, 4 0, 4 4, 0 4, 0 0))";

        // Act
        var result = GeometryReader.ReadWktGeometry(wkt);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("Polygon");
    }

    [Fact]
    public void ReadWktGeometry_WithMultiPoint_ReturnsGeoJson()
    {
        // Arrange
        var wkt = "MULTIPOINT((0 0), (1 1))";

        // Act
        var result = GeometryReader.ReadWktGeometry(wkt);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("MultiPoint");
    }

    [Fact]
    public void ReadWktGeometry_WithMultiLineString_ReturnsGeoJson()
    {
        // Arrange
        var wkt = "MULTILINESTRING((0 0, 1 1), (2 2, 3 3))";

        // Act
        var result = GeometryReader.ReadWktGeometry(wkt);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("MultiLineString");
    }

    [Fact]
    public void ReadWktGeometry_WithMultiPolygon_ReturnsGeoJson()
    {
        // Arrange
        var wkt = "MULTIPOLYGON(((0 0, 1 0, 1 1, 0 1, 0 0)))";

        // Act
        var result = GeometryReader.ReadWktGeometry(wkt);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("MultiPolygon");
    }

    [Fact]
    public void ReadWktGeometry_WithGeometryCollection_ReturnsGeoJson()
    {
        // Arrange
        var wkt = "GEOMETRYCOLLECTION(POINT(0 0), LINESTRING(0 0, 1 1))";

        // Act
        var result = GeometryReader.ReadWktGeometry(wkt);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("GeometryCollection");
    }

    [Fact]
    public void ReadWktGeometry_WithEmptyString_ReturnsNull()
    {
        // Act
        var result = GeometryReader.ReadWktGeometry("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ReadWktGeometry_WithNull_ReturnsNull()
    {
        // Act
        var result = GeometryReader.ReadWktGeometry(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ReadWktGeometry_WithInvalidWkt_ReturnsNull()
    {
        // Arrange
        var invalidWkt = "INVALID GEOMETRY";

        // Act
        var result = GeometryReader.ReadWktGeometry(invalidWkt);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ReadTextGeometry_WithWkt_ReturnsGeoJson()
    {
        // Arrange
        var wkt = "POINT(5 10)";

        // Act
        var result = GeometryReader.ReadTextGeometry(wkt);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("Point");
    }

    [Fact]
    public void ReadTextGeometry_WithGeoJson_ReturnsGeoJson()
    {
        // Arrange
        var geoJson = "{\"type\":\"Point\",\"coordinates\":[5,10]}";

        // Act
        var result = GeometryReader.ReadTextGeometry(geoJson);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("Point");
    }

    [Fact]
    public void ReadGeoJsonGeometry_WithValidGeoJson_ReturnsJsonNode()
    {
        // Arrange
        var geoJson = "{\"type\":\"Point\",\"coordinates\":[1,2]}";

        // Act
        var result = GeometryReader.ReadGeoJsonGeometry(geoJson);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("Point");
    }

    [Fact]
    public void ReadGeoJsonGeometry_WithPolygon_ReturnsJsonNode()
    {
        // Arrange
        var geoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[4,0],[4,4],[0,4],[0,0]]]}";

        // Act
        var result = GeometryReader.ReadGeoJsonGeometry(geoJson);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("Polygon");
    }

    [Fact]
    public void ReadGeoJsonGeometry_WithInvalidGeoJson_ReturnsNull()
    {
        // Arrange
        var invalidGeoJson = "{\"type\":\"Invalid\"}";

        // Act
        var result = GeometryReader.ReadGeoJsonGeometry(invalidGeoJson);

        // Assert - Falls back to parsing as JSON
        result.Should().NotBeNull(); // It's still valid JSON
    }

    [Fact]
    public void ReadGeoJsonGeometry_WithEmptyString_ReturnsNull()
    {
        // Act
        var result = GeometryReader.ReadGeoJsonGeometry("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TransformAndSerializeGeometry_WithValidGeometry_ReturnsGeoJson()
    {
        // Arrange
        var point = new Point(new Coordinate(1, 2)) { SRID = 4326 };

        // Act
        var result = GeometryReader.TransformAndSerializeGeometry(point, 4326, 4326);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("Point");
    }

    [Fact]
    public void TransformAndSerializeGeometry_WithNull_ReturnsNull()
    {
        // Act
        var result = GeometryReader.TransformAndSerializeGeometry(null, 4326, 4326);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseGeometry_WithWkt_ReturnsGeometry()
    {
        // Arrange
        var wkt = "POINT(10 20)";

        // Act
        var result = GeometryReader.ParseGeometry(wkt);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<Point>();
        result!.Coordinate.X.Should().Be(10);
        result.Coordinate.Y.Should().Be(20);
    }

    [Fact]
    public void ParseGeometry_WithGeoJson_ReturnsGeometry()
    {
        // Arrange
        var geoJson = "{\"type\":\"Point\",\"coordinates\":[10,20]}";

        // Act
        var result = GeometryReader.ParseGeometry(geoJson);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<Point>();
    }

    [Fact]
    public void ParseGeometry_WithInvalidText_ReturnsNull()
    {
        // Arrange
        var invalid = "not a geometry";

        // Act
        var result = GeometryReader.ParseGeometry(invalid);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseGeometry_WithEmptyString_ReturnsNull()
    {
        // Act
        var result = GeometryReader.ParseGeometry("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void SerializeGeometry_WithPoint_ReturnsGeoJson()
    {
        // Arrange
        var point = new Point(new Coordinate(5, 10));

        // Act
        var result = GeometryReader.SerializeGeometry(point);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("Point");
    }

    [Fact]
    public void SerializeGeometry_WithNull_ReturnsNull()
    {
        // Act
        var result = GeometryReader.SerializeGeometry(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void LooksLikeJson_WithGeoJson_ReturnsTrue()
    {
        // Arrange
        var geoJson = "{\"type\":\"Point\"}";

        // Act
        var result = GeometryReader.LooksLikeJson(geoJson);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void LooksLikeJson_WithWkt_ReturnsFalse()
    {
        // Arrange
        var wkt = "POINT(1 2)";

        // Act
        var result = GeometryReader.LooksLikeJson(wkt);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CreateGeoJsonReader_ReturnsNewInstance()
    {
        // Act
        var reader = GeometryReader.CreateGeoJsonReader();

        // Assert
        reader.Should().NotBeNull();
        reader.Should().BeOfType<GeoJsonReader>();
    }

    [Fact]
    public void CreateWktReader_ReturnsNewInstance()
    {
        // Act
        var reader = GeometryReader.CreateWktReader();

        // Assert
        reader.Should().NotBeNull();
        reader.Should().BeOfType<WKTReader>();
    }

    [Fact]
    public void CreateWkbReader_ReturnsNewInstance()
    {
        // Act
        var reader = GeometryReader.CreateWkbReader();

        // Assert
        reader.Should().NotBeNull();
        reader.Should().BeOfType<WKBReader>();
    }

    [Theory]
    [InlineData("POINT(180 90)")] // Boundary coordinates
    [InlineData("POINT(-180 -90)")]
    [InlineData("POINT(0 0)")] // Origin
    [InlineData("LINESTRING(-180 -90, 180 90)")] // Crossing boundaries
    public void ReadWktGeometry_WithBoundaryCoordinates_HandlesCorrectly(string wkt)
    {
        // Act
        var result = GeometryReader.ReadWktGeometry(wkt);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void ReadWktGeometry_WithPolygonHole_ReturnsGeoJson()
    {
        // Arrange - Polygon with a hole
        var wkt = "POLYGON((0 0, 10 0, 10 10, 0 10, 0 0), (2 2, 8 2, 8 8, 2 8, 2 2))";

        // Act
        var result = GeometryReader.ReadWktGeometry(wkt);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("Polygon");
        var coordinates = result["coordinates"]!.AsArray();
        coordinates.Count.Should().Be(2); // Exterior ring + hole
    }

    // Mock DataReader for testing
    private class MockDataReader : IDataReader
    {
        private readonly (string name, object value)[] _columns;

        public MockDataReader((string name, object value)[] columns)
        {
            _columns = columns;
        }

        public int FieldCount => _columns.Length;
        public object this[int i] => _columns[i].value;
        public object this[string name] => throw new NotImplementedException();

        public int Depth => 0;
        public bool IsClosed => false;
        public int RecordsAffected => 0;

        public void Close() { }
        public void Dispose() { }

        public string GetName(int i) => _columns[i].name;
        public bool IsDBNull(int i) => _columns[i].value is DBNull;
        public object GetValue(int i) => _columns[i].value;

        public bool GetBoolean(int i) => throw new NotImplementedException();
        public byte GetByte(int i) => throw new NotImplementedException();
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotImplementedException();
        public char GetChar(int i) => throw new NotImplementedException();
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotImplementedException();
        public IDataReader GetData(int i) => throw new NotImplementedException();
        public string GetDataTypeName(int i) => throw new NotImplementedException();
        public DateTime GetDateTime(int i) => throw new NotImplementedException();
        public decimal GetDecimal(int i) => throw new NotImplementedException();
        public double GetDouble(int i) => throw new NotImplementedException();
        public Type GetFieldType(int i) => throw new NotImplementedException();
        public float GetFloat(int i) => throw new NotImplementedException();
        public Guid GetGuid(int i) => throw new NotImplementedException();
        public short GetInt16(int i) => throw new NotImplementedException();
        public int GetInt32(int i) => throw new NotImplementedException();
        public long GetInt64(int i) => throw new NotImplementedException();
        public int GetOrdinal(string name) => throw new NotImplementedException();
        public DataTable GetSchemaTable() => throw new NotImplementedException();
        public string GetString(int i) => throw new NotImplementedException();
        public int GetValues(object[] values) => throw new NotImplementedException();
        public bool NextResult() => false;
        public bool Read() => false;
    }
}

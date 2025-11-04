using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json.Nodes;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data;

[Trait("Category", "Unit")]
public class FeatureRecordReaderTests
{
    [Fact]
    public void ReadFeatureRecord_WithAllFields_ReadsAllAttributes()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("id", 1),
            ("name", "test"),
            ("active", true)
        });

        var layer = CreateTestLayer();

        // Act
        var record = FeatureRecordReader.ReadFeatureRecord(reader, layer);

        // Assert
        record.Attributes.Should().HaveCount(4); // 3 fields + geometry
        record.Attributes["id"].Should().Be(1);
        record.Attributes["name"].Should().Be("test");
        record.Attributes["active"].Should().Be(true);
    }

    [Fact]
    public void ReadFeatureRecord_WithNullValues_HandlesNullCorrectly()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("id", 1),
            ("name", DBNull.Value),
            ("active", DBNull.Value)
        });

        var layer = CreateTestLayer();

        // Act
        var record = FeatureRecordReader.ReadFeatureRecord(reader, layer);

        // Assert
        record.Attributes["id"].Should().Be(1);
        record.Attributes["name"].Should().BeNull();
        record.Attributes["active"].Should().BeNull();
    }

    [Fact]
    public void ReadFeatureRecord_WithGeometryReader_ReadsGeometry()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("id", 1),
            ("geom", "POINT(1 2)")
        });

        var layer = CreateTestLayer();
        var geometryNode = JsonNode.Parse("{\"type\":\"Point\",\"coordinates\":[1,2]}");
        JsonNode? GeometryReader(IDataReader r, string col) => geometryNode;

        // Act
        var record = FeatureRecordReader.ReadFeatureRecord(reader, layer, geometryReader: GeometryReader);

        // Assert
        record.Attributes["geom"].Should().NotBeNull();
        record.Attributes["geom"].Should().Be(geometryNode);
    }

    [Fact]
    public void ReadFeatureRecord_WithoutGeometryReader_SkipsGeometry()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("id", 1),
            ("geom", "POINT(1 2)")
        });

        var layer = CreateTestLayer();

        // Act
        var record = FeatureRecordReader.ReadFeatureRecord(reader, layer);

        // Assert
        record.Attributes["geom"].Should().BeNull();
    }

    [Fact]
    public void ReadFeatureRecord_WithCustomGeometryFieldName_UsesCustomName()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("id", 1),
            ("custom_geom", "POINT(1 2)")
        });

        var layer = CreateTestLayer();
        var geometryNode = JsonNode.Parse("{\"type\":\"Point\",\"coordinates\":[1,2]}");
        JsonNode? GeometryReader(IDataReader r, string col) => geometryNode;

        // Act
        var record = FeatureRecordReader.ReadFeatureRecord(
            reader,
            layer,
            geometryFieldName: "custom_geom",
            geometryReader: GeometryReader);

        // Assert
        record.Attributes["custom_geom"].Should().NotBeNull();
    }

    [Fact]
    public void ReadFeatureRecord_WithNullReader_ThrowsArgumentNullException()
    {
        // Arrange
        IDataReader? reader = null;
        var layer = CreateTestLayer();

        // Act
        var act = () => FeatureRecordReader.ReadFeatureRecord(reader!, layer);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("reader");
    }

    [Fact]
    public void ReadFeatureRecord_WithNullLayer_ThrowsArgumentNullException()
    {
        // Arrange
        var reader = new MockDataReader(Array.Empty<(string, object)>());
        LayerDefinition? layer = null;

        // Act
        var act = () => FeatureRecordReader.ReadFeatureRecord(reader, layer!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("layer");
    }

    [Fact]
    public void ReadFeatureRecordWithCustomGeometry_SkipsSpecifiedColumns()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("id", 1),
            ("geom", "POINT(1 2)"),
            ("geom_srid", 4326)
        });

        var layer = CreateTestLayer();
        var skipColumns = new HashSet<string> { "geom", "geom_srid" };
        var geometryNode = JsonNode.Parse("{\"type\":\"Point\",\"coordinates\":[1,2]}");

        // Act
        var record = FeatureRecordReader.ReadFeatureRecordWithCustomGeometry(
            reader,
            layer,
            "geom",
            skipColumns,
            () => geometryNode);

        // Assert
        record.Attributes.Should().HaveCount(2); // id + geom (geom_srid skipped)
        record.Attributes["id"].Should().Be(1);
        record.Attributes["geom"].Should().NotBeNull();
        record.Attributes.Should().NotContainKey("geom_srid");
    }

    [Fact]
    public void ReadAttributes_WithFieldNames_ReadsSpecifiedFields()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("count", 10),
            ("sum", 100.5),
            ("avg", 10.05)
        });

        var fieldNames = new[] { "count", "sum", "avg" };

        // Act
        var attributes = FeatureRecordReader.ReadAttributes(reader, fieldNames);

        // Assert
        attributes.Should().HaveCount(3);
        attributes["count"].Should().Be(10);
        attributes["sum"].Should().Be(100.5);
        attributes["avg"].Should().Be(10.05);
    }

    [Fact]
    public void ReadAttributes_WithMoreFieldsThanColumns_StopsAtColumnCount()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("col1", 1),
            ("col2", 2)
        });

        var fieldNames = new[] { "field1", "field2", "field3", "field4" };

        // Act
        var attributes = FeatureRecordReader.ReadAttributes(reader, fieldNames);

        // Assert
        attributes.Should().HaveCount(2);
    }

    [Fact]
    public void GetFieldValue_WithValidOrdinal_ReturnsTypedValue()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("id", 123)
        });

        // Act
        var result = FeatureRecordReader.GetFieldValue<int>(reader, 0);

        // Assert
        result.Should().Be(123);
    }

    [Fact]
    public void GetFieldValue_WithNullValue_ReturnsDefault()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("value", DBNull.Value)
        });

        // Act
        var result = FeatureRecordReader.GetFieldValue<string>(reader, 0, "default");

        // Assert
        result.Should().Be("default");
    }

    [Fact]
    public void GetFieldValue_WithConversion_ConvertsType()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("value", 123L)
        });

        // Act
        var result = FeatureRecordReader.GetFieldValue<int>(reader, 0);

        // Assert
        result.Should().Be(123);
    }

    [Fact]
    public void GetFieldValue_Untyped_ReturnsValue()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("value", "test")
        });

        // Act
        var result = FeatureRecordReader.GetFieldValue(reader, 0);

        // Assert
        result.Should().Be("test");
    }

    [Fact]
    public void GetFieldValue_Untyped_WithDBNull_ReturnsNull()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("value", DBNull.Value)
        });

        // Act
        var result = FeatureRecordReader.GetFieldValue(reader, 0);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetString_WithStringValue_ReturnsString()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("name", "test")
        });

        // Act
        var result = FeatureRecordReader.GetString(reader, 0);

        // Assert
        result.Should().Be("test");
    }

    [Fact]
    public void GetString_WithNull_ReturnsNull()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("name", DBNull.Value)
        });

        // Act
        var result = FeatureRecordReader.GetString(reader, 0);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetInt32_WithIntValue_ReturnsInt()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("id", 42)
        });

        // Act
        var result = FeatureRecordReader.GetInt32(reader, 0);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void GetInt32_WithNull_ReturnsDefaultValue()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("id", DBNull.Value)
        });

        // Act
        var result = FeatureRecordReader.GetInt32(reader, 0, -1);

        // Assert
        result.Should().Be(-1);
    }

    [Fact]
    public void TryGetGeometryOrdinal_WithExistingColumn_ReturnsOrdinal()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("id", 1),
            ("geom", "POINT(1 2)"),
            ("name", "test")
        });

        // Act
        var ordinal = FeatureRecordReader.TryGetGeometryOrdinal(reader, "geom");

        // Assert
        ordinal.Should().Be(1);
    }

    [Fact]
    public void TryGetGeometryOrdinal_WithNonExistingColumn_ReturnsMinusOne()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("id", 1),
            ("name", "test")
        });

        // Act
        var ordinal = FeatureRecordReader.TryGetGeometryOrdinal(reader, "geom");

        // Assert
        ordinal.Should().Be(-1);
    }

    [Fact]
    public void TryGetGeometryOrdinal_WithNullFieldName_ReturnsMinusOne()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("id", 1)
        });

        // Act
        var ordinal = FeatureRecordReader.TryGetGeometryOrdinal(reader, null);

        // Assert
        ordinal.Should().Be(-1);
    }

    [Fact]
    public void HasColumn_WithExistingColumn_ReturnsTrue()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("id", 1),
            ("name", "test")
        });

        // Act
        var result = FeatureRecordReader.HasColumn(reader, "name");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasColumn_WithNonExistingColumn_ReturnsFalse()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("id", 1)
        });

        // Act
        var result = FeatureRecordReader.HasColumn(reader, "name");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasColumn_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("Name", "test")
        });

        // Act
        var result = FeatureRecordReader.HasColumn(reader, "name");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GetColumnNames_ReturnsAllColumnNames()
    {
        // Arrange
        var reader = new MockDataReader(new (string name, object value)[]
        {
            ("id", 1),
            ("name", "test"),
            ("active", true)
        });

        // Act
        var columns = FeatureRecordReader.GetColumnNames(reader);

        // Assert
        columns.Should().HaveCount(3);
        columns.Should().Contain("id");
        columns.Should().Contain("name");
        columns.Should().Contain("active");
    }

    private LayerDefinition CreateTestLayer()
    {
        return new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Fields = new[]
            {
                new FieldDefinition { Name = "id", DataType = "int" },
                new FieldDefinition { Name = "name", DataType = "string" },
                new FieldDefinition { Name = "active", DataType = "bool" }
            }
        };
    }

    // Mock DataReader for testing
    private class MockDataReader : IDataReader
    {
        private readonly (string name, object value)[] _columns;
        private bool _hasRead;

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

        public int GetOrdinal(string name)
        {
            for (var i = 0; i < _columns.Length; i++)
            {
                if (string.Equals(_columns[i].name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        public bool IsDBNull(int i) => _columns[i].value is DBNull;

        public object GetValue(int i) => _columns[i].value;

        public bool GetBoolean(int i) => (bool)_columns[i].value;
        public byte GetByte(int i) => (byte)_columns[i].value;
        public char GetChar(int i) => (char)_columns[i].value;
        public DateTime GetDateTime(int i) => (DateTime)_columns[i].value;
        public decimal GetDecimal(int i) => (decimal)_columns[i].value;
        public double GetDouble(int i) => (double)_columns[i].value;
        public float GetFloat(int i) => (float)_columns[i].value;
        public Guid GetGuid(int i) => (Guid)_columns[i].value;
        public short GetInt16(int i) => (short)_columns[i].value;
        public int GetInt32(int i) => (int)_columns[i].value;
        public long GetInt64(int i) => (long)_columns[i].value;
        public string GetString(int i) => (string)_columns[i].value;

        public bool Read()
        {
            if (_hasRead) return false;
            _hasRead = true;
            return true;
        }

        public bool NextResult() => false;

        public string GetDataTypeName(int i) => _columns[i].value.GetType().Name;
        public Type GetFieldType(int i) => _columns[i].value.GetType();

        public int GetValues(object[] values)
        {
            for (var i = 0; i < Math.Min(values.Length, _columns.Length); i++)
            {
                values[i] = _columns[i].value;
            }
            return Math.Min(values.Length, _columns.Length);
        }

        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length)
            => throw new NotImplementedException();

        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length)
            => throw new NotImplementedException();

        public IDataReader GetData(int i) => throw new NotImplementedException();
        public DataTable GetSchemaTable() => throw new NotImplementedException();
    }
}

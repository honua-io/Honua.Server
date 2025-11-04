using System;
using FluentAssertions;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data.Query;

[Trait("Category", "Unit")]
public class LayerMetadataHelperTests
{
    [Fact]
    public void GetPrimaryKeyColumn_WithStoragePrimaryKey_ReturnsStorageValue()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Storage = new LayerStorageDefinition
            {
                PrimaryKey = "custom_pk"
            }
        };

        // Act
        var result = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        // Assert
        result.Should().Be("custom_pk");
    }

    [Fact]
    public void GetPrimaryKeyColumn_WithoutStoragePrimaryKey_ReturnsIdField()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "feature_id"
        };

        // Act
        var result = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        // Assert
        result.Should().Be("feature_id");
    }

    [Fact]
    public void GetPrimaryKeyColumn_WithNullLayer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => LayerMetadataHelper.GetPrimaryKeyColumn(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("layer");
    }

    [Fact]
    public void GetGeometryColumn_WithStorageGeometryColumn_ReturnsStorageValue()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Storage = new LayerStorageDefinition
            {
                GeometryColumn = "the_geom"
            }
        };

        // Act
        var result = LayerMetadataHelper.GetGeometryColumn(layer);

        // Assert
        result.Should().Be("the_geom");
    }

    [Fact]
    public void GetGeometryColumn_WithoutStorageGeometryColumn_ReturnsGeometryField()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "shape",
            IdField = "id"
        };

        // Act
        var result = LayerMetadataHelper.GetGeometryColumn(layer);

        // Assert
        result.Should().Be("shape");
    }

    [Fact]
    public void GetGeometryColumn_WithNullLayer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => LayerMetadataHelper.GetGeometryColumn(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("layer");
    }

    [Fact]
    public void GetTableName_WithStorageTable_ReturnsStorageValue()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "layer_id",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Storage = new LayerStorageDefinition
            {
                Table = "custom_table"
            }
        };

        // Act
        var result = LayerMetadataHelper.GetTableName(layer);

        // Assert
        result.Should().Be("custom_table");
    }

    [Fact]
    public void GetTableName_WithoutStorageTable_ReturnsLayerId()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "my_layer",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id"
        };

        // Act
        var result = LayerMetadataHelper.GetTableName(layer);

        // Assert
        result.Should().Be("my_layer");
    }

    [Fact]
    public void GetTableName_WithNullLayer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => LayerMetadataHelper.GetTableName(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("layer");
    }

    [Fact]
    public void GetTableExpression_WithSimpleTableName_ReturnsQuotedName()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Storage = new LayerStorageDefinition { Table = "my_table" }
        };

        // Act
        var result = LayerMetadataHelper.GetTableExpression(layer, name => $"\"{name}\"");

        // Assert
        result.Should().Be("\"my_table\"");
    }

    [Fact]
    public void GetTableExpression_WithSchemaQualifiedName_ReturnsQuotedSchemaAndTable()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Storage = new LayerStorageDefinition { Table = "public.my_table" }
        };

        // Act
        var result = LayerMetadataHelper.GetTableExpression(layer, name => $"\"{name}\"");

        // Assert
        result.Should().Be("\"public\".\"my_table\"");
    }

    [Fact]
    public void GetTableExpression_WithDatabaseSchemaTable_ReturnsFullyQualified()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Storage = new LayerStorageDefinition { Table = "mydb.public.my_table" }
        };

        // Act
        var result = LayerMetadataHelper.GetTableExpression(layer, name => $"[{name}]");

        // Assert
        result.Should().Be("[mydb].[public].[my_table]");
    }

    [Fact]
    public void GetTableExpression_WithNullLayer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => LayerMetadataHelper.GetTableExpression(null!, name => name);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("layer");
    }

    [Fact]
    public void GetTableExpression_WithNullQuoteIdentifier_ThrowsArgumentNullException()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id"
        };

        // Act
        var act = () => LayerMetadataHelper.GetTableExpression(layer, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("quoteIdentifier");
    }

    [Fact]
    public void NormalizeKeyValue_WithIntType_ParsesInt()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Fields = new[]
            {
                new FieldDefinition { Name = "id", DataType = "int" }
            }
        };

        // Act
        var result = LayerMetadataHelper.NormalizeKeyValue("123", layer);

        // Assert
        result.Should().BeOfType<int>().Which.Should().Be(123);
    }

    [Fact]
    public void NormalizeKeyValue_WithLongType_ParsesLong()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Fields = new[]
            {
                new FieldDefinition { Name = "id", DataType = "bigint" }
            }
        };

        // Act
        var result = LayerMetadataHelper.NormalizeKeyValue("9223372036854775807", layer);

        // Assert
        result.Should().BeOfType<long>().Which.Should().Be(9223372036854775807L);
    }

    [Fact]
    public void NormalizeKeyValue_WithDoubleType_ParsesDouble()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Fields = new[]
            {
                new FieldDefinition { Name = "id", DataType = "double" }
            }
        };

        // Act
        var result = LayerMetadataHelper.NormalizeKeyValue("3.14159", layer);

        // Assert
        result.Should().BeOfType<double>().Which.Should().BeApproximately(3.14159, 0.00001);
    }

    [Fact]
    public void NormalizeKeyValue_WithDecimalType_ParsesDecimal()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Fields = new[]
            {
                new FieldDefinition { Name = "id", DataType = "decimal" }
            }
        };

        // Act
        var result = LayerMetadataHelper.NormalizeKeyValue("123.45", layer);

        // Assert
        result.Should().BeOfType<decimal>().Which.Should().Be(123.45m);
    }

    [Fact]
    public void NormalizeKeyValue_WithGuidType_ParsesGuid()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Fields = new[]
            {
                new FieldDefinition { Name = "id", DataType = "guid" }
            }
        };
        var guidString = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

        // Act
        var result = LayerMetadataHelper.NormalizeKeyValue(guidString, layer);

        // Assert
        result.Should().BeOfType<Guid>();
    }

    [Fact]
    public void NormalizeKeyValue_WithStringType_ReturnsString()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Fields = new[]
            {
                new FieldDefinition { Name = "id", DataType = "string" }
            }
        };

        // Act
        var result = LayerMetadataHelper.NormalizeKeyValue("test123", layer);

        // Assert
        result.Should().Be("test123");
    }

    [Fact]
    public void NormalizeKeyValue_WithInvalidInt_ReturnsOriginalString()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Fields = new[]
            {
                new FieldDefinition { Name = "id", DataType = "int" }
            }
        };

        // Act
        var result = LayerMetadataHelper.NormalizeKeyValue("not_a_number", layer);

        // Assert
        result.Should().Be("not_a_number");
    }

    [Fact]
    public void NormalizeKeyValue_WithNoDataTypeHint_ReturnsOriginalString()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Fields = new[]
            {
                new FieldDefinition { Name = "id" }
            }
        };

        // Act
        var result = LayerMetadataHelper.NormalizeKeyValue("123", layer);

        // Assert
        result.Should().Be("123");
    }

    [Fact]
    public void NormalizeKeyValue_WithNullFeatureId_ThrowsArgumentNullException()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id"
        };

        // Act
        var act = () => LayerMetadataHelper.NormalizeKeyValue(null!, layer);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("featureId");
    }

    [Fact]
    public void NormalizeKeyValue_WithNullLayer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => LayerMetadataHelper.NormalizeKeyValue("123", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("layer");
    }

    [Fact]
    public void GetSchemaName_WithSchemaQualifiedTable_ReturnsSchema()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Storage = new LayerStorageDefinition { Table = "public.my_table" }
        };

        // Act
        var result = LayerMetadataHelper.GetSchemaName(layer);

        // Assert
        result.Should().Be("public");
    }

    [Fact]
    public void GetSchemaName_WithSimpleTableName_ReturnsNull()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Storage = new LayerStorageDefinition { Table = "my_table" }
        };

        // Act
        var result = LayerMetadataHelper.GetSchemaName(layer);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetSchemaName_WithDatabaseSchemaTable_ReturnsFirstPart()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Storage = new LayerStorageDefinition { Table = "mydb.public.my_table" }
        };

        // Act
        var result = LayerMetadataHelper.GetSchemaName(layer);

        // Assert
        result.Should().Be("mydb");
    }

    [Fact]
    public void GetSchemaName_WithNullLayer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => LayerMetadataHelper.GetSchemaName(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("layer");
    }

    [Fact]
    public void GetFields_ReturnsLayerFields()
    {
        // Arrange
        var layer = new LayerDefinition
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
                new FieldDefinition { Name = "name", DataType = "string" }
            }
        };

        // Act
        var result = LayerMetadataHelper.GetFields(layer);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("id");
        result[1].Name.Should().Be("name");
    }

    [Fact]
    public void GetFields_WithNullLayer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => LayerMetadataHelper.GetFields(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("layer");
    }

    [Fact]
    public void HasField_WithExistingField_ReturnsTrue()
    {
        // Arrange
        var layer = new LayerDefinition
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
                new FieldDefinition { Name = "name", DataType = "string" }
            }
        };

        // Act
        var result = LayerMetadataHelper.HasField(layer, "name");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasField_WithNonExistingField_ReturnsFalse()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Fields = new[]
            {
                new FieldDefinition { Name = "id", DataType = "int" }
            }
        };

        // Act
        var result = LayerMetadataHelper.HasField(layer, "missing");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasField_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Fields = new[]
            {
                new FieldDefinition { Name = "Name", DataType = "string" }
            }
        };

        // Act
        var result = LayerMetadataHelper.HasField(layer, "name");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasField_WithNullLayer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => LayerMetadataHelper.HasField(null!, "field");

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("layer");
    }

    [Fact]
    public void HasField_WithNullFieldName_ThrowsArgumentNullException()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Fields = Array.Empty<FieldDefinition>()
        };

        // Act
        var act = () => LayerMetadataHelper.HasField(layer, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("fieldName");
    }

    [Theory]
    [InlineData("int", "42", typeof(int))]
    [InlineData("int32", "42", typeof(int))]
    [InlineData("integer", "42", typeof(int))]
    [InlineData("smallint", "42", typeof(int))]
    [InlineData("bigint", "9223372036854775807", typeof(long))]
    [InlineData("long", "9223372036854775807", typeof(long))]
    [InlineData("int64", "9223372036854775807", typeof(long))]
    public void NormalizeKeyValue_WithVariousIntTypes_ParsesCorrectly(string dataType, string value, Type expectedType)
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Fields = new[]
            {
                new FieldDefinition { Name = "id", DataType = dataType }
            }
        };

        // Act
        var result = LayerMetadataHelper.NormalizeKeyValue(value, layer);

        // Assert
        result.Should().BeOfType(expectedType);
    }

    [Theory]
    [InlineData("uuid")]
    [InlineData("uniqueidentifier")]
    public void NormalizeKeyValue_WithUuidTypes_ParsesGuid(string dataType)
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Fields = new[]
            {
                new FieldDefinition { Name = "id", DataType = dataType }
            }
        };
        var guidString = "550e8400-e29b-41d4-a716-446655440000";

        // Act
        var result = LayerMetadataHelper.NormalizeKeyValue(guidString, layer);

        // Assert
        result.Should().BeOfType<Guid>();
    }
}

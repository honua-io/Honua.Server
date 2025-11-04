using System;
using Honua.Server.Core.Import.Validation;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Import.Validation;

public sealed class TypeCoercionTests
{
    [Theory]
    [InlineData("123", "integer", 123L)]
    [InlineData("456", "bigint", 456L)]
    [InlineData(789, "integer", 789L)]
    [InlineData((short)100, "integer", 100L)]
    public void TryCoerce_ToInteger_Succeeds(object value, string targetType, long expected)
    {
        // Act
        var result = TypeCoercion.TryCoerce(value, targetType);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("123", "smallint", (short)123)]
    [InlineData(100, "smallint", (short)100)]
    [InlineData((byte)50, "smallint", (short)50)]
    public void TryCoerce_ToSmallInt_Succeeds(object value, string targetType, short expected)
    {
        // Act
        var result = TypeCoercion.TryCoerce(value, targetType);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("3.14", "float")]
    [InlineData("123.456", "double")]
    [InlineData(42, "float")]
    [InlineData(42.5, "double")]
    public void TryCoerce_ToFloatOrDouble_Succeeds(object value, string targetType)
    {
        // Act
        var result = TypeCoercion.TryCoerce(value, targetType);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Value);
    }

    [Theory]
    [InlineData("2024-01-01T12:00:00Z", "datetime")]
    [InlineData("2024-01-01", "timestamp")]
    public void TryCoerce_ToDateTime_FromString_Succeeds(string value, string targetType)
    {
        // Act
        var result = TypeCoercion.TryCoerce(value, targetType);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Value is DateTime or DateTimeOffset);
    }

    [Fact]
    public void TryCoerce_ToDateTime_FromUnixTimestamp_Succeeds()
    {
        // Arrange
        var unixTimestamp = 1672574400L; // 2023-01-01 12:00:00 UTC
        var targetType = "datetime";

        // Act
        var result = TypeCoercion.TryCoerce(unixTimestamp, targetType);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Value is DateTimeOffset);
    }

    [Theory]
    [InlineData("550e8400-e29b-41d4-a716-446655440000", "uuid")]
    [InlineData("550e8400e29b41d4a716446655440000", "uniqueidentifier")]
    public void TryCoerce_ToGuid_Succeeds(string value, string targetType)
    {
        // Act
        var result = TypeCoercion.TryCoerce(value, targetType);

        // Assert
        Assert.True(result.Success);
        Assert.IsType<Guid>(result.Value);
    }

    [Theory]
    [InlineData("true", "boolean", true)]
    [InlineData("false", "bool", false)]
    [InlineData("1", "boolean", true)]
    [InlineData("0", "boolean", false)]
    [InlineData("yes", "boolean", true)]
    [InlineData("no", "boolean", false)]
    [InlineData(1, "boolean", true)]
    [InlineData(0, "boolean", false)]
    public void TryCoerce_ToBoolean_Succeeds(object value, string targetType, bool expected)
    {
        // Act
        var result = TypeCoercion.TryCoerce(value, targetType);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData(123, "text", "123")]
    [InlineData(3.14, "varchar", "3.14")]
    [InlineData(true, "string", "True")]
    public void TryCoerce_ToString_Succeeds(object value, string targetType, string expected)
    {
        // Act
        var result = TypeCoercion.TryCoerce(value, targetType);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void TryCoerce_NullValue_ReturnsSuccessWithNull()
    {
        // Act
        var result = TypeCoercion.TryCoerce(null, "integer");

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Value);
    }

    [Fact]
    public void TryCoerce_InvalidInteger_Fails()
    {
        // Act
        var result = TypeCoercion.TryCoerce("not-a-number", "integer");

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void TryCoerce_InvalidDateTime_Fails()
    {
        // Act
        var result = TypeCoercion.TryCoerce("invalid-date", "datetime");

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void TryCoerce_IntegerOutOfSmallIntRange_Fails()
    {
        // Act
        var result = TypeCoercion.TryCoerce(100000, "smallint");

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void TryCoerce_ToByteArray_FromBase64_Succeeds()
    {
        // Arrange
        var base64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 });

        // Act
        var result = TypeCoercion.TryCoerce(base64, "blob");

        // Assert
        Assert.True(result.Success);
        Assert.IsType<byte[]>(result.Value);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, (byte[])result.Value!);
    }

    [Fact]
    public void TryCoerce_ToByteArray_FromByteArray_ReturnsIdentical()
    {
        // Arrange
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = TypeCoercion.TryCoerce(bytes, "bytea");

        // Assert
        Assert.True(result.Success);
        Assert.Same(bytes, result.Value);
    }
}

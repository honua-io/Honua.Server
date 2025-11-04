using System;
using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Honua.Server.Core.Data;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data;

[Trait("Category", "Unit")]
public class FeatureRecordNormalizerTests
{
    [Fact]
    public void NormalizeValue_WithNull_ReturnsNull()
    {
        // Act
        var result = FeatureRecordNormalizer.NormalizeValue(null, false);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NormalizeValue_WithString_ReturnsString()
    {
        // Arrange
        var value = "test string";

        // Act
        var result = FeatureRecordNormalizer.NormalizeValue(value, false);

        // Assert
        result.Should().Be("test string");
    }

    [Fact]
    public void NormalizeValue_WithJsonNode_ReturnsJsonString()
    {
        // Arrange
        var value = JsonNode.Parse("{\"type\":\"Point\",\"coordinates\":[1,2]}");

        // Act
        var result = FeatureRecordNormalizer.NormalizeValue(value, false);

        // Assert
        result.Should().BeOfType<string>();
        result.As<string>().Should().Contain("Point");
    }

    [Fact]
    public void NormalizeValue_WithJsonElement_ReturnsString()
    {
        // Arrange
        var doc = JsonDocument.Parse("{\"key\":\"value\"}");
        var element = doc.RootElement;

        // Act
        var result = FeatureRecordNormalizer.NormalizeValue(element, false);

        // Assert
        result.Should().BeOfType<string>();
    }

    [Fact]
    public void NormalizeValue_WithNullJsonElement_ReturnsNull()
    {
        // Arrange
        var doc = JsonDocument.Parse("null");
        var element = doc.RootElement;

        // Act
        var result = FeatureRecordNormalizer.NormalizeValue(element, false);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NormalizeValue_WithDateTime_ConvertsToUtc()
    {
        // Arrange
        var localTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Local);

        // Act
        var result = FeatureRecordNormalizer.NormalizeValue(localTime, false);

        // Assert
        result.Should().BeOfType<DateTime>();
        result.As<DateTime>().Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void NormalizeValue_WithUtcDateTime_RemainsUtc()
    {
        // Arrange
        var utcTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = FeatureRecordNormalizer.NormalizeValue(utcTime, false);

        // Assert
        result.Should().BeOfType<DateTime>();
        result.As<DateTime>().Should().Be(utcTime);
    }

    [Fact]
    public void NormalizeValue_WithDateTimeOffset_ConvertsToUtcDateTime()
    {
        // Arrange
        var offset = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.FromHours(-5));

        // Act
        var result = FeatureRecordNormalizer.NormalizeValue(offset, false);

        // Assert
        result.Should().BeOfType<DateTime>();
        result.As<DateTime>().Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void NormalizeGeometryValue_WithGeoJsonString_ReturnsString()
    {
        // Arrange
        var geoJson = "{\"type\":\"Point\",\"coordinates\":[1,2]}";

        // Act
        var result = FeatureRecordNormalizer.NormalizeGeometryValue(geoJson);

        // Assert
        result.Should().Be(geoJson);
    }

    [Fact]
    public void NormalizeGeometryValue_WithJsonNode_ReturnsJsonString()
    {
        // Arrange
        var node = JsonNode.Parse("{\"type\":\"Point\",\"coordinates\":[1,2]}");

        // Act
        var result = FeatureRecordNormalizer.NormalizeGeometryValue(node);

        // Assert
        result.Should().BeOfType<string>();
    }

    [Fact]
    public void NormalizeGeometryValue_WithNull_ReturnsNull()
    {
        // Act
        var result = FeatureRecordNormalizer.NormalizeGeometryValue(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NormalizeBooleanValue_WithTrueAndNumeric_ReturnsOne()
    {
        // Act
        var result = FeatureRecordNormalizer.NormalizeBooleanValue(true, useNumericBoolean: true);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public void NormalizeBooleanValue_WithFalseAndNumeric_ReturnsZero()
    {
        // Act
        var result = FeatureRecordNormalizer.NormalizeBooleanValue(false, useNumericBoolean: true);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void NormalizeBooleanValue_WithTrueAndNonNumeric_ReturnsTrue()
    {
        // Act
        var result = FeatureRecordNormalizer.NormalizeBooleanValue(true, useNumericBoolean: false);

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void NormalizeDateTimeValue_WithLocalTime_ConvertsToUtc()
    {
        // Arrange
        var localTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Local);

        // Act
        var result = FeatureRecordNormalizer.NormalizeDateTimeValue(localTime);

        // Assert
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void NormalizeDateTimeOffsetValue_ConvertsToUtcDateTime()
    {
        // Arrange
        var offset = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.FromHours(2));

        // Act
        var result = FeatureRecordNormalizer.NormalizeDateTimeOffsetValue(offset);

        // Assert
        result.Kind.Should().Be(DateTimeKind.Utc);
        result.Should().Be(offset.UtcDateTime);
    }

    [Fact]
    public void ParseGeometry_WithValidGeoJson_ReturnsJsonNode()
    {
        // Arrange
        var geoJson = "{\"type\":\"Point\",\"coordinates\":[1,2]}";

        // Act
        var result = FeatureRecordNormalizer.ParseGeometry(geoJson);

        // Assert
        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("Point");
    }

    [Fact]
    public void ParseGeometry_WithInvalidJson_ReturnsNull()
    {
        // Arrange
        var invalidJson = "{invalid json";

        // Act
        var result = FeatureRecordNormalizer.ParseGeometry(invalidJson);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseGeometry_WithEmptyString_ReturnsNull()
    {
        // Act
        var result = FeatureRecordNormalizer.ParseGeometry("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseGeometry_WithNull_ReturnsNull()
    {
        // Act
        var result = FeatureRecordNormalizer.ParseGeometry(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void LooksLikeJson_WithObjectStart_ReturnsTrue()
    {
        // Arrange
        var text = "{\"key\":\"value\"}";

        // Act
        var result = FeatureRecordNormalizer.LooksLikeJson(text);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void LooksLikeJson_WithArrayStart_ReturnsTrue()
    {
        // Arrange
        var text = "[1,2,3]";

        // Act
        var result = FeatureRecordNormalizer.LooksLikeJson(text);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void LooksLikeJson_WithWhitespace_ReturnsTrue()
    {
        // Arrange
        var text = "  {\"key\":\"value\"}  ";

        // Act
        var result = FeatureRecordNormalizer.LooksLikeJson(text);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void LooksLikeJson_WithPlainText_ReturnsFalse()
    {
        // Arrange
        var text = "POINT(1 2)";

        // Act
        var result = FeatureRecordNormalizer.LooksLikeJson(text);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void LooksLikeJson_WithEmptyString_ReturnsFalse()
    {
        // Act
        var result = FeatureRecordNormalizer.LooksLikeJson("");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void LooksLikeJson_WithNull_ReturnsFalse()
    {
        // Act
        var result = FeatureRecordNormalizer.LooksLikeJson(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void NormalizeNullValue_WithNull_ReturnsDBNull()
    {
        // Act
        var result = FeatureRecordNormalizer.NormalizeNullValue(null);

        // Assert
        result.Should().Be(DBNull.Value);
    }

    [Fact]
    public void NormalizeNullValue_WithValue_ReturnsValue()
    {
        // Arrange
        var value = "test";

        // Act
        var result = FeatureRecordNormalizer.NormalizeNullValue(value);

        // Assert
        result.Should().Be("test");
    }

    [Fact]
    public void IsNullOrDbNull_WithNull_ReturnsTrue()
    {
        // Act
        var result = FeatureRecordNormalizer.IsNullOrDbNull(null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsNullOrDbNull_WithDBNull_ReturnsTrue()
    {
        // Act
        var result = FeatureRecordNormalizer.IsNullOrDbNull(DBNull.Value);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsNullOrDbNull_WithValue_ReturnsFalse()
    {
        // Act
        var result = FeatureRecordNormalizer.IsNullOrDbNull("test");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ConvertJsonElement_WithNull_ReturnsNull()
    {
        // Arrange
        var doc = JsonDocument.Parse("null");
        var element = doc.RootElement;

        // Act
        var result = FeatureRecordNormalizer.ConvertJsonElement(element);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ConvertJsonElement_WithTrue_ReturnsTrue()
    {
        // Arrange
        var doc = JsonDocument.Parse("true");
        var element = doc.RootElement;

        // Act
        var result = FeatureRecordNormalizer.ConvertJsonElement(element);

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void ConvertJsonElement_WithFalse_ReturnsFalse()
    {
        // Arrange
        var doc = JsonDocument.Parse("false");
        var element = doc.RootElement;

        // Act
        var result = FeatureRecordNormalizer.ConvertJsonElement(element);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void ConvertJsonElement_WithInt32_ReturnsInt()
    {
        // Arrange
        var doc = JsonDocument.Parse("42");
        var element = doc.RootElement;

        // Act
        var result = FeatureRecordNormalizer.ConvertJsonElement(element);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void ConvertJsonElement_WithInt64_ReturnsLong()
    {
        // Arrange
        var doc = JsonDocument.Parse("9223372036854775807");
        var element = doc.RootElement;

        // Act
        var result = FeatureRecordNormalizer.ConvertJsonElement(element);

        // Assert
        result.Should().Be(9223372036854775807L);
    }

    [Fact]
    public void ConvertJsonElement_WithDouble_ReturnsDouble()
    {
        // Arrange
        var doc = JsonDocument.Parse("3.14159");
        var element = doc.RootElement;

        // Act
        var result = FeatureRecordNormalizer.ConvertJsonElement(element);

        // Assert
        result.Should().Be(3.14159);
    }

    [Fact]
    public void ConvertJsonElement_WithString_ReturnsString()
    {
        // Arrange
        var doc = JsonDocument.Parse("\"test\"");
        var element = doc.RootElement;

        // Act
        var result = FeatureRecordNormalizer.ConvertJsonElement(element);

        // Assert
        result.Should().Be("test");
    }

    [Fact]
    public void ConvertJsonElement_WithDateTime_ReturnsDateTime()
    {
        // Arrange
        var doc = JsonDocument.Parse("\"2024-01-01T12:00:00Z\"");
        var element = doc.RootElement;

        // Act
        var result = FeatureRecordNormalizer.ConvertJsonElement(element);

        // Assert
        result.Should().BeOfType<DateTime>();
    }

    [Fact]
    public void NormalizeFieldValue_WithNull_ReturnsNull()
    {
        // Act
        var result = FeatureRecordNormalizer.NormalizeFieldValue(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NormalizeFieldValue_WithDBNull_ReturnsNull()
    {
        // Act
        var result = FeatureRecordNormalizer.NormalizeFieldValue(DBNull.Value);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NormalizeFieldValue_WithNoTargetType_ReturnsValue()
    {
        // Act
        var result = FeatureRecordNormalizer.NormalizeFieldValue("test", null);

        // Assert
        result.Should().Be("test");
    }

    [Fact]
    public void NormalizeFieldValue_WithDateTimeString_ParsesDateTime()
    {
        // Act
        var result = FeatureRecordNormalizer.NormalizeFieldValue(
            "2024-01-01T12:00:00Z",
            typeof(DateTime));

        // Assert
        result.Should().BeOfType<DateTime>();
    }

    [Fact]
    public void NormalizeFieldValue_WithLongToBoolean_ConvertsToTrue()
    {
        // Act
        var result = FeatureRecordNormalizer.NormalizeFieldValue(1L, typeof(bool));

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void NormalizeFieldValue_WithLongToBoolean_ConvertsToFalse()
    {
        // Act
        var result = FeatureRecordNormalizer.NormalizeFieldValue(0L, typeof(bool));

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void NormalizeFieldValue_WithIntToBoolean_ConvertsCorrectly()
    {
        // Act
        var resultTrue = FeatureRecordNormalizer.NormalizeFieldValue(1, typeof(bool));
        var resultFalse = FeatureRecordNormalizer.NormalizeFieldValue(0, typeof(bool));

        // Assert
        resultTrue.Should().Be(true);
        resultFalse.Should().Be(false);
    }

    [Fact]
    public void SafeToString_WithNull_ReturnsDefaultValue()
    {
        // Act
        var result = FeatureRecordNormalizer.SafeToString(null, "default");

        // Assert
        result.Should().Be("default");
    }

    [Fact]
    public void SafeToString_WithDBNull_ReturnsDefaultValue()
    {
        // Act
        var result = FeatureRecordNormalizer.SafeToString(DBNull.Value, "default");

        // Assert
        result.Should().Be("default");
    }

    [Fact]
    public void SafeToString_WithValue_ReturnsString()
    {
        // Act
        var result = FeatureRecordNormalizer.SafeToString(123);

        // Assert
        result.Should().Be("123");
    }

    [Fact]
    public void SafeToString_WithJsonNode_ReturnsJsonString()
    {
        // Arrange
        var node = JsonNode.Parse("{\"key\":\"value\"}");

        // Act
        var result = FeatureRecordNormalizer.SafeToString(node);

        // Assert
        result.Should().Contain("key");
    }

    [Theory]
    [InlineData("test with 'quotes'")]
    [InlineData("日本語テスト")]
    [InlineData("emoji 😀🎉")]
    [InlineData("newline\ntest")]
    [InlineData("tab\ttest")]
    public void NormalizeValue_WithSpecialCharacters_PreservesValue(string input)
    {
        // Act
        var result = FeatureRecordNormalizer.NormalizeValue(input, false);

        // Assert
        result.Should().Be(input);
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Http;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data;

public class OptimisticLockingTests
{
    [Fact]
    public void ConcurrencyException_ConstructorWithDetails_SetsPropertiesCorrectly()
    {
        // Arrange
        var entityId = "feature-123";
        var entityType = "Feature";
        var expectedVersion = 5L;
        var actualVersion = 7L;

        // Act
        var exception = new ConcurrencyException(entityId, entityType, expectedVersion, actualVersion);

        // Assert
        Assert.Equal(entityId, exception.EntityId);
        Assert.Equal(entityType, exception.EntityType);
        Assert.Equal(expectedVersion, exception.ExpectedVersion);
        Assert.Equal(actualVersion, exception.ActualVersion);
        Assert.Contains("Feature", exception.Message);
        Assert.Contains("feature-123", exception.Message);
        Assert.Contains("5", exception.Message);
        Assert.Contains("7", exception.Message);
    }

    [Fact]
    public void FeatureRecord_WithVersion_StoresVersionCorrectly()
    {
        // Arrange
        var attributes = new Dictionary<string, object?>
        {
            ["id"] = "123",
            ["name"] = "Test Feature",
            ["value"] = 42
        };
        var version = 5L;

        // Act
        var record = new FeatureRecord(attributes, version);

        // Assert
        Assert.NotNull(record.Version);
        Assert.Equal(version, record.Version);
        Assert.Equal(3, record.Attributes.Count);
    }

    [Fact]
    public void FeatureRecord_WithoutVersion_HasNullVersion()
    {
        // Arrange
        var attributes = new Dictionary<string, object?>
        {
            ["id"] = "123",
            ["name"] = "Test Feature"
        };

        // Act
        var record = new FeatureRecord(attributes);

        // Assert
        Assert.Null(record.Version);
    }

    [Theory]
    [InlineData(1L, "W/\"1\"")]
    [InlineData(42L, "W/\"42\"")]
    [InlineData(9999999999L, "W/\"9999999999\"")]
    public void ETagHelper_GenerateETag_WithLongVersion_ReturnsCorrectWeakETag(long version, string expectedETag)
    {
        // Act
        var etag = ETagHelper.GenerateETag(version);

        // Assert
        Assert.Equal(expectedETag, etag);
    }

    [Fact]
    public void ETagHelper_GenerateETag_WithByteArray_ReturnsBase64ETag()
    {
        // Arrange - Simulate SQL Server ROWVERSION
        var version = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x23, 0x45 };

        // Act
        var etag = ETagHelper.GenerateETag(version);

        // Assert
        Assert.NotNull(etag);
        Assert.StartsWith("W/\"", etag);
        Assert.EndsWith("\"", etag);
        Assert.Contains("AAAAAAAAASNE", etag); // Base64 of the byte array
    }

    [Fact]
    public void ETagHelper_GenerateETag_WithNullVersion_ReturnsNull()
    {
        // Act
        var etag = ETagHelper.GenerateETag((object?)null);

        // Assert
        Assert.Null(etag);
    }

    [Fact]
    public void ETagHelper_GenerateETag_WithFeatureRecord_ExtractsVersion()
    {
        // Arrange
        var record = new FeatureRecord(
            new Dictionary<string, object?> { ["id"] = "123" },
            42L);

        // Act
        var etag = ETagHelper.GenerateETag(record);

        // Assert
        Assert.Equal("W/\"42\"", etag);
    }

    [Theory]
    [InlineData("W/\"123\"", "123")]
    [InlineData("\"456\"", "456")]
    [InlineData("W/\"abc-def\"", "abc-def")]
    [InlineData("\"xyz\"", "xyz")]
    public void ETagHelper_ParseETag_WithValidETag_ExtractsValue(string etag, string expectedValue)
    {
        // Act
        var parsed = ETagHelper.ParseETag(etag);

        // Assert
        Assert.Equal(expectedValue, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ETagHelper_ParseETag_WithInvalidETag_ReturnsNull(string? etag)
    {
        // Act
        var parsed = ETagHelper.ParseETag(etag);

        // Assert
        Assert.Null(parsed);
    }

    [Fact]
    public void ETagHelper_ConvertETagToVersion_WithLongString_ReturnsLong()
    {
        // Arrange
        var etagValue = "123456789";

        // Act
        var version = ETagHelper.ConvertETagToVersion(etagValue);

        // Assert
        Assert.IsType<long>(version);
        Assert.Equal(123456789L, version);
    }

    [Fact]
    public void ETagHelper_ConvertETagToVersion_WithBase64String_ReturnsByteArray()
    {
        // Arrange
        var originalBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var base64 = Convert.ToBase64String(originalBytes);

        // Act
        var version = ETagHelper.ConvertETagToVersion(base64, "rowversion");

        // Assert
        Assert.IsType<byte[]>(version);
        Assert.Equal(originalBytes, (byte[])version);
    }

    [Fact]
    public void ETagHelper_ETagMatches_WithMatchingVersions_ReturnsTrue()
    {
        // Arrange
        var version = 42L;
        var etag = "W/\"42\"";

        // Act
        var matches = ETagHelper.ETagMatches(etag, version);

        // Assert
        Assert.True(matches);
    }

    [Fact]
    public void ETagHelper_ETagMatches_WithNonMatchingVersions_ReturnsFalse()
    {
        // Arrange
        var version = 42L;
        var etag = "W/\"43\"";

        // Act
        var matches = ETagHelper.ETagMatches(etag, version);

        // Assert
        Assert.False(matches);
    }

    [Fact]
    public void ETagHelper_ETagMatches_WithNullETag_ReturnsFalse()
    {
        // Arrange
        var version = 42L;

        // Act
        var matches = ETagHelper.ETagMatches(null, version);

        // Assert
        Assert.False(matches);
    }

    [Fact]
    public void ETagHelper_ETagMatches_WithNullVersion_ReturnsFalse()
    {
        // Arrange
        var etag = "W/\"42\"";

        // Act
        var matches = ETagHelper.ETagMatches(etag, null);

        // Assert
        Assert.False(matches);
    }

    [Fact]
    public void OptimisticLockingOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new OptimisticLockingOptions();

        // Assert
        Assert.True(options.Enabled);
        Assert.Equal(VersionRequirementMode.Lenient, options.VersionRequirement);
        Assert.Equal("row_version", options.VersionColumnName);
        Assert.True(options.IncludeVersionInResponses);
        Assert.Equal(0, options.MaxRetryAttempts);
        Assert.Equal(100, options.RetryDelayMilliseconds);
    }

    [Fact]
    public void VersionRequirementMode_HasExpectedValues()
    {
        // Assert enum values exist
        Assert.Equal(0, (int)VersionRequirementMode.Lenient);
        Assert.Equal(1, (int)VersionRequirementMode.Strict);
    }
}

public class FeatureRecordVersionTests
{
    [Fact]
    public void FeatureRecord_With_ReturnsNewInstanceWithUpdatedVersion()
    {
        // Arrange
        var originalAttributes = new Dictionary<string, object?>
        {
            ["id"] = "123",
            ["name"] = "Original"
        };
        var originalRecord = new FeatureRecord(originalAttributes, 1L);

        // Act
        var updatedRecord = originalRecord with { Version = 2L };

        // Assert
        Assert.Equal(1L, originalRecord.Version);
        Assert.Equal(2L, updatedRecord.Version);
        Assert.Same(originalAttributes, originalRecord.Attributes);
        Assert.Same(originalAttributes, updatedRecord.Attributes);
    }

    [Fact]
    public void FeatureRecord_With_CanUpdateBothAttributesAndVersion()
    {
        // Arrange
        var originalAttributes = new Dictionary<string, object?> { ["id"] = "123" };
        var newAttributes = new Dictionary<string, object?> { ["id"] = "456" };
        var originalRecord = new FeatureRecord(originalAttributes, 1L);

        // Act
        var updatedRecord = originalRecord with
        {
            Attributes = newAttributes,
            Version = 2L
        };

        // Assert
        Assert.Equal(1L, originalRecord.Version);
        Assert.Equal(2L, updatedRecord.Version);
        Assert.NotSame(originalAttributes, updatedRecord.Attributes);
        Assert.Equal(newAttributes, updatedRecord.Attributes);
    }
}

using System;
using FluentAssertions;
using Honua.Server.Core.Raster.Cache;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Cache;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class CacheKeyGeneratorTests
{
    [Fact]
    public void GenerateCacheKey_WithValidPath_ShouldIncludePathHash()
    {
        // Arrange
        var sourceUri = "/data/temperature/2023/temp_file.nc";

        // Act
        var cacheKey = CacheKeyGenerator.GenerateCacheKey(sourceUri);

        // Assert
        cacheKey.Should().NotBeNullOrEmpty();
        cacheKey.Should().Contain("_temp_file_");

        // Should have path hash prefix (16 hex chars)
        var parts = cacheKey.Split('_');
        parts.Length.Should().BeGreaterThanOrEqualTo(3);
        parts[0].Should().HaveLength(16);
        parts[0].Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public void GenerateCacheKey_WithDifferentPaths_ShouldProduceDifferentHashes()
    {
        // Arrange
        var path1 = "/data/dir1/temp_file.nc";
        var path2 = "/data/dir2/temp_file.nc";

        // Act
        var key1 = CacheKeyGenerator.GenerateCacheKey(path1);
        var key2 = CacheKeyGenerator.GenerateCacheKey(path2);

        // Assert
        key1.Should().NotBe(key2);

        // Extract hash prefixes
        var hash1 = key1.Split('_')[0];
        var hash2 = key2.Split('_')[0];
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void GenerateCacheKey_WithSameFile_DifferentDirectories_ShouldNotCollide()
    {
        // Arrange - Same filename in different directories (the original collision issue)
        var path1 = "/data/weather/temperature.nc";
        var path2 = "/data/climate/temperature.nc";

        // Act
        var key1 = CacheKeyGenerator.GenerateCacheKey(path1);
        var key2 = CacheKeyGenerator.GenerateCacheKey(path2);

        // Assert
        key1.Should().NotBe(key2, "Files with same name in different directories should have different cache keys");
    }

    [Fact]
    public void GenerateCacheKey_WithVariableName_ShouldIncludeVariable()
    {
        // Arrange
        var sourceUri = "/data/temp.nc";
        var variableName = "air_temperature";

        // Act
        var cacheKey = CacheKeyGenerator.GenerateCacheKey(sourceUri, variableName);

        // Assert
        cacheKey.Should().Contain("air_temperature");
    }

    [Fact]
    public void GenerateCacheKey_WithTimeIndex_ShouldIncludeTimeIndex()
    {
        // Arrange
        var sourceUri = "/data/temp.nc";
        var timeIndex = 42;

        // Act
        var cacheKey = CacheKeyGenerator.GenerateCacheKey(sourceUri, null, timeIndex);

        // Assert
        cacheKey.Should().EndWith("_42");
    }

    [Fact]
    public void GenerateCacheKey_WithAllParameters_ShouldIncludeAllComponents()
    {
        // Arrange
        var sourceUri = "/data/weather/temperature.nc";
        var variableName = "air_temp";
        var timeIndex = 10;

        // Act
        var cacheKey = CacheKeyGenerator.GenerateCacheKey(sourceUri, variableName, timeIndex);

        // Assert
        var parts = cacheKey.Split('_');
        parts.Should().Contain("temperature");
        parts.Should().Contain("air");
        parts.Should().Contain("temp");
        cacheKey.Should().EndWith("_10");
    }

    [Fact]
    public void GenerateCacheKey_ShouldBeFilesystemSafe()
    {
        // Arrange
        var sourceUri = "/data/with spaces/special:chars?.nc";

        // Act
        var cacheKey = CacheKeyGenerator.GenerateCacheKey(sourceUri);

        // Assert
        cacheKey.Should().NotContain(" ");
        cacheKey.Should().NotContain(":");
        cacheKey.Should().NotContain("?");
        cacheKey.Should().NotContain("/");
        cacheKey.Should().NotContain("\\");
    }

    [Fact]
    public void GenerateCacheKey_WithNullOrEmptyUri_ShouldThrowArgumentException()
    {
        // Act & Assert
        var act1 = () => CacheKeyGenerator.GenerateCacheKey(null!);
        var act2 = () => CacheKeyGenerator.GenerateCacheKey("");
        var act3 = () => CacheKeyGenerator.GenerateCacheKey("   ");

        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
        act3.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GeneratePathHash_ShouldBeConsistent()
    {
        // Arrange
        var path = "/data/temperature/file.nc";

        // Act
        var hash1 = CacheKeyGenerator.GeneratePathHash(path);
        var hash2 = CacheKeyGenerator.GeneratePathHash(path);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GeneratePathHash_ShouldBeCaseInsensitive()
    {
        // Arrange
        var path1 = "/data/Temperature/File.nc";
        var path2 = "/data/temperature/file.nc";

        // Act
        var hash1 = CacheKeyGenerator.GeneratePathHash(path1);
        var hash2 = CacheKeyGenerator.GeneratePathHash(path2);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GeneratePathHash_ShouldNormalizePathSeparators()
    {
        // Arrange
        var unixPath = "/data/temperature/file.nc";
        var windowsPath = "\\data\\temperature\\file.nc";

        // Act
        var hash1 = CacheKeyGenerator.GeneratePathHash(unixPath);
        var hash2 = CacheKeyGenerator.GeneratePathHash(windowsPath);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GeneratePathHash_ShouldReturn16HexCharacters()
    {
        // Arrange
        var path = "/data/test/file.nc";

        // Act
        var hash = CacheKeyGenerator.GeneratePathHash(path);

        // Assert
        hash.Should().HaveLength(16);
        hash.Should().MatchRegex("^[0-9a-f]{16}$");
    }

    [Fact]
    public void ValidateCacheKey_WithValidKey_ShouldReturnTrue()
    {
        // Arrange
        var validKey = CacheKeyGenerator.GenerateCacheKey("/data/temp.nc");

        // Act
        var isValid = CacheKeyGenerator.ValidateCacheKey(validKey);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateCacheKey_WithInvalidKey_ShouldReturnFalse()
    {
        // Arrange - Old format without path hash
        var invalidKey = "temp_file_default_0";

        // Act
        var isValid = CacheKeyGenerator.ValidateCacheKey(invalidKey);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateCacheKey_WithNullOrEmpty_ShouldReturnFalse()
    {
        // Act & Assert
        CacheKeyGenerator.ValidateCacheKey(null!).Should().BeFalse();
        CacheKeyGenerator.ValidateCacheKey("").Should().BeFalse();
        CacheKeyGenerator.ValidateCacheKey("   ").Should().BeFalse();
    }

    [Fact]
    public void ValidateCacheKey_WithTooShortKey_ShouldReturnFalse()
    {
        // Arrange
        var shortKey = "abc123";

        // Act
        var isValid = CacheKeyGenerator.ValidateCacheKey(shortKey);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void DetectCollision_WithIdenticalKeys_ShouldReturnTrue()
    {
        // Arrange
        var key = CacheKeyGenerator.GenerateCacheKey("/data/temp.nc");

        // Act
        var hasCollision = CacheKeyGenerator.DetectCollision(key, key);

        // Assert
        hasCollision.Should().BeTrue();
    }

    [Fact]
    public void DetectCollision_WithDifferentKeys_ShouldReturnFalse()
    {
        // Arrange
        var key1 = CacheKeyGenerator.GenerateCacheKey("/data/temp1.nc");
        var key2 = CacheKeyGenerator.GenerateCacheKey("/data/temp2.nc");

        // Act
        var hasCollision = CacheKeyGenerator.DetectCollision(key1, key2);

        // Assert
        hasCollision.Should().BeFalse();
    }

    [Fact]
    public void GenerateCacheKeyFromDatasetId_ShouldCreateValidKey()
    {
        // Arrange
        var datasetId = "weather-temperature-2023";

        // Act
        var cacheKey = CacheKeyGenerator.GenerateCacheKeyFromDatasetId(datasetId);

        // Assert
        cacheKey.Should().NotBeNullOrEmpty();
        cacheKey.Should().Contain("weather-temperature-2023");
    }

    [Fact]
    public void GenerateCacheKeyFromDatasetId_WithNullOrEmptyId_ShouldThrowArgumentException()
    {
        // Act & Assert
        var act1 = () => CacheKeyGenerator.GenerateCacheKeyFromDatasetId(null!);
        var act2 = () => CacheKeyGenerator.GenerateCacheKeyFromDatasetId("");
        var act3 = () => CacheKeyGenerator.GenerateCacheKeyFromDatasetId("   ");

        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
        act3.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateCacheKey_WithLongPath_ShouldTruncateFilename()
    {
        // Arrange
        var longFilename = new string('a', 100);
        var sourceUri = $"/data/{longFilename}.nc";

        // Act
        var cacheKey = CacheKeyGenerator.GenerateCacheKey(sourceUri);

        // Assert
        cacheKey.Should().NotBeNullOrEmpty();
        // Key should be reasonable length (not excessive)
        cacheKey.Length.Should().BeLessThan(150);
    }

    [Fact]
    public void GenerateCacheKey_WithSpecialCharactersInVariable_ShouldSanitize()
    {
        // Arrange
        var sourceUri = "/data/temp.nc";
        var variableName = "air/temperature:level*2";

        // Act
        var cacheKey = CacheKeyGenerator.GenerateCacheKey(sourceUri, variableName);

        // Assert
        cacheKey.Should().NotContain("/");
        cacheKey.Should().NotContain(":");
        cacheKey.Should().NotContain("*");
    }

    [Fact]
    public void CollisionResistance_Test_1000Variations()
    {
        // Arrange - Generate many variations of similar filenames
        var keys = new System.Collections.Generic.HashSet<string>();

        // Act - Generate keys for 1000 different files
        for (int i = 0; i < 1000; i++)
        {
            var path = $"/data/dir{i}/temperature.nc";
            var key = CacheKeyGenerator.GenerateCacheKey(path);
            keys.Add(key);
        }

        // Assert - Should have 1000 unique keys (no collisions)
        keys.Should().HaveCount(1000, "All generated cache keys should be unique");
    }
}

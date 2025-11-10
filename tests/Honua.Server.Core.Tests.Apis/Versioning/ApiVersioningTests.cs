// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Versioning;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Versioning;

public class ApiVersioningTests
{
    [Theory]
    [InlineData("1.0", 1, 0)]
    [InlineData("2.5", 2, 5)]
    [InlineData("10.15", 10, 15)]
    public void ParseVersion_WithValidVersion_ReturnsCorrectNumbers(string versionString, int major, int minor)
    {
        // Act
        var version = ApiVersion.Parse(versionString);

        // Assert
        version.Major.Should().Be(major);
        version.Minor.Should().Be(minor);
    }

    [Fact]
    public void ParseVersion_WithInvalidVersion_ThrowsException()
    {
        // Arrange
        var invalidVersion = "invalid";

        // Act & Assert
        Assert.Throws<FormatException>(() => ApiVersion.Parse(invalidVersion));
    }

    [Fact]
    public void CompareVersions_NewerVersion_ReturnsPositive()
    {
        // Arrange
        var v1 = new ApiVersion(1, 0);
        var v2 = new ApiVersion(2, 0);

        // Act
        var comparison = v2.CompareTo(v1);

        // Assert
        comparison.Should().BePositive();
    }

    [Fact]
    public void CompareVersions_OlderVersion_ReturnsNegative()
    {
        // Arrange
        var v1 = new ApiVersion(2, 0);
        var v2 = new ApiVersion(1, 0);

        // Act
        var comparison = v2.CompareTo(v1);

        // Assert
        comparison.Should().BeNegative();
    }

    [Fact]
    public void CompareVersions_SameVersion_ReturnsZero()
    {
        // Arrange
        var v1 = new ApiVersion(1, 5);
        var v2 = new ApiVersion(1, 5);

        // Act
        var comparison = v1.CompareTo(v2);

        // Assert
        comparison.Should().Be(0);
    }

    [Fact]
    public void IsCompatibleWith_SameMajorVersion_ReturnsTrue()
    {
        // Arrange
        var v1 = new ApiVersion(1, 0);
        var v2 = new ApiVersion(1, 5);

        // Act
        var isCompatible = v1.IsCompatibleWith(v2);

        // Assert
        isCompatible.Should().BeTrue();
    }

    [Fact]
    public void IsCompatibleWith_DifferentMajorVersion_ReturnsFalse()
    {
        // Arrange
        var v1 = new ApiVersion(1, 0);
        var v2 = new ApiVersion(2, 0);

        // Act
        var isCompatible = v1.IsCompatibleWith(v2);

        // Assert
        isCompatible.Should().BeFalse();
    }

    [Fact]
    public void ToString_ReturnsFormattedVersion()
    {
        // Arrange
        var version = new ApiVersion(3, 2);

        // Act
        var versionString = version.ToString();

        // Assert
        versionString.Should().Be("3.2");
    }
}

// Mock implementation for testing
public class ApiVersion : IComparable<ApiVersion>
{
    public int Major { get; }
    public int Minor { get; }

    public ApiVersion(int major, int minor)
    {
        Major = major;
        Minor = minor;
    }

    public static ApiVersion Parse(string version)
    {
        var parts = version.Split('.');
        if (parts.Length != 2)
            throw new FormatException("Invalid version format");

        return new ApiVersion(int.Parse(parts[0]), int.Parse(parts[1]));
    }

    public int CompareTo(ApiVersion? other)
    {
        if (other == null) return 1;
        var majorComparison = Major.CompareTo(other.Major);
        return majorComparison != 0 ? majorComparison : Minor.CompareTo(other.Minor);
    }

    public bool IsCompatibleWith(ApiVersion other)
    {
        return Major == other.Major;
    }

    public override string ToString() => $"{Major}.{Minor}";
}

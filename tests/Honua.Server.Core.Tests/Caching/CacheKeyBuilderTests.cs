// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Caching;
using Xunit;

namespace Honua.Server.Core.Tests.Caching;

public class CacheKeyBuilderTests
{
    [Fact]
    public void Build_WithSimpleValues_CreatesCorrectKey()
    {
        // Arrange
        var builder = new CacheKeyBuilder("TestPrefix");

        // Act
        var key = builder
            .WithNamespace("DataLayer")
            .WithEntity("Feature")
            .WithId("123")
            .Build();

        // Assert
        key.Should().Contain("TestPrefix");
        key.Should().Contain("DataLayer");
        key.Should().Contain("Feature");
        key.Should().Contain("123");
    }

    [Fact]
    public void Build_WithMultipleParameters_IncludesAllParams()
    {
        // Arrange
        var builder = new CacheKeyBuilder("Query");

        // Act
        var key = builder
            .WithNamespace("Spatial")
            .WithParameter("bbox", "0,0,10,10")
            .WithParameter("srid", "4326")
            .WithParameter("limit", "100")
            .Build();

        // Assert
        key.Should().Contain("bbox");
        key.Should().Contain("srid");
        key.Should().Contain("limit");
    }

    [Fact]
    public void Build_WithNullParameter_HandlesGracefully()
    {
        // Arrange
        var builder = new CacheKeyBuilder("Test");

        // Act
        var key = builder
            .WithParameter("value", null)
            .Build();

        // Assert
        key.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Build_CalledMultipleTimes_ReturnsSameKey()
    {
        // Arrange
        var builder = new CacheKeyBuilder("Test")
            .WithNamespace("NS")
            .WithId("456");

        // Act
        var key1 = builder.Build();
        var key2 = builder.Build();

        // Assert
        key1.Should().Be(key2);
    }

    [Fact]
    public void WithVersion_AddsVersionToKey()
    {
        // Arrange
        var builder = new CacheKeyBuilder("Versioned");

        // Act
        var key = builder
            .WithVersion("v2.0")
            .Build();

        // Assert
        key.Should().Contain("v2.0");
    }

    [Theory]
    [InlineData("special:chars")]
    [InlineData("with/slashes")]
    [InlineData("with spaces")]
    public void Build_WithSpecialCharacters_NormalizesKey(string input)
    {
        // Arrange
        var builder = new CacheKeyBuilder("Test");

        // Act
        var key = builder
            .WithParameter("special", input)
            .Build();

        // Assert
        key.Should().NotBeNullOrEmpty();
        // Key should not contain problematic characters
        key.Should().NotContain(":");
    }
}

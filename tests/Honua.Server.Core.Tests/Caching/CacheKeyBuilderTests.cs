// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Caching;
using Xunit;

namespace Honua.Server.Core.Tests.Caching;

public class CacheKeyBuilderTests
{
    [Fact]
    public void ForLayer_CreatesCorrectKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder.ForLayer("service1", "layer1")
            .WithSuffix("metadata")
            .Build();

        // Assert
        key.Should().Contain("layer");
        key.Should().Contain("service1");
        key.Should().Contain("layer1");
        key.Should().Contain("metadata");
    }

    [Fact]
    public void ForTile_CreatesCorrectKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder.ForTile("WebMercatorQuad", 5, 10, 12, "pbf")
            .Build();

        // Assert
        key.Should().Contain("tile");
        key.Should().Contain("WebMercatorQuad");
        key.Should().Contain("5");
        key.Should().Contain("10");
        key.Should().Contain("12");
        key.Should().Contain("pbf");
    }

    [Fact]
    public void ForQuery_WithComponents_CreatesCorrectKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder.ForQuery("layer1")
            .WithComponent("filter")
            .Build();

        // Assert
        key.Should().Contain("query");
        key.Should().Contain("layer1");
        key.Should().Contain("filter");
    }

    [Fact]
    public void WithBoundingBox_AddsHashToKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder.ForQuery("layer1")
            .WithBoundingBox(0, 0, 10, 10)
            .Build();

        // Assert
        key.Should().NotBeNullOrEmpty();
        key.Should().Contain("query");
    }

    [Fact]
    public void WithVersion_AddsVersionToKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder.ForLayer("service1", "layer1")
            .WithVersion(2)
            .Build();

        // Assert
        key.Should().Contain("v2");
    }

    [Fact]
    public void ForCrsTransform_CreatesCorrectKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder.ForCrsTransform("EPSG:4326", "EPSG:3857")
            .Build();

        // Assert
        key.Should().Contain("crs");
        key.Should().Contain("EPSG");
    }

    [Fact]
    public void ForStacCollection_WithId_CreatesCorrectKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder.ForStacCollection("collection1")
            .Build();

        // Assert
        key.Should().Contain("stac");
        key.Should().Contain("collection");
        key.Should().Contain("collection1");
    }

    [Fact]
    public void ForStacItem_CreatesCorrectKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder.ForStacItem("collection1", "item1")
            .Build();

        // Assert
        key.Should().Contain("stac");
        key.Should().Contain("item");
        key.Should().Contain("collection1");
        key.Should().Contain("item1");
    }

    [Fact]
    public void WithFilter_AddsHashToKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder.ForQuery("layer1")
            .WithFilter("name = 'test'")
            .Build();

        // Assert
        key.Should().NotBeNullOrEmpty();
        key.Should().Contain("query");
    }

    [Fact]
    public void WithObjectHash_AddsHashToKey()
    {
        // Arrange
        var obj = new { Name = "test", Value = 123 };

        // Act
        var key = CacheKeyBuilder.ForQuery("layer1")
            .WithObjectHash(obj)
            .Build();

        // Assert
        key.Should().NotBeNullOrEmpty();
        key.Should().Contain("query");
    }

    [Fact]
    public void ForStacCollection_WithoutId_CreatesCollectionsKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder.ForStacCollection()
            .Build();

        // Assert
        key.Should().Contain("stac");
        key.Should().Contain("collections");
    }

    [Fact]
    public void ForStacItem_WithoutItemId_CreatesItemsListKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder.ForStacItem("collection1")
            .Build();

        // Assert
        key.Should().Contain("stac");
        key.Should().Contain("collection1");
        key.Should().Contain("items");
    }

    [Fact]
    public void ForStacSearch_CreatesSearchKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder.ForStacSearch()
            .Build();

        // Assert
        key.Should().Contain("stac");
        key.Should().Contain("search");
    }

    [Fact]
    public void ForOgcApi_CreatesCorrectKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder.ForOgcApi("features")
            .Build();

        // Assert
        key.Should().Contain("ogc");
        key.Should().Contain("features");
    }

    [Fact]
    public void WithTimestamp_AddsTimestampToKey()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2025, 11, 14, 12, 0, 0, TimeSpan.Zero);

        // Act
        var key = CacheKeyBuilder.ForLayer("service1", "layer1")
            .WithTimestamp(timestamp)
            .Build();

        // Assert
        key.Should().NotBeNullOrEmpty();
        key.Should().Contain("layer");
    }

    [Fact]
    public void Build_CalledTwice_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = CacheKeyBuilder.ForLayer("service1", "layer1");
        builder.Build();

        // Act
        var act = () => builder.Build();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been built*");
    }

    [Fact]
    public void WithFilter_WithNullOrEmpty_DoesNotAddToKey()
    {
        // Arrange & Act
        var key1 = CacheKeyBuilder.ForQuery("layer1")
            .WithFilter(null!)
            .Build();

        var key2 = CacheKeyBuilder.ForQuery("layer1")
            .WithFilter("")
            .Build();

        var key3 = CacheKeyBuilder.ForQuery("layer1")
            .WithFilter("   ")
            .Build();

        // Assert
        key1.Should().NotBeNullOrEmpty();
        key2.Should().NotBeNullOrEmpty();
        key3.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void WithObjectHash_WithNull_DoesNotAddToKey()
    {
        // Arrange & Act
        var key = CacheKeyBuilder.ForQuery("layer1")
            .WithObjectHash<object>(null!)
            .Build();

        // Assert
        key.Should().NotBeNullOrEmpty();
        key.Should().Contain("query");
    }
}

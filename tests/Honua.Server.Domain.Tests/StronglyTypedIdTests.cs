// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Domain;
using Honua.Server.Core.Domain.ValueObjects;
using Xunit;

namespace Honua.Server.Domain.Tests;

/// <summary>
/// Unit tests for strongly-typed ID value objects (ShareTokenId, MapId, UserId).
/// Tests GUID-based ID validation, parsing, and equality.
/// </summary>
[Trait("Category", "Unit")]
public class StronglyTypedIdTests
{
    #region ShareTokenId Tests

    [Fact]
    public void ShareTokenId_Constructor_ShouldSetValue_WhenValidGuidProvided()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = new ShareTokenId(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void ShareTokenId_Constructor_ShouldThrowDomainException_WhenEmptyGuidProvided()
    {
        // Arrange, Act & Assert
        var act = () => new ShareTokenId(Guid.Empty);
        act.Should().Throw<DomainException>()
            .WithMessage("Share token ID cannot be empty.")
            .Which.ErrorCode.Should().Be("SHARETOKENID_EMPTY");
    }

    [Fact]
    public void ShareTokenId_New_ShouldCreateUniqueId_WhenCalled()
    {
        // Arrange & Act
        var id1 = ShareTokenId.New();
        var id2 = ShareTokenId.New();

        // Assert
        id1.Should().NotBe(id2);
        id1.Value.Should().NotBe(Guid.Empty);
        id2.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void ShareTokenId_Parse_ShouldCreateId_WhenValidGuidStringProvided()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var guidString = guid.ToString();

        // Act
        var id = ShareTokenId.Parse(guidString);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("not-a-guid")]
    public void ShareTokenId_Parse_ShouldThrowDomainException_WhenInvalidGuidStringProvided(string invalidGuid)
    {
        // Arrange, Act & Assert
        var act = () => ShareTokenId.Parse(invalidGuid);
        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("SHARETOKENID_INVALID_FORMAT");
    }

    [Fact]
    public void ShareTokenId_TryParse_ShouldReturnTrue_WhenValidGuidStringProvided()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var guidString = guid.ToString();

        // Act
        var result = ShareTokenId.TryParse(guidString, out var id);

        // Assert
        result.Should().BeTrue();
        id.Should().NotBeNull();
        id!.Value.Should().Be(guid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("not-a-guid")]
    public void ShareTokenId_TryParse_ShouldReturnFalse_WhenInvalidGuidStringProvided(string invalidGuid)
    {
        // Arrange, Act
        var result = ShareTokenId.TryParse(invalidGuid, out var id);

        // Assert
        result.Should().BeFalse();
        id.Should().BeNull();
    }

    [Fact]
    public void ShareTokenId_TryParse_ShouldReturnFalse_WhenEmptyGuidProvided()
    {
        // Arrange
        var emptyGuidString = Guid.Empty.ToString();

        // Act
        var result = ShareTokenId.TryParse(emptyGuidString, out var id);

        // Assert
        result.Should().BeFalse();
        id.Should().BeNull();
    }

    [Fact]
    public void ShareTokenId_ToString_ShouldReturnGuidString_WhenCalled()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = new ShareTokenId(guid);

        // Act
        var result = id.ToString();

        // Assert
        result.Should().Be(guid.ToString());
    }

    [Fact]
    public void ShareTokenId_ImplicitConversion_ShouldConvertToGuid_Correctly()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = new ShareTokenId(guid);

        // Act
        Guid result = id;

        // Assert
        result.Should().Be(guid);
    }

    [Fact]
    public void ShareTokenId_Equality_ShouldBeTrue_ForSameGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new ShareTokenId(guid);
        var id2 = new ShareTokenId(guid);

        // Act & Assert
        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void ShareTokenId_Equality_ShouldBeFalse_ForDifferentGuids()
    {
        // Arrange
        var id1 = ShareTokenId.New();
        var id2 = ShareTokenId.New();

        // Act & Assert
        id1.Should().NotBe(id2);
        (id1 != id2).Should().BeTrue();
    }

    #endregion

    #region MapId Tests

    [Fact]
    public void MapId_Constructor_ShouldSetValue_WhenValidGuidProvided()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = new MapId(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void MapId_Constructor_ShouldThrowDomainException_WhenEmptyGuidProvided()
    {
        // Arrange, Act & Assert
        var act = () => new MapId(Guid.Empty);
        act.Should().Throw<DomainException>()
            .WithMessage("Map ID cannot be empty.")
            .Which.ErrorCode.Should().Be("MAPID_EMPTY");
    }

    [Fact]
    public void MapId_New_ShouldCreateUniqueId_WhenCalled()
    {
        // Arrange & Act
        var id1 = MapId.New();
        var id2 = MapId.New();

        // Assert
        id1.Should().NotBe(id2);
        id1.Value.Should().NotBe(Guid.Empty);
        id2.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void MapId_Parse_ShouldCreateId_WhenValidGuidStringProvided()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var guidString = guid.ToString();

        // Act
        var id = MapId.Parse(guidString);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("not-a-guid")]
    public void MapId_Parse_ShouldThrowDomainException_WhenInvalidGuidStringProvided(string invalidGuid)
    {
        // Arrange, Act & Assert
        var act = () => MapId.Parse(invalidGuid);
        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("MAPID_INVALID_FORMAT");
    }

    [Fact]
    public void MapId_TryParse_ShouldReturnTrue_WhenValidGuidStringProvided()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var guidString = guid.ToString();

        // Act
        var result = MapId.TryParse(guidString, out var id);

        // Assert
        result.Should().BeTrue();
        id.Should().NotBeNull();
        id!.Value.Should().Be(guid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("not-a-guid")]
    public void MapId_TryParse_ShouldReturnFalse_WhenInvalidGuidStringProvided(string invalidGuid)
    {
        // Arrange, Act
        var result = MapId.TryParse(invalidGuid, out var id);

        // Assert
        result.Should().BeFalse();
        id.Should().BeNull();
    }

    [Fact]
    public void MapId_TryParse_ShouldReturnFalse_WhenEmptyGuidProvided()
    {
        // Arrange
        var emptyGuidString = Guid.Empty.ToString();

        // Act
        var result = MapId.TryParse(emptyGuidString, out var id);

        // Assert
        result.Should().BeFalse();
        id.Should().BeNull();
    }

    [Fact]
    public void MapId_ToString_ShouldReturnGuidString_WhenCalled()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = new MapId(guid);

        // Act
        var result = id.ToString();

        // Assert
        result.Should().Be(guid.ToString());
    }

    [Fact]
    public void MapId_ImplicitConversion_ShouldConvertToGuid_Correctly()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = new MapId(guid);

        // Act
        Guid result = id;

        // Assert
        result.Should().Be(guid);
    }

    [Fact]
    public void MapId_Equality_ShouldBeTrue_ForSameGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new MapId(guid);
        var id2 = new MapId(guid);

        // Act & Assert
        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void MapId_Equality_ShouldBeFalse_ForDifferentGuids()
    {
        // Arrange
        var id1 = MapId.New();
        var id2 = MapId.New();

        // Act & Assert
        id1.Should().NotBe(id2);
        (id1 != id2).Should().BeTrue();
    }

    #endregion

    #region UserId Tests

    [Fact]
    public void UserId_Constructor_ShouldSetValue_WhenValidGuidProvided()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = new UserId(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void UserId_Constructor_ShouldThrowDomainException_WhenEmptyGuidProvided()
    {
        // Arrange, Act & Assert
        var act = () => new UserId(Guid.Empty);
        act.Should().Throw<DomainException>()
            .WithMessage("User ID cannot be empty.")
            .Which.ErrorCode.Should().Be("USERID_EMPTY");
    }

    [Fact]
    public void UserId_New_ShouldCreateUniqueId_WhenCalled()
    {
        // Arrange & Act
        var id1 = UserId.New();
        var id2 = UserId.New();

        // Assert
        id1.Should().NotBe(id2);
        id1.Value.Should().NotBe(Guid.Empty);
        id2.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void UserId_Parse_ShouldCreateId_WhenValidGuidStringProvided()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var guidString = guid.ToString();

        // Act
        var id = UserId.Parse(guidString);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("not-a-guid")]
    public void UserId_Parse_ShouldThrowDomainException_WhenInvalidGuidStringProvided(string invalidGuid)
    {
        // Arrange, Act & Assert
        var act = () => UserId.Parse(invalidGuid);
        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("USERID_INVALID_FORMAT");
    }

    [Fact]
    public void UserId_TryParse_ShouldReturnTrue_WhenValidGuidStringProvided()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var guidString = guid.ToString();

        // Act
        var result = UserId.TryParse(guidString, out var id);

        // Assert
        result.Should().BeTrue();
        id.Should().NotBeNull();
        id!.Value.Should().Be(guid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("not-a-guid")]
    public void UserId_TryParse_ShouldReturnFalse_WhenInvalidGuidStringProvided(string invalidGuid)
    {
        // Arrange, Act
        var result = UserId.TryParse(invalidGuid, out var id);

        // Assert
        result.Should().BeFalse();
        id.Should().BeNull();
    }

    [Fact]
    public void UserId_TryParse_ShouldReturnFalse_WhenEmptyGuidProvided()
    {
        // Arrange
        var emptyGuidString = Guid.Empty.ToString();

        // Act
        var result = UserId.TryParse(emptyGuidString, out var id);

        // Assert
        result.Should().BeFalse();
        id.Should().BeNull();
    }

    [Fact]
    public void UserId_ToString_ShouldReturnGuidString_WhenCalled()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = new UserId(guid);

        // Act
        var result = id.ToString();

        // Assert
        result.Should().Be(guid.ToString());
    }

    [Fact]
    public void UserId_ImplicitConversion_ShouldConvertToGuid_Correctly()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = new UserId(guid);

        // Act
        Guid result = id;

        // Assert
        result.Should().Be(guid);
    }

    [Fact]
    public void UserId_Equality_ShouldBeTrue_ForSameGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new UserId(guid);
        var id2 = new UserId(guid);

        // Act & Assert
        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void UserId_Equality_ShouldBeFalse_ForDifferentGuids()
    {
        // Arrange
        var id1 = UserId.New();
        var id2 = UserId.New();

        // Act & Assert
        id1.Should().NotBe(id2);
        (id1 != id2).Should().BeTrue();
    }

    #endregion

    #region Type Safety Tests

    [Fact]
    public void StronglyTypedIds_ShouldPreventMixingTypes_AtCompileTime()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var userId = new UserId(guid);
        var mapId = new MapId(guid);
        var shareTokenId = new ShareTokenId(guid);

        // Act & Assert
        // This test documents that the types are different and cannot be mixed
        userId.Should().NotBe(mapId as object);
        mapId.Should().NotBe(shareTokenId as object);
        shareTokenId.Should().NotBe(userId as object);

        // Even though they have the same GUID value, they are different types
        userId.Value.Should().Be(guid);
        mapId.Value.Should().Be(guid);
        shareTokenId.Value.Should().Be(guid);
    }

    [Fact]
    public void StronglyTypedIds_ShouldBeImmutable_AfterConstruction()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var userId = new UserId(guid);
        var mapId = new MapId(guid);
        var shareTokenId = new ShareTokenId(guid);

        // Act & Assert
        userId.Value.Should().Be(guid);
        mapId.Value.Should().Be(guid);
        shareTokenId.Value.Should().Be(guid);

        // Cannot modify properties (compile-time guarantee via init-only setters)
        // This test documents the immutability design
    }

    #endregion
}

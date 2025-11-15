// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Domain;
using Honua.Server.Core.Domain.ValueObjects;
using Xunit;

namespace Honua.Server.Domain.Tests;

/// <summary>
/// Unit tests for the Permission value object.
/// Tests permission creation, parsing, matching, and implication logic.
/// </summary>
[Trait("Category", "Unit")]
public class PermissionTests
{
    [Fact]
    public void Constructor_ShouldCreatePermission_WhenValidInputProvided()
    {
        // Arrange & Act
        var permission = new Permission("read", "map");

        // Assert
        permission.Action.Should().Be("read");
        permission.ResourceType.Should().Be("map");
        permission.Scope.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldCreatePermissionWithScope_WhenScopeProvided()
    {
        // Arrange & Act
        var permission = new Permission("read", "map", "own");

        // Assert
        permission.Action.Should().Be("read");
        permission.ResourceType.Should().Be("map");
        permission.Scope.Should().Be("own");
    }

    [Fact]
    public void Constructor_ShouldNormalizeActionAndResourceType_ToLowerCase()
    {
        // Arrange & Act
        var permission = new Permission("READ", "MAP", "OWN");

        // Assert
        permission.Action.Should().Be("read");
        permission.ResourceType.Should().Be("map");
        permission.Scope.Should().Be("OWN", "scope case should be preserved");
    }

    [Fact]
    public void Constructor_ShouldTrimInputs_WhenProvided()
    {
        // Arrange & Act
        var permission = new Permission("  read  ", "  map  ", "  own  ");

        // Assert
        permission.Action.Should().Be("read");
        permission.ResourceType.Should().Be("map");
        permission.Scope.Should().Be("own");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_ShouldThrowDomainException_WhenActionIsEmpty(string action)
    {
        // Arrange, Act & Assert
        var act = () => new Permission(action, "map");
        act.Should().Throw<DomainException>()
            .WithMessage("Permission action cannot be empty.")
            .Which.ErrorCode.Should().Be("PERMISSION_ACTION_EMPTY");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_ShouldThrowDomainException_WhenResourceTypeIsEmpty(string resourceType)
    {
        // Arrange, Act & Assert
        var act = () => new Permission("read", resourceType);
        act.Should().Throw<DomainException>()
            .WithMessage("Permission resource type cannot be empty.")
            .Which.ErrorCode.Should().Be("PERMISSION_RESOURCE_TYPE_EMPTY");
    }

    [Theory]
    [InlineData("read:map", "read", "map", null)]
    [InlineData("write:layer", "write", "layer", null)]
    [InlineData("delete:user", "delete", "user", null)]
    [InlineData("read:map:own", "read", "map", "own")]
    [InlineData("write:layer:123", "write", "layer", "123")]
    public void Parse_ShouldCreatePermission_WhenValidStringProvided(
        string permissionString, string expectedAction, string expectedResourceType, string? expectedScope)
    {
        // Arrange & Act
        var permission = Permission.Parse(permissionString);

        // Assert
        permission.Action.Should().Be(expectedAction);
        permission.ResourceType.Should().Be(expectedResourceType);
        permission.Scope.Should().Be(expectedScope);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Parse_ShouldThrowDomainException_WhenStringIsEmpty(string permissionString)
    {
        // Arrange, Act & Assert
        var act = () => Permission.Parse(permissionString);
        act.Should().Throw<DomainException>()
            .WithMessage("Permission string cannot be empty.")
            .Which.ErrorCode.Should().Be("PERMISSION_STRING_EMPTY");
    }

    [Theory]
    [InlineData("read")]
    [InlineData("read:")]
    [InlineData(":map")]
    [InlineData("read:map:scope:extra")]
    public void Parse_ShouldThrowDomainException_WhenFormatIsInvalid(string permissionString)
    {
        // Arrange, Act & Assert
        var act = () => Permission.Parse(permissionString);
        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("PERMISSION_INVALID_FORMAT");
    }

    [Fact]
    public void TryParse_ShouldReturnTrue_WhenValidStringProvided()
    {
        // Arrange
        var permissionString = "read:map";

        // Act
        var result = Permission.TryParse(permissionString, out var permission);

        // Assert
        result.Should().BeTrue();
        permission.Should().NotBeNull();
        permission!.Action.Should().Be("read");
        permission.ResourceType.Should().Be("map");
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("read")]
    public void TryParse_ShouldReturnFalse_WhenInvalidStringProvided(string permissionString)
    {
        // Arrange, Act
        var result = Permission.TryParse(permissionString, out var permission);

        // Assert
        result.Should().BeFalse();
        permission.Should().BeNull();
    }

    [Fact]
    public void Matches_ShouldReturnTrue_WhenPermissionsAreIdentical()
    {
        // Arrange
        var permission1 = new Permission("read", "map");
        var permission2 = new Permission("read", "map");

        // Act
        var result = permission1.Matches(permission2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Matches_ShouldReturnFalse_WhenActionsAreDifferent()
    {
        // Arrange
        var permission1 = new Permission("read", "map");
        var permission2 = new Permission("write", "map");

        // Act
        var result = permission1.Matches(permission2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Matches_ShouldReturnFalse_WhenResourceTypesAreDifferent()
    {
        // Arrange
        var permission1 = new Permission("read", "map");
        var permission2 = new Permission("read", "layer");

        // Act
        var result = permission1.Matches(permission2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Matches_ShouldReturnTrue_WhenScopeIsNull_AndOtherHasAnyScope()
    {
        // Arrange
        var permission1 = new Permission("read", "map", null);
        var permission2 = new Permission("read", "map", "own");

        // Act
        var result = permission1.Matches(permission2);

        // Assert
        result.Should().BeTrue("null scope should match any scope");
    }

    [Fact]
    public void Matches_ShouldReturnTrue_WhenScopesAreIdentical()
    {
        // Arrange
        var permission1 = new Permission("read", "map", "own");
        var permission2 = new Permission("read", "map", "own");

        // Act
        var result = permission1.Matches(permission2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Matches_ShouldReturnFalse_WhenScopesAreDifferent()
    {
        // Arrange
        var permission1 = new Permission("read", "map", "own");
        var permission2 = new Permission("read", "map", "all");

        // Act
        var result = permission1.Matches(permission2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Matches_ShouldThrowArgumentNullException_WhenOtherIsNull()
    {
        // Arrange
        var permission = new Permission("read", "map");

        // Act & Assert
        var act = () => permission.Matches(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Implies_ShouldReturnTrue_WhenPermissionsAreIdentical()
    {
        // Arrange
        var permission1 = new Permission("read", "map");
        var permission2 = new Permission("read", "map");

        // Act
        var result = permission1.Implies(permission2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Implies_ShouldReturnTrue_WhenActionIsWildcard()
    {
        // Arrange
        var permission1 = new Permission("*", "map");
        var permission2 = new Permission("read", "map");

        // Act
        var result = permission1.Implies(permission2);

        // Assert
        result.Should().BeTrue("wildcard action should imply any action");
    }

    [Fact]
    public void Implies_ShouldReturnTrue_WhenResourceTypeIsWildcard()
    {
        // Arrange
        var permission1 = new Permission("read", "*");
        var permission2 = new Permission("read", "map");

        // Act
        var result = permission1.Implies(permission2);

        // Assert
        result.Should().BeTrue("wildcard resource type should imply any resource type");
    }

    [Fact]
    public void Implies_ShouldReturnTrue_WhenBothAreWildcards()
    {
        // Arrange
        var permission1 = new Permission("*", "*");
        var permission2 = new Permission("read", "map");

        // Act
        var result = permission1.Implies(permission2);

        // Assert
        result.Should().BeTrue("full wildcard should imply everything");
    }

    [Fact]
    public void Implies_ShouldReturnFalse_WhenActionIsWildcardButResourceTypeDiffers()
    {
        // Arrange
        var permission1 = new Permission("*", "map");
        var permission2 = new Permission("read", "layer");

        // Act
        var result = permission1.Implies(permission2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Implies_ShouldReturnFalse_WhenResourceTypeIsWildcardButActionDiffers()
    {
        // Arrange
        var permission1 = new Permission("read", "*");
        var permission2 = new Permission("write", "map");

        // Act
        var result = permission1.Implies(permission2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Implies_ShouldThrowArgumentNullException_WhenRequiredIsNull()
    {
        // Arrange
        var permission = new Permission("read", "map");

        // Act & Assert
        var act = () => permission.Implies(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToString_ShouldReturnFormattedString_WithoutScope()
    {
        // Arrange
        var permission = new Permission("read", "map");

        // Act
        var result = permission.ToString();

        // Assert
        result.Should().Be("read:map");
    }

    [Fact]
    public void ToString_ShouldReturnFormattedString_WithScope()
    {
        // Arrange
        var permission = new Permission("read", "map", "own");

        // Act
        var result = permission.ToString();

        // Assert
        result.Should().Be("read:map:own");
    }

    [Fact]
    public void ImplicitConversion_ShouldConvertToString_Correctly()
    {
        // Arrange
        var permission = new Permission("read", "map", "own");

        // Act
        string result = permission;

        // Assert
        result.Should().Be("read:map:own");
    }

    [Fact]
    public void Read_FactoryMethod_ShouldCreateReadPermission()
    {
        // Arrange & Act
        var permission = Permission.Read("map");

        // Assert
        permission.Action.Should().Be("read");
        permission.ResourceType.Should().Be("map");
        permission.Scope.Should().BeNull();
    }

    [Fact]
    public void Read_FactoryMethod_ShouldCreateReadPermissionWithScope()
    {
        // Arrange & Act
        var permission = Permission.Read("map", "own");

        // Assert
        permission.Action.Should().Be("read");
        permission.ResourceType.Should().Be("map");
        permission.Scope.Should().Be("own");
    }

    [Fact]
    public void Write_FactoryMethod_ShouldCreateWritePermission()
    {
        // Arrange & Act
        var permission = Permission.Write("layer");

        // Assert
        permission.Action.Should().Be("write");
        permission.ResourceType.Should().Be("layer");
    }

    [Fact]
    public void Delete_FactoryMethod_ShouldCreateDeletePermission()
    {
        // Arrange & Act
        var permission = Permission.Delete("user");

        // Assert
        permission.Action.Should().Be("delete");
        permission.ResourceType.Should().Be("user");
    }

    [Fact]
    public void FullAccess_FactoryMethod_ShouldCreateWildcardActionPermission()
    {
        // Arrange & Act
        var permission = Permission.FullAccess("map");

        // Assert
        permission.Action.Should().Be("*");
        permission.ResourceType.Should().Be("map");
    }

    [Fact]
    public void Admin_FactoryMethod_ShouldCreateFullWildcardPermission()
    {
        // Arrange & Act
        var permission = Permission.Admin();

        // Assert
        permission.Action.Should().Be("*");
        permission.ResourceType.Should().Be("*");
        permission.Scope.Should().BeNull();
    }

    [Fact]
    public void Equality_ShouldBeTrue_ForIdenticalPermissions()
    {
        // Arrange
        var permission1 = new Permission("read", "map", "own");
        var permission2 = new Permission("read", "map", "own");

        // Act & Assert
        permission1.Should().Be(permission2);
        (permission1 == permission2).Should().BeTrue();
        permission1.GetHashCode().Should().Be(permission2.GetHashCode());
    }

    [Fact]
    public void Equality_ShouldBeFalse_ForDifferentPermissions()
    {
        // Arrange
        var permission1 = new Permission("read", "map");
        var permission2 = new Permission("write", "map");

        // Act & Assert
        permission1.Should().NotBe(permission2);
        (permission1 != permission2).Should().BeTrue();
    }

    [Fact]
    public void Permission_ShouldBeImmutable_AfterConstruction()
    {
        // Arrange
        var permission = new Permission("read", "map", "own");

        // Act & Assert
        permission.Action.Should().Be("read");
        permission.ResourceType.Should().Be("map");
        permission.Scope.Should().Be("own");

        // Cannot modify properties (compile-time guarantee via init-only setters)
        // This test documents the immutability design
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Domain;
using Honua.Server.Core.Domain.ValueObjects;
using Xunit;

namespace Honua.Server.Domain.Tests;

/// <summary>
/// Unit tests for the Username value object.
/// Tests username validation, formatting, and equality.
/// </summary>
[Trait("Category", "Unit")]
public class UsernameTests
{
    [Theory]
    [InlineData("abc")]
    [InlineData("user")]
    [InlineData("user123")]
    [InlineData("user-name")]
    [InlineData("user_name")]
    [InlineData("user.name")]
    [InlineData("user-name_123")]
    [InlineData("a1b2c3")]
    [InlineData("test.user-name_123")]
    [InlineData("a12")]
    public void Constructor_ShouldAcceptValidUsername_WhenProvided(string username)
    {
        // Arrange, Act & Assert
        var act = () => new Username(username);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_ShouldThrowDomainException_WhenUsernameIsEmpty(string username)
    {
        // Arrange, Act & Assert
        var act = () => new Username(username);
        act.Should().Throw<DomainException>()
            .WithMessage("Username cannot be empty.")
            .Which.ErrorCode.Should().Be("USERNAME_EMPTY");
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a")]
    [InlineData("12")]
    public void Constructor_ShouldThrowDomainException_WhenUsernameIsTooShort(string username)
    {
        // Arrange, Act & Assert
        var act = () => new Username(username);
        act.Should().Throw<DomainException>()
            .WithMessage("Username must be at least 3 characters long.")
            .Which.ErrorCode.Should().Be("USERNAME_TOO_SHORT");
    }

    [Fact]
    public void Constructor_ShouldThrowDomainException_WhenUsernameIsTooLong()
    {
        // Arrange
        var username = new string('a', 31);

        // Act & Assert
        var act = () => new Username(username);
        act.Should().Throw<DomainException>()
            .WithMessage("Username cannot exceed 30 characters.")
            .Which.ErrorCode.Should().Be("USERNAME_TOO_LONG");
    }

    [Theory]
    [InlineData("-username")]
    [InlineData("_username")]
    [InlineData(".username")]
    [InlineData("username-")]
    [InlineData("username_")]
    [InlineData("username.")]
    public void Constructor_ShouldThrowDomainException_WhenUsernameStartsOrEndsWithSpecialChar(string username)
    {
        // Arrange, Act & Assert
        var act = () => new Username(username);
        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("USERNAME_INVALID_FORMAT");
    }

    [Theory]
    [InlineData("user..name")]
    [InlineData("user--name")]
    [InlineData("user__name")]
    [InlineData("user...name")]
    public void Constructor_ShouldThrowDomainException_WhenUsernameHasConsecutiveSpecialChars(string username)
    {
        // Arrange, Act & Assert
        var act = () => new Username(username);
        act.Should().Throw<DomainException>()
            .WithMessage("Username cannot contain consecutive special characters.")
            .Which.ErrorCode.Should().Be("USERNAME_CONSECUTIVE_SPECIAL_CHARS");
    }

    [Theory]
    [InlineData("user name")]
    [InlineData("user@name")]
    [InlineData("user#name")]
    [InlineData("user$name")]
    [InlineData("user%name")]
    [InlineData("user&name")]
    [InlineData("user*name")]
    public void Constructor_ShouldThrowDomainException_WhenUsernameContainsInvalidChars(string username)
    {
        // Arrange, Act & Assert
        var act = () => new Username(username);
        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("USERNAME_INVALID_FORMAT");
    }

    [Fact]
    public void Constructor_ShouldTrimWhitespace_FromUsername()
    {
        // Arrange
        var username = "  testuser  ";

        // Act
        var usernameObj = new Username(username);

        // Assert
        usernameObj.Value.Should().Be("testuser");
    }

    [Fact]
    public void Constructor_ShouldNotNormalizeCase_OfUsername()
    {
        // Arrange
        var username = "TestUser";

        // Act
        var usernameObj = new Username(username);

        // Assert
        usernameObj.Value.Should().Be("TestUser", "username case should be preserved");
    }

    [Fact]
    public void From_ShouldCreateUsername_WhenValidUsernameProvided()
    {
        // Arrange
        var username = "testuser";

        // Act
        var usernameObj = Username.From(username);

        // Assert
        usernameObj.Value.Should().Be("testuser");
    }

    [Fact]
    public void From_ShouldThrowDomainException_WhenInvalidUsernameProvided()
    {
        // Arrange
        var username = "ab";

        // Act & Assert
        var act = () => Username.From(username);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void TryCreate_ShouldReturnTrue_WhenValidUsernameProvided()
    {
        // Arrange
        var username = "testuser";

        // Act
        var result = Username.TryCreate(username, out var usernameObj);

        // Assert
        result.Should().BeTrue();
        usernameObj.Should().NotBeNull();
        usernameObj!.Value.Should().Be("testuser");
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("")]
    [InlineData("user..name")]
    [InlineData("-username")]
    public void TryCreate_ShouldReturnFalse_WhenInvalidUsernameProvided(string username)
    {
        // Arrange, Act
        var result = Username.TryCreate(username, out var usernameObj);

        // Assert
        result.Should().BeFalse();
        usernameObj.Should().BeNull();
    }

    [Fact]
    public void ToString_ShouldReturnUsernameValue_WhenCalled()
    {
        // Arrange
        var usernameObj = new Username("testuser");

        // Act
        var result = usernameObj.ToString();

        // Assert
        result.Should().Be("testuser");
    }

    [Fact]
    public void ImplicitConversion_ShouldConvertToString_Correctly()
    {
        // Arrange
        var usernameObj = new Username("testuser");

        // Act
        string result = usernameObj;

        // Assert
        result.Should().Be("testuser");
    }

    [Fact]
    public void Equality_ShouldBeTrue_ForSameUsername()
    {
        // Arrange
        var username1 = new Username("testuser");
        var username2 = new Username("testuser");

        // Act & Assert
        username1.Should().Be(username2);
        (username1 == username2).Should().BeTrue();
        username1.GetHashCode().Should().Be(username2.GetHashCode());
    }

    [Fact]
    public void Equality_ShouldBeFalse_ForDifferentUsernames()
    {
        // Arrange
        var username1 = new Username("testuser1");
        var username2 = new Username("testuser2");

        // Act & Assert
        username1.Should().NotBe(username2);
        (username1 != username2).Should().BeTrue();
    }

    [Fact]
    public void Equality_ShouldBeFalse_ForDifferentCasing()
    {
        // Arrange
        var username1 = new Username("TestUser");
        var username2 = new Username("testuser");

        // Act & Assert
        username1.Should().NotBe(username2, "username case should be preserved and compared");
    }

    [Fact]
    public void Constructor_ShouldAcceptMinLength_Of3Characters()
    {
        // Arrange
        var username = "abc";

        // Act
        var usernameObj = new Username(username);

        // Assert
        usernameObj.Value.Should().Be("abc");
    }

    [Fact]
    public void Constructor_ShouldAcceptMaxLength_Of30Characters()
    {
        // Arrange
        var username = new string('a', 30);

        // Act
        var usernameObj = new Username(username);

        // Assert
        usernameObj.Value.Should().HaveLength(30);
    }

    [Theory]
    [InlineData("abc", true)]
    [InlineData("user-name", true)]
    [InlineData("user_name", true)]
    [InlineData("user.name", true)]
    [InlineData("user123", true)]
    [InlineData("123user", true)]
    [InlineData("a", false)]
    [InlineData("ab", false)]
    public void Constructor_ShouldValidateLength_Correctly(string username, bool shouldBeValid)
    {
        // Arrange, Act & Assert
        if (shouldBeValid)
        {
            var act = () => new Username(username);
            act.Should().NotThrow();
        }
        else
        {
            var act = () => new Username(username);
            act.Should().Throw<DomainException>();
        }
    }

    [Fact]
    public void Username_ShouldBeImmutable_AfterConstruction()
    {
        // Arrange
        var usernameObj = new Username("testuser");

        // Act & Assert
        usernameObj.Value.Should().Be("testuser");

        // Cannot modify properties (compile-time guarantee via init-only setters)
        // This test documents the immutability design
    }

    [Theory]
    [InlineData("a1b2c3d4e5", true)]
    [InlineData("user.test-name_123", true)]
    [InlineData("Test_User", true)]
    [InlineData("simple", true)]
    public void Constructor_ShouldAcceptMixedValidCharacters_Correctly(string username, bool shouldBeValid)
    {
        // Arrange, Act & Assert
        var act = () => new Username(username);
        if (shouldBeValid)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<DomainException>();
        }
    }
}

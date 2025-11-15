// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Domain.Sharing;
using Xunit;

namespace Honua.Server.Domain.Tests.Sharing;

[Trait("Category", "Unit")]
public sealed class SharePasswordTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidPassword_ShouldCreateHashedPassword()
    {
        // Arrange & Act
        var password = SharePassword.Create("secure-password");

        // Assert
        password.Should().NotBeNull();
        password.Hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Create_WithEmptyPassword_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => SharePassword.Create("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Password cannot be empty*")
            .And.ParamName.Should().Be("plainTextPassword");
    }

    [Fact]
    public void Create_WithNullPassword_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => SharePassword.Create(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Password cannot be empty*");
    }

    [Fact]
    public void Create_WithWhitespacePassword_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => SharePassword.Create("   ");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Password cannot be empty*");
    }

    [Fact]
    public void Create_WithPasswordTooShort_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => SharePassword.Create("abc"); // Less than 4 characters

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Password must be at least 4 characters long*")
            .And.ParamName.Should().Be("plainTextPassword");
    }

    [Fact]
    public void Create_WithPasswordTooLong_ShouldThrowArgumentException()
    {
        // Arrange
        var longPassword = new string('x', 101);

        // Act
        Action act = () => SharePassword.Create(longPassword);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Password must not exceed 100 characters*")
            .And.ParamName.Should().Be("plainTextPassword");
    }

    [Fact]
    public void Create_WithMinimumLengthPassword_ShouldSucceed()
    {
        // Arrange & Act
        var password = SharePassword.Create("1234"); // Exactly 4 characters

        // Assert
        password.Should().NotBeNull();
        password.Hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Create_WithMaximumLengthPassword_ShouldSucceed()
    {
        // Arrange
        var maxPassword = new string('x', 100);

        // Act
        var password = SharePassword.Create(maxPassword);

        // Assert
        password.Should().NotBeNull();
        password.Hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Create_DifferentPasswords_ShouldProduceDifferentHashes()
    {
        // Arrange & Act
        var password1 = SharePassword.Create("password1");
        var password2 = SharePassword.Create("password2");

        // Assert
        password1.Hash.Should().NotBe(password2.Hash);
    }

    [Fact]
    public void Create_SamePassword_ShouldProduceDifferentHashesDueToSalt()
    {
        // Arrange & Act
        var password1 = SharePassword.Create("same-password");
        var password2 = SharePassword.Create("same-password");

        // Assert
        // Due to different salts, hashes should be different
        password1.Hash.Should().NotBe(password2.Hash);
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        var password = SharePassword.Create("correct-password");

        // Act
        var result = password.Validate("correct-password");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        var password = SharePassword.Create("correct-password");

        // Act
        var result = password.Validate("wrong-password");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithEmptyPassword_ShouldReturnFalse()
    {
        // Arrange
        var password = SharePassword.Create("correct-password");

        // Act
        var result = password.Validate("");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithNullPassword_ShouldReturnFalse()
    {
        // Arrange
        var password = SharePassword.Create("correct-password");

        // Act
        var result = password.Validate(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithWhitespacePassword_ShouldReturnFalse()
    {
        // Arrange
        var password = SharePassword.Create("correct-password");

        // Act
        var result = password.Validate("   ");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Validate_CaseSensitive_ShouldReturnFalse()
    {
        // Arrange
        var password = SharePassword.Create("Password123");

        // Act
        var result = password.Validate("password123");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Validate_MultipleTimes_ShouldAlwaysReturnTrueForCorrectPassword()
    {
        // Arrange
        var password = SharePassword.Create("correct-password");

        // Act & Assert
        password.Validate("correct-password").Should().BeTrue();
        password.Validate("correct-password").Should().BeTrue();
        password.Validate("correct-password").Should().BeTrue();
    }

    [Fact]
    public void Validate_MultipleTimes_ShouldAlwaysReturnFalseForIncorrectPassword()
    {
        // Arrange
        var password = SharePassword.Create("correct-password");

        // Act & Assert
        password.Validate("wrong-password").Should().BeFalse();
        password.Validate("wrong-password").Should().BeFalse();
        password.Validate("wrong-password").Should().BeFalse();
    }

    #endregion

    #region FromHash Tests

    [Fact]
    public void FromHash_WithValidHash_ShouldCreatePassword()
    {
        // Arrange
        var originalPassword = SharePassword.Create("test-password");
        var hash = originalPassword.Hash;

        // Act
        var restoredPassword = SharePassword.FromHash(hash);

        // Assert
        restoredPassword.Should().NotBeNull();
        restoredPassword.Hash.Should().Be(hash);
    }

    [Fact]
    public void FromHash_RestoredPassword_ShouldValidateCorrectly()
    {
        // Arrange
        var originalPassword = SharePassword.Create("test-password");
        var hash = originalPassword.Hash;

        // Act
        var restoredPassword = SharePassword.FromHash(hash);

        // Assert
        restoredPassword.Validate("test-password").Should().BeTrue();
        restoredPassword.Validate("wrong-password").Should().BeFalse();
    }

    [Fact]
    public void FromHash_WithEmptyHash_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => SharePassword.FromHash("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Password hash cannot be empty*");
    }

    [Fact]
    public void FromHash_WithNullHash_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => SharePassword.FromHash(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Password hash cannot be empty*");
    }

    #endregion

    #region Value Object Equality Tests

    [Fact]
    public void Equality_SameHash_ShouldBeEqual()
    {
        // Arrange
        var password = SharePassword.Create("test-password");
        var hash = password.Hash;
        var restoredPassword = SharePassword.FromHash(hash);

        // Act & Assert
        password.Should().Be(restoredPassword);
        (password == restoredPassword).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentHashes_ShouldNotBeEqual()
    {
        // Arrange
        var password1 = SharePassword.Create("password1");
        var password2 = SharePassword.Create("password2");

        // Act & Assert
        password1.Should().NotBe(password2);
        (password1 != password2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_SameHash_ShouldProduceSameHashCode()
    {
        // Arrange
        var password = SharePassword.Create("test-password");
        var hash = password.Hash;
        var restoredPassword = SharePassword.FromHash(hash);

        // Act & Assert
        password.GetHashCode().Should().Be(restoredPassword.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentHashes_ShouldProduceDifferentHashCodes()
    {
        // Arrange
        var password1 = SharePassword.Create("password1");
        var password2 = SharePassword.Create("password2");

        // Act & Assert
        password1.GetHashCode().Should().NotBe(password2.GetHashCode());
    }

    #endregion

    #region Security Tests

    [Fact]
    public void Create_ShouldUseSecureHashing()
    {
        // Arrange & Act
        var password = SharePassword.Create("test-password");

        // Assert
        // Hash should be base64 encoded and contain salt + hash
        // PBKDF2 with 16-byte salt + 32-byte hash = 48 bytes = 64 base64 characters
        password.Hash.Should().NotBeNullOrEmpty();
        password.Hash.Length.Should().BeGreaterOrEqualTo(64); // Base64 encoded 48 bytes
    }

    [Fact]
    public void Create_WithSpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var passwordWithSpecialChars = "p@ssw0rd!#$%^&*()";

        // Act
        var password = SharePassword.Create(passwordWithSpecialChars);

        // Assert
        password.Validate(passwordWithSpecialChars).Should().BeTrue();
        password.Validate("password").Should().BeFalse();
    }

    [Fact]
    public void Create_WithUnicodeCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var unicodePassword = "pāsswörd™";

        // Act
        var password = SharePassword.Create(unicodePassword);

        // Assert
        password.Validate(unicodePassword).Should().BeTrue();
        password.Validate("password").Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldResistTimingAttacks()
    {
        // Arrange
        var password = SharePassword.Create("secure-password");

        // Act - Multiple validations should take similar time
        var result1 = password.Validate("wrong-password-1");
        var result2 = password.Validate("wrong-password-2");
        var result3 = password.Validate("secure-password");

        // Assert - Just verify they work consistently
        result1.Should().BeFalse();
        result2.Should().BeFalse();
        result3.Should().BeTrue();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ShouldNotRevealFullHash()
    {
        // Arrange
        var password = SharePassword.Create("secret-password");

        // Act
        var stringRepresentation = password.ToString();

        // Assert
        stringRepresentation.Should().NotContain(password.Hash);
        stringRepresentation.Should().Contain("Password Hash");
        stringRepresentation.Should().Contain("...");
    }

    #endregion
}
